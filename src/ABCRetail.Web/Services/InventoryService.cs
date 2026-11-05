using System.Text.Json;
using ABCRetail.Shared;
using ABCRetail.Web.Infrastructure;

namespace ABCRetail.Web.Services;

public interface IInventoryService
{
    Task<string> SubmitAsync(string productId, string operation, int quantity, string? reason, CancellationToken cancellationToken);
}

public sealed class InventoryService : IInventoryService
{
    private readonly AzureStorageContext _storage;
    private readonly IProductService _products;

    public InventoryService(AzureStorageContext storage, IProductService products)
    {
        _storage = storage;
        _products = products;
    }

    public async Task<string> SubmitAsync(
        string productId,
        string operation,
        int quantity,
        string? reason,
        CancellationToken cancellationToken)
    {
        var product = await _products.GetAsync(productId, cancellationToken)
            ?? throw new InvalidOperationException("Select a product that is already saved.");

        var message = new InventoryMessage
        {
            TransactionId = $"INV-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            ProductId = product.RowKey,
            ProductName = product.ProductName,
            Operation = operation,
            Quantity = quantity,
            Reason = reason?.Trim() ?? string.Empty,
            SubmittedAtUtc = DateTime.UtcNow
        };

        var validationError = RetailMessageValidator.ValidateInventory(message);
        if (validationError is not null)
        {
            throw new InvalidOperationException(validationError);
        }

        var queue = _storage.GetQueue(_storage.Options.InventoryQueueName);
        await queue.SendMessageAsync(JsonSerializer.Serialize(message, RetailJson.Options), cancellationToken);
        return message.TransactionId;
    }
}
