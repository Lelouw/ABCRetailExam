using System.Text.Json;
using ABCRetail.Shared;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;

namespace ABCRetail.Functions.Infrastructure;

public sealed class TelemetryPipeline
{
    private readonly QueueClient? _queue;
    private readonly TelemetryStore _store;

    public TelemetryPipeline(IConfiguration configuration)
    {
        var storage = configuration["AzureWebJobsStorage"];
        var queueName = configuration["TelemetryQueueName"];
        if (string.IsNullOrWhiteSpace(queueName))
        {
            queueName = StorageResourceNames.TelemetryQueue;
        }

        _queue = string.IsNullOrWhiteSpace(storage) ? null : new QueueClient(storage, queueName);
        _store = new TelemetryStore(configuration.GetConnectionString("TelemetryDb") ?? string.Empty);
    }

    public async Task EnqueueAsync(TelemetryReading reading, CancellationToken cancellationToken)
    {
        if (_queue is null)
        {
            throw new InvalidOperationException("AzureWebJobsStorage is not configured.");
        }

        await _queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var json = JsonSerializer.Serialize(reading, RetailJson.Options);
        await _queue.SendMessageAsync(json, cancellationToken);
    }

    public Task SaveAsync(ProcessedReading reading, CancellationToken cancellationToken) =>
        _store.SaveAsync(reading, cancellationToken);
}
