using ABCRetail.Shared;
using ABCRetail.Web.Infrastructure;
using ABCRetail.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Web.Controllers;

public class ContractsController : Controller
{
    private readonly AzureStorageContext _storage;
    private readonly IFileShareStore _files;
    private readonly ILogger<ContractsController> _logger;

    public ContractsController(AzureStorageContext storage, IFileShareStore files, ILogger<ContractsController> logger)
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

            return View(await _files.ListAsync(_storage.Options.ContractShareName, cancellationToken));
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Upload(IFormFile? contract, CancellationToken cancellationToken) =>
        StorageAction.RunAsync(this, _logger, async () =>
        {
            var error = ContractUploadRules.Validate(contract?.FileName, contract?.Length ?? 0);
            if (error is not null)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Index));
            }

            var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{FileNameSanitizer.Sanitize(contract!.FileName)}";
            await using var stream = contract.OpenReadStream();
            await _files.UploadAsync(_storage.Options.ContractShareName, fileName, stream, cancellationToken);
            TempData["Message"] = "Contract uploaded to Azure Files.";
            return RedirectToAction(nameof(Index));
        });

    public async Task<IActionResult> Download(string name, CancellationToken cancellationToken)
    {
        if (!_storage.IsConfigured || !FileNameSanitizer.IsSafeFileName(name))
        {
            return BadRequest();
        }

        var file = await _files.DownloadAsync(_storage.Options.ContractShareName, name, cancellationToken);
        return file is null ? NotFound() : File(file.Content, "application/octet-stream", name);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Delete(string name, CancellationToken cancellationToken) =>
        StorageAction.RunAsync(this, _logger, async () =>
        {
            if (!FileNameSanitizer.IsSafeFileName(name))
            {
                return BadRequest();
            }

            await _files.DeleteAsync(_storage.Options.ContractShareName, name, cancellationToken);
            TempData["Message"] = "Contract removed from Azure Files.";
            return RedirectToAction(nameof(Index));
        });
}
