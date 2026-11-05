using ABCRetail.Shared;
using ABCRetail.Web.Infrastructure;
using ABCRetail.Web.Models;
using ABCRetail.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ABCRetail.Web.Controllers;

public class InventoryController : Controller
{
    private readonly AzureStorageContext _storage;
    private readonly IInventoryService _inventory;
    private readonly IProductService _products;
    private readonly IFileShareStore _files;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        AzureStorageContext storage,
        IInventoryService inventory,
        IProductService products,
        IFileShareStore files,
        ILogger<InventoryController> logger)
    {
        _storage = storage;
        _inventory = inventory;
        _products = products;
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

            ViewBag.Products = await _products.ListAsync(cancellationToken);
            var logs = await _files.ListAsync(_storage.Options.LogShareName, cancellationToken);
            ViewBag.Logs = logs.Where(file => file.Name.StartsWith("inventory-", StringComparison.OrdinalIgnoreCase)).Take(8).ToList();
            return View(new InventoryForm());
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Index(InventoryForm form, CancellationToken cancellationToken) =>
        StorageAction.RunAsync(this, _logger, async () =>
        {
            if (!InventoryOperations.IsKnown(form.Operation))
            {
                ModelState.AddModelError(nameof(form.Operation), "Operation must be Restock or Reduce.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Products = await _products.ListAsync(cancellationToken);
                ViewBag.Logs = Array.Empty<StoredFile>();
                return View(form);
            }

            var transactionId = await _inventory.SubmitAsync(
                form.ProductId!,
                form.Operation,
                form.Quantity,
                form.Reason,
                cancellationToken);

            TempData["Message"] = $"Inventory transaction {transactionId} was written to Azure Queue Storage. Stock changes when the function app processes it.";
            return RedirectToAction(nameof(Index));
        });
}
