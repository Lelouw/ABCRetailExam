using ABCRetail.Shared;

namespace ABCRetail.Shared.Tests;

public class TelemetryRulesTests
{
    [Theory]
    [InlineData(22, ReadingStatuses.Normal)]
    [InlineData(35, ReadingStatuses.Warning)]
    [InlineData(46, ReadingStatuses.Critical)]
    public void Temperature_IsClassifiedByThreshold(double value, string expected)
    {
        var decision = ReadingAnalyser.Analyse(Reading(TelemetryMetrics.Temperature, value));

        Assert.Equal(expected, decision.Status);
    }

    [Fact]
    public void Humidity_OutsidePreferredRange_IsWarning()
    {
        var decision = ReadingAnalyser.Analyse(Reading(TelemetryMetrics.Humidity, 78));

        Assert.Equal(ReadingStatuses.Warning, decision.Status);
    }

    [Fact]
    public void Validate_RejectsABlankDevice()
    {
        var reading = Reading(TelemetryMetrics.CpuPercent, 10);
        reading.DeviceId = " ";

        Assert.Equal("Device id is required and must be 64 characters or fewer.", ReadingAnalyser.Validate(reading));
    }

    [Fact]
    public void Validate_RejectsHumidityAbove100()
    {
        var error = ReadingAnalyser.Validate(Reading(TelemetryMetrics.Humidity, 120));

        Assert.Equal("Humidity must be between 0 and 100.", error);
    }

    private static TelemetryReading Reading(string metric, double value) => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = "sensor-01",
        Metric = metric,
        Value = value,
        RecordedAtUtc = DateTime.UtcNow
    };
}
