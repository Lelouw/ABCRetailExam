using ABCRetail.Functions.Infrastructure;
using Microsoft.Azure.Functions.Worker;

namespace ABCRetail.Functions;

public class QueueFunctions
{
    private readonly StorageGateway _storage;

    public QueueFunctions(StorageGateway storage)
    {
        _storage = storage;
    }

    [Function(nameof(ProcessOrder))]
    public Task ProcessOrder(
        [QueueTrigger("%OrderQueueName%", Connection = "AzureWebJobsStorage")] string body,
        CancellationToken cancellationToken) =>
        _storage.ProcessOrderAsync(body, cancellationToken);

    [Function(nameof(ProcessInventory))]
    public Task ProcessInventory(
        [QueueTrigger("%InventoryQueueName%", Connection = "AzureWebJobsStorage")] string body,
        CancellationToken cancellationToken) =>
        _storage.ProcessInventoryAsync(body, cancellationToken);
}
