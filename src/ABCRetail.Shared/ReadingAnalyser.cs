namespace ABCRetail.Shared;

public static class ReadingAnalyser
{
    public static string? Validate(TelemetryReading? reading)
    {
        if (reading is null)
        {
            return "A reading is required.";
        }

        if (string.IsNullOrWhiteSpace(reading.DeviceId) || reading.DeviceId.Trim().Length > 64)
        {
            return "Device id is required and must be 64 characters or fewer.";
        }

        if (!TelemetryMetrics.All.Contains(reading.Metric))
        {
            return "Metric must be Temperature, Humidity, CpuPercent, or Requests.";
        }

        if (double.IsNaN(reading.Value) || double.IsInfinity(reading.Value))
        {
            return "Value must be a finite number.";
        }

        if (reading.RecordedAtUtc == default)
        {
            return "Recorded time is required.";
        }

        var now = DateTime.UtcNow;
        if (reading.RecordedAtUtc > now.AddMinutes(5))
        {
            return "Recorded time cannot be in the future.";
        }

        if (reading.RecordedAtUtc < now.AddHours(-24))
        {
            return "Recorded time is older than 24 hours.";
        }

        return reading.Metric switch
        {
            TelemetryMetrics.Temperature when reading.Value is < -40 or > 80 => "Temperature is outside the sensor range of -40 to 80.",
            TelemetryMetrics.Humidity when reading.Value is < 0 or > 100 => "Humidity must be between 0 and 100.",
            TelemetryMetrics.CpuPercent when reading.Value is < 0 or > 100 => "CPU percent must be between 0 and 100.",
            TelemetryMetrics.Requests when reading.Value < 0 => "Request count cannot be negative.",
            _ => null
        };
    }

    public static ReadingDecision Analyse(TelemetryReading reading)
    {
        return reading.Metric switch
        {
            TelemetryMetrics.Temperature => UpperBand(reading.Value, 30, 40, "Temperature", "°C"),
            TelemetryMetrics.CpuPercent => UpperBand(reading.Value, 70, 90, "CPU", "%"),
            TelemetryMetrics.Requests => UpperBand(reading.Value, 500, 1000, "Requests", ""),
            TelemetryMetrics.Humidity => HumidityBand(reading.Value),
            _ => new ReadingDecision(ReadingStatuses.Rejected, "Unknown metric.")
        };
    }

    private static ReadingDecision UpperBand(double value, double warning, double critical, string metric, string unit)
    {
        if (value >= critical)
        {
            return new ReadingDecision(ReadingStatuses.Critical, $"{metric} {Format(value, unit)} is at or above {Format(critical, unit)}.");
        }

        if (value >= warning)
        {
            return new ReadingDecision(ReadingStatuses.Warning, $"{metric} {Format(value, unit)} is at or above {Format(warning, unit)}.");
        }

        return new ReadingDecision(ReadingStatuses.Normal, $"{metric} {Format(value, unit)} is within the normal range.");
    }

    private static ReadingDecision HumidityBand(double value)
    {
        if (value < 20 || value > 85)
        {
            return new ReadingDecision(ReadingStatuses.Critical, $"Humidity {value:0.#}% is outside 20% to 85%.");
        }

        if (value < 30 || value > 70)
        {
            return new ReadingDecision(ReadingStatuses.Warning, $"Humidity {value:0.#}% is outside the preferred 30% to 70%.");
        }

        return new ReadingDecision(ReadingStatuses.Normal, $"Humidity {value:0.#}% is within the normal range.");
    }

    private static string Format(double value, string unit) =>
        string.IsNullOrEmpty(unit) ? value.ToString("0.#") : $"{value:0.#}{unit}";
}
