using System.Text;
using System.Text.Json;
using ABCRetail.Shared;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;

namespace ABCRetail.Functions.Infrastructure;

public sealed class StorageGateway
{
    private readonly ILogger<StorageGateway> _logger;

    public StorageGateway(ILogger<StorageGateway> logger)
    {
        _logger = logger;
    }

    public async Task<string> SaveCustomerAsync(CustomerWrite customer, CancellationToken cancellationToken)
    {
        var error = RetailMessageValidator.ValidateCustomer(customer);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        var entity = new Customer
        {
            PartitionKey = StorageResourceNames.CustomerPartition,
            RowKey = Guid.NewGuid().ToString("N"),
            FirstName = customer.FirstName.Trim(),
            LastName = customer.LastName.Trim(),
            Email = customer.Email.Trim(),
            PhoneNumber = customer.PhoneNumber.Trim(),
            Address = customer.Address.Trim()
        };

        await GetTable("CustomerTableName", StorageResourceNames.CustomersTable)
            .AddEntityAsync(entity, cancellationToken);

        return entity.RowKey;
    }

    public async Task<string> UploadImageAsync(Stream content, string contentType, long length, CancellationToken cancellationToken)
    {
        var error = ImageUploadRules.Validate(contentType, length);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        if (!ImageUploadRules.TryGetExtension(contentType, out var extension))
        {
            throw new InvalidOperationException("Upload a JPG, PNG, or WEBP image.");
        }

        var container = new BlobContainerClient(ConnectionString, Setting("ImageContainerName", StorageResourceNames.ImageContainer));
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobName = $"product-{Guid.NewGuid():N}{extension}";
        await container.GetBlobClient(blobName).UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            },
            cancellationToken);

        return blobName;
    }

    public async Task<string> EnqueueOrderAsync(OrderMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.OrderId))
        {
            message.OrderId = NewId("ORD");
        }

        if (message.SubmittedAtUtc == default)
        {
            message.SubmittedAtUtc = DateTime.UtcNow;
        }

        message.TotalAmount = OrderCalculator.Total(message.UnitPrice, message.Quantity);
        var error = RetailMessageValidator.ValidateOrder(message);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        await SendAsync(Setting("OrderQueueName", StorageResourceNames.OrderQueue), message, cancellationToken);
        return message.OrderId;
    }

    public async Task<string> EnqueueInventoryAsync(InventoryMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.TransactionId))
        {
            message.TransactionId = NewId("INV");
        }

        if (message.SubmittedAtUtc == default)
        {
            message.SubmittedAtUtc = DateTime.UtcNow;
        }

        var error = RetailMessageValidator.ValidateInventory(message);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        await SendAsync(Setting("InventoryQueueName", StorageResourceNames.InventoryQueue), message, cancellationToken);
        return message.TransactionId;
    }

    public async Task<string> SaveActivityLogAsync(ActivityLogWrite log, CancellationToken cancellationToken)
    {
        var error = RetailMessageValidator.ValidateActivityLog(log);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        var fileName = FileNameSanitizer.Sanitize($"{log.ActivityType}-{log.ReferenceNumber}-{DateTime.UtcNow:yyyyMMddHHmmss}.txt");
        var content =
            $"Reference: {log.ReferenceNumber}{Environment.NewLine}" +
            $"Activity: {log.ActivityType}{Environment.NewLine}" +
            $"Description: {log.Description}{Environment.NewLine}" +
            $"Date: {DateTime.UtcNow:u}";

        await WriteTextAsync(Setting("LogShareName", StorageResourceNames.LogShare), fileName, content, cancellationToken);
        return fileName;
    }

    public async Task ProcessOrderAsync(string body, CancellationToken cancellationToken)
    {
        OrderMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<OrderMessage>(body, RetailJson.Options);
        }
        catch (JsonException)
        {
            _logger.LogWarning("Skipped an order message because it was not valid JSON.");
            return;
        }

        var error = RetailMessageValidator.ValidateOrder(message);
        if (error is not null || message is null)
        {
            _logger.LogWarning("Skipped an order message: {Reason}", error ?? "Empty message.");
            return;
        }

        var orders = GetTable("OrderTableName", StorageResourceNames.OrdersTable);
        var existing = await orders.GetEntityIfExistsAsync<OrderEntity>(
            StorageResourceNames.OrderPartition,
            message.OrderId,
            cancellationToken: cancellationToken);

        var entity = existing.HasValue ? existing.Value! : new OrderEntity
        {
            PartitionKey = StorageResourceNames.OrderPartition,
            RowKey = message.OrderId,
            CustomerId = message.CustomerId,
            CustomerName = message.CustomerName,
            ProductId = message.ProductId,
            ProductName = message.ProductName,
            Quantity = message.Quantity,
            UnitPrice = message.UnitPrice,
            TotalAmount = message.TotalAmount,
            SubmittedAtUtc = AsUtc(message.SubmittedAtUtc),
            Status = OrderStatuses.Queued
        };

        if (entity.Status is OrderStatuses.Processed or OrderStatuses.Rejected)
        {
            await WriteOrderLogAsync(entity, cancellationToken);
            return;
        }

        var products = GetTable("ProductTableName", StorageResourceNames.ProductsTable);
        var productResult = await products.GetEntityIfExistsAsync<Product>(
            StorageResourceNames.ProductPartition,
            message.ProductId,
            cancellationToken: cancellationToken);

        if (!productResult.HasValue)
        {
            entity.Status = OrderStatuses.Rejected;
            entity.Note = "Product was not found.";
        }
        else if (productResult.Value!.StockQuantity < message.Quantity)
        {
            entity.Status = OrderStatuses.Rejected;
            entity.Note = $"Not enough stock. Available: {productResult.Value.StockQuantity}.";
        }
        else
        {
            var product = productResult.Value;
            if (!entity.StockApplied)
            {
                product.StockQuantity -= message.Quantity;
                await products.UpdateEntityAsync(product, product.ETag, TableUpdateMode.Replace, cancellationToken);
                entity.StockApplied = true;
            }

            entity.Status = OrderStatuses.Processed;
            entity.Note = "Stock updated.";
        }

        entity.ProcessedAtUtc = DateTime.UtcNow;
        entity.SubmittedAtUtc = AsUtc(entity.SubmittedAtUtc);
        await orders.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
        await WriteOrderLogAsync(entity, cancellationToken);
        _logger.LogInformation("Order {OrderId} finished with status {Status}.", entity.RowKey, entity.Status);
    }

    public async Task ProcessInventoryAsync(string body, CancellationToken cancellationToken)
    {
        InventoryMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<InventoryMessage>(body, RetailJson.Options);
        }
        catch (JsonException)
        {
            _logger.LogWarning("Skipped an inventory message because it was not valid JSON.");
            return;
        }

        var error = RetailMessageValidator.ValidateInventory(message);
        if (error is not null || message is null)
        {
            _logger.LogWarning("Skipped an inventory message: {Reason}", error ?? "Empty message.");
            return;
        }

        var fileName = $"inventory-{message.TransactionId}.txt";
        if (await FileExistsAsync(fileName, cancellationToken))
        {
            return;
        }

        var products = GetTable("ProductTableName", StorageResourceNames.ProductsTable);
        var productResult = await products.GetEntityIfExistsAsync<Product>(
            StorageResourceNames.ProductPartition,
            message.ProductId,
            cancellationToken: cancellationToken);

        string status;
        string note;
        if (!productResult.HasValue)
        {
            status = "Rejected";
            note = "Product was not found.";
        }
        else
        {
            try
            {
                var product = productResult.Value!;
                product.StockQuantity = InventoryOperations.Apply(product.StockQuantity, message.Operation, message.Quantity);
                await products.UpdateEntityAsync(product, product.ETag, TableUpdateMode.Replace, cancellationToken);
                status = "Completed";
                note = $"{message.Operation} applied. Stock is now {product.StockQuantity}.";
            }
            catch (InvalidOperationException ex)
            {
                status = "Rejected";
                note = ex.Message;
            }
        }

        var content =
            $"Transaction: {message.TransactionId}{Environment.NewLine}" +
            $"Product: {message.ProductName}{Environment.NewLine}" +
            $"Operation: {message.Operation}{Environment.NewLine}" +
            $"Quantity: {message.Quantity}{Environment.NewLine}" +
            $"Reason: {message.Reason}{Environment.NewLine}" +
            $"Status: {status}{Environment.NewLine}" +
            $"Note: {note}{Environment.NewLine}" +
            $"Date: {DateTime.UtcNow:u}";

        await WriteTextAsync(Setting("LogShareName", StorageResourceNames.LogShare), fileName, content, cancellationToken);
        _logger.LogInformation("Inventory transaction {TransactionId} finished with status {Status}.", message.TransactionId, status);
    }

    private async Task WriteOrderLogAsync(OrderEntity entity, CancellationToken cancellationToken)
    {
        var content =
            $"Order: {entity.RowKey}{Environment.NewLine}" +
            $"Customer: {entity.CustomerName}{Environment.NewLine}" +
            $"Product: {entity.ProductName}{Environment.NewLine}" +
            $"Quantity: {entity.Quantity}{Environment.NewLine}" +
            $"Total: {entity.TotalAmount:0.00}{Environment.NewLine}" +
            $"Status: {entity.Status}{Environment.NewLine}" +
            $"Note: {entity.Note}{Environment.NewLine}" +
            $"Date: {DateTime.UtcNow:u}";

        await WriteTextAsync(Setting("LogShareName", StorageResourceNames.LogShare), $"order-{entity.RowKey}.txt", content, cancellationToken);
    }

    private async Task<bool> FileExistsAsync(string fileName, CancellationToken cancellationToken)
    {
        if (LocalFileShare.IsEmulator(ConnectionString))
        {
            return File.Exists(LocalFileShare.GetPath(Setting("LogShareName", StorageResourceNames.LogShare), fileName));
        }

        var share = new ShareClient(ConnectionString, Setting("LogShareName", StorageResourceNames.LogShare));
        if (!await share.ExistsAsync(cancellationToken))
        {
            return false;
        }

        return await share.GetRootDirectoryClient().GetFileClient(fileName).ExistsAsync(cancellationToken);
    }

    private async Task WriteTextAsync(string shareName, string fileName, string content, CancellationToken cancellationToken)
    {
        if (LocalFileShare.IsEmulator(ConnectionString))
        {
            await File.WriteAllTextAsync(LocalFileShare.GetPath(shareName, fileName), content, cancellationToken);
            return;
        }

        var share = new ShareClient(ConnectionString, shareName);
        await share.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var file = share.GetRootDirectoryClient().GetFileClient(fileName);
        var bytes = Encoding.UTF8.GetBytes(content);
        await file.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        await file.CreateAsync(bytes.Length, cancellationToken: cancellationToken);
        await file.UploadRangeAsync(new Azure.HttpRange(0, bytes.Length), new MemoryStream(bytes), cancellationToken: cancellationToken);
    }

    private async Task SendAsync<T>(string queueName, T message, CancellationToken cancellationToken)
    {
        var queue = new QueueClient(ConnectionString, queueName);
        await queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await queue.SendMessageAsync(JsonSerializer.Serialize(message, RetailJson.Options), cancellationToken);
    }

    private TableClient GetTable(string settingName, string fallback) =>
        new(ConnectionString, Setting(settingName, fallback));

    private string ConnectionString =>
        Environment.GetEnvironmentVariable("AzureWebJobsStorage")
        ?? throw new InvalidOperationException("AzureWebJobsStorage is not configured.");

    private static string Setting(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;

    private static string NewId(string prefix) =>
        $"{prefix}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
