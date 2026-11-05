namespace ABCRetail.Shared;

public static class TelemetryMetrics
{
    public const string Temperature = "Temperature";
    public const string Humidity = "Humidity";
    public const string CpuPercent = "CpuPercent";
    public const string Requests = "Requests";

    public static readonly IReadOnlyList<string> All =
    [
        Temperature,
        Humidity,
        CpuPercent,
        Requests
    ];
}

public static class ReadingStatuses
{
    public const string Normal = "Normal";
    public const string Warning = "Warning";
    public const string Critical = "Critical";
    public const string Rejected = "Rejected";
}

public sealed class TelemetryReading
{
    public Guid Id { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public string Metric { get; set; } = string.Empty;

    public double Value { get; set; }

    public DateTime RecordedAtUtc { get; set; }
}

public sealed class ProcessedReading
{
    public Guid Id { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public string Metric { get; set; } = string.Empty;

    public double Value { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTime RecordedAtUtc { get; set; }

    public DateTime ProcessedAtUtc { get; set; }
}

public sealed class ReadingSummary
{
    public int Normal { get; init; }

    public int Warning { get; init; }

    public int Critical { get; init; }

    public int Rejected { get; init; }

    public int Total => Normal + Warning + Critical + Rejected;
}

public sealed record ReadingDecision(string Status, string Reason);
