using System.Text.Json;
using ABCRetail.Shared;
using ABCRetail.Web.Infrastructure;

namespace ABCRetail.Web.Services;

public sealed class TelemetryIngestService
{
    private readonly AzureStorageContext _storage;

    public TelemetryIngestService(AzureStorageContext storage)
    {
        _storage = storage;
    }

    public async Task EnqueueAsync(TelemetryReading reading, CancellationToken cancellationToken)
    {
        var queue = _storage.GetQueue(_storage.Options.TelemetryQueueName);
        await queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var json = JsonSerializer.Serialize(reading, RetailJson.Options);
        await queue.SendMessageAsync(json, cancellationToken);
    }

    public async Task<int> SimulateAsync(int count, CancellationToken cancellationToken)
    {
        var total = Math.Clamp(count, 1, 200);
        var devices = new[] { "sensor-01", "sensor-02", "till-01", "web-01" };
        var metrics = TelemetryMetrics.All;
        for (var index = 0; index < total; index++)
        {
            var metric = metrics[Random.Shared.Next(metrics.Count)];
            await EnqueueAsync(new TelemetryReading
            {
                Id = Guid.NewGuid(),
                DeviceId = devices[Random.Shared.Next(devices.Length)],
                Metric = metric,
                Value = Sample(metric),
                RecordedAtUtc = DateTime.UtcNow
            }, cancellationToken);
        }

        return total;
    }

    private static double Sample(string metric) => metric switch
    {
        TelemetryMetrics.Temperature => Random.Shared.Next(18, 52),
        TelemetryMetrics.Humidity => Random.Shared.Next(10, 96),
        TelemetryMetrics.CpuPercent => Random.Shared.Next(15, 100),
        _ => Random.Shared.Next(20, 1400)
    };
}
