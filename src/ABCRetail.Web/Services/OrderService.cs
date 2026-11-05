using System.Text.Json;
using ABCRetail.Shared;
using ABCRetail.Web.Infrastructure;

namespace ABCRetail.Web.Services;

public interface IOrderService
{
    Task<IReadOnlyList<OrderEntity>> ListAsync(CancellationToken cancellationToken);

    Task<OrderEntity?> GetAsync(string orderId, CancellationToken cancellationToken);

    Task<OrderEntity> SubmitAsync(string customerId, string productId, int quantity, CancellationToken cancellationToken);
}

public sealed class OrderService : IOrderService
{
    private readonly AzureStorageContext _storage;
    private readonly ICustomerStore _customers;
    private readonly IProductService _products;

    public OrderService(AzureStorageContext storage, ICustomerStore customers, IProductService products)
    {
        _storage = storage;
        _customers = customers;
        _products = products;
    }

    public async Task<IReadOnlyList<OrderEntity>> ListAsync(CancellationToken cancellationToken)
    {
        var orders = new List<OrderEntity>();
        await foreach (var order in _storage.GetTable(_storage.Options.OrderTableName).QueryAsync<OrderEntity>(
            item => item.PartitionKey == StorageResourceNames.OrderPartition,
            cancellationToken: cancellationToken))
        {
            orders.Add(order);
        }

        orders.Sort((left, right) => right.SubmittedAtUtc.CompareTo(left.SubmittedAtUtc));
        return orders;
    }

    public async Task<OrderEntity?> GetAsync(string orderId, CancellationToken cancellationToken)
    {
        var response = await _storage.GetTable(_storage.Options.OrderTableName)
            .GetEntityIfExistsAsync<OrderEntity>(StorageResourceNames.OrderPartition, orderId, cancellationToken: cancellationToken);

        return response.HasValue ? response.Value : null;
    }

    public async Task<OrderEntity> SubmitAsync(string customerId, string productId, int quantity, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetAsync(customerId, cancellationToken)
            ?? throw new InvalidOperationException("Select a customer who is already saved.");

        var product = await _products.GetAsync(productId, cancellationToken)
            ?? throw new InvalidOperationException("Select a product that is already saved.");

        var order = new OrderEntity
        {
            PartitionKey = StorageResourceNames.OrderPartition,
            RowKey = $"ORD-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            CustomerId = customer.RowKey,
            CustomerName = customer.FullName,
            ProductId = product.RowKey,
            ProductName = product.ProductName,
            Quantity = quantity,
            UnitPrice = product.Price,
            TotalAmount = OrderCalculator.Total(product.Price, quantity),
            Status = OrderStatuses.Queued,
            SubmittedAtUtc = DateTime.UtcNow,
            Note = "Waiting for the order function."
        };

        var message = new OrderMessage
        {
            OrderId = order.RowKey,
            CustomerId = order.CustomerId,
            CustomerName = order.CustomerName,
            ProductId = order.ProductId,
            ProductName = order.ProductName,
            Quantity = order.Quantity,
            UnitPrice = order.UnitPrice,
            TotalAmount = order.TotalAmount,
            SubmittedAtUtc = order.SubmittedAtUtc
        };

        var validationError = RetailMessageValidator.ValidateOrder(message);
        if (validationError is not null)
        {
            throw new InvalidOperationException(validationError);
        }

        await _storage.GetTable(_storage.Options.OrderTableName).AddEntityAsync(order, cancellationToken);

        var queue = _storage.GetQueue(_storage.Options.OrderQueueName);
        await queue.SendMessageAsync(JsonSerializer.Serialize(message, RetailJson.Options), cancellationToken);

        return order;
    }
}
