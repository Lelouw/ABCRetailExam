using ABCRetail.Shared;
using ABCRetail.Web.Infrastructure;
using ABCRetail.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Web.Controllers;

public class LogsController : Controller
{
    private readonly AzureStorageContext _storage;
    private readonly IFileShareStore _files;
    private readonly ILogger<LogsController> _logger;

    public LogsController(AzureStorageContext storage, IFileShareStore files, ILogger<LogsController> logger)
    {
        _storage = storage;
        _files = files;
        _logger = logger;
    }

    public Task<IActionResult> Index(CancellationToken cancellationToken) =>
        StorageAction.RunAsync(this, _logger, async () =>
        {
            if (!_storage.IsConfigured)
            {
                return View("NotConfigured");
            }

            return View(await _files.ListAsync(_storage.Options.LogShareName, cancellationToken));
        });

    public async Task<IActionResult> Download(string name, CancellationToken cancellationToken)
    {
        if (!_storage.IsConfigured || !FileNameSanitizer.IsSafeFileName(name))
        {
            return BadRequest();
        }

        var file = await _files.DownloadAsync(_storage.Options.LogShareName, name, cancellationToken);
        return file is null ? NotFound() : File(file.Content, "text/plain", name);
    }
}
