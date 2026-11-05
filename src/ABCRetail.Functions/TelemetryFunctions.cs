using System.Text.Json;
using ABCRetail.Functions.Infrastructure;
using ABCRetail.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetail.Functions;

public class TelemetryFunctions
{
    private readonly TelemetryPipeline _pipeline;
    private readonly ILogger<TelemetryFunctions> _logger;

    public TelemetryFunctions(TelemetryPipeline pipeline, ILogger<TelemetryFunctions> logger)
    {
        _pipeline = pipeline;
        _logger = logger;
    }

    [Function(nameof(IngestTelemetry))]
    public async Task<IActionResult> IngestTelemetry(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "telemetry")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        TelemetryReading? reading;
        try
        {
            reading = await JsonSerializer.DeserializeAsync<TelemetryReading>(request.Body, RetailJson.Options, cancellationToken);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(new { message = "The body is not a valid reading." });
        }

        if (reading is not null && reading.Id == Guid.Empty)
        {
            reading.Id = Guid.NewGuid();
        }

        if (reading is not null && reading.RecordedAtUtc == default)
        {
            reading.RecordedAtUtc = DateTime.UtcNow;
        }

        var error = ReadingAnalyser.Validate(reading);
        if (error is not null)
        {
            return new BadRequestObjectResult(new { message = error });
        }

        await _pipeline.EnqueueAsync(reading!, cancellationToken);
        return new OkObjectResult(new { message = "Reading queued.", id = reading!.Id });
    }

    [Function(nameof(ProcessTelemetry))]
    public async Task ProcessTelemetry(
        [QueueTrigger("%TelemetryQueueName%", Connection = "AzureWebJobsStorage")] string body,
        CancellationToken cancellationToken)
    {
        TelemetryReading? reading;
        try
        {
            reading = JsonSerializer.Deserialize<TelemetryReading>(body, RetailJson.Options);
        }
        catch (JsonException)
        {
            _logger.LogWarning("A telemetry message was not valid JSON and was skipped.");
            return;
        }

        var error = ReadingAnalyser.Validate(reading);
        var decision = error is null
            ? ReadingAnalyser.Analyse(reading!)
            : new ReadingDecision(ReadingStatuses.Rejected, error);
        var source = reading ?? new TelemetryReading();
        var processed = new ProcessedReading
        {
            Id = source.Id == Guid.Empty ? Guid.NewGuid() : source.Id,
            DeviceId = string.IsNullOrWhiteSpace(source.DeviceId) ? "unknown" : source.DeviceId.Trim(),
            Metric = string.IsNullOrWhiteSpace(source.Metric) ? "unknown" : source.Metric.Trim(),
            Value = double.IsFinite(source.Value) ? source.Value : 0,
            Status = decision.Status,
            Reason = decision.Reason,
            RecordedAtUtc = source.RecordedAtUtc == default ? DateTime.UtcNow : source.RecordedAtUtc,
            ProcessedAtUtc = DateTime.UtcNow
        };

        await _pipeline.SaveAsync(processed, cancellationToken);
        _logger.LogInformation("Stored {Status} reading {Id} for {DeviceId}.", processed.Status, processed.Id, processed.DeviceId);
    }
}
