using ABCRetail.Shared;

namespace ABCRetail.Web.Models;

public sealed class ReadingsPage
{
    public bool DatabaseConfigured { get; init; }

    public string? Error { get; init; }

    public ReadingSummary Summary { get; init; } = new();

    public IReadOnlyList<ProcessedReading> Readings { get; init; } = [];
}

public sealed class ReadingInput
{
    public string DeviceId { get; set; } = "sensor-01";

    public string Metric { get; set; } = TelemetryMetrics.Temperature;

    public double Value { get; set; } = 24;
}
