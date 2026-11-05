using ABCRetail.Shared;
using ABCRetail.Web.Models;
using ABCRetail.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ABCRetail.Web.Controllers;

public class ReadingsController : Controller
{
    private readonly TelemetryStore _store;
    private readonly TelemetryIngestService _ingest;

    public ReadingsController(TelemetryStore store, TelemetryIngestService ingest)
    {
        _store = store;
        _ingest = ingest;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!_store.IsConfigured)
        {
            return View(new ReadingsPage { DatabaseConfigured = false });
        }

        try
        {
            await _store.EnsureSchemaAsync(cancellationToken);
            var readings = await _store.GetRecentAsync(50, cancellationToken);
            var summary = await _store.GetSummaryAsync(cancellationToken);
            return View(new ReadingsPage
            {
                DatabaseConfigured = true,
                Readings = readings,
                Summary = summary
            });
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            return View(new ReadingsPage
            {
                DatabaseConfigured = true,
                Error = "TelemetryDb could not be queried. Check the SQL firewall and the connection string."
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ingest(ReadingInput input, CancellationToken cancellationToken)
    {
        var reading = new TelemetryReading
        {
            Id = Guid.NewGuid(),
            DeviceId = input.DeviceId.Trim(),
            Metric = input.Metric,
            Value = input.Value,
            RecordedAtUtc = DateTime.UtcNow
        };
        var error = ReadingAnalyser.Validate(reading);
        if (error is not null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _ingest.EnqueueAsync(reading, cancellationToken);
            TempData["Message"] = "Reading queued. The function will analyse it and write the result to TelemetryDb.";
        }
        catch (InvalidOperationException)
        {
            TempData["Error"] = "Storage is not configured, so the reading could not be queued.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Simulate(int count, CancellationToken cancellationToken)
    {
        try
        {
            var sent = await _ingest.SimulateAsync(count == 0 ? 50 : count, cancellationToken);
            TempData["Message"] = $"{sent} readings were queued. Refresh after the function finishes to see them in TelemetryDb.";
        }
        catch (InvalidOperationException)
        {
            TempData["Error"] = "Storage is not configured, so the traffic burst could not be queued.";
        }

        return RedirectToAction(nameof(Index));
    }
}
