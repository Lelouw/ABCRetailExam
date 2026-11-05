using ABCRetail.Shared;
using ABCRetail.Web.Infrastructure;
using ABCRetail.Web.Models;
using ABCRetail.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Web.Controllers;

public class ProductsController : Controller
{
    private readonly AzureStorageContext _storage;
    private readonly IProductService _products;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(AzureStorageContext storage, IProductService products, ILogger<ProductsController> logger)
    {
        _storage = storage;
        _products = products;
        _logger = logger;
    }

    public Task<IActionResult> Index(CancellationToken cancellationToken) =>
        StorageAction.RunAsync(this, _logger, async () =>
        {
            if (!_storage.IsConfigured)
            {
                return View("NotConfigured");
            }

            return View(await _products.ListAsync(cancellationToken));
        });

    public IActionResult Create() => _storage.IsConfigured ? View(new ProductForm()) : View("NotConfigured");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create(ProductForm form, CancellationToken cancellationToken) =>
        StorageAction.RunAsync(this, _logger, async () =>
        {
            var imageError = ValidateImage(form.Image, required: true);
            if (imageError is not null)
            {
                ModelState.AddModelError(nameof(form.Image), imageError);
            }

            if (!ModelState.IsValid)
            {
                return View(form);
            }

            await _products.AddAsync(ToProduct(form), await OpenImageAsync(form.Image), cancellationToken);
            TempData["Message"] = "Product and image saved. The record is in Table Storage and the image is in Blob Storage.";
            return RedirectToAction(nameof(Index));
        });

    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        if (!_storage.IsConfigured)
        {
            return View("NotConfigured");
        }

        var product = await _products.GetAsync(id, cancellationToken);
        return product is null ? NotFound() : View(ToForm(product));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(string id, ProductForm form, CancellationToken cancellationToken) =>
        StorageAction.RunAsync(this, _logger, async () =>
        {
            if (!string.Equals(id, form.RowKey, StringComparison.Ordinal))
            {
                return BadRequest();
            }

            var imageError = ValidateImage(form.Image, required: false);
            if (imageError is not null)
            {
                ModelState.AddModelError(nameof(form.Image), imageError);
            }

            if (!ModelState.IsValid)
            {
                return View(form);
            }

            var product = ToProduct(form);
            product.RowKey = id;
            await _products.UpdateAsync(product, await OpenImageAsync(form.Image), cancellationToken);
            TempData["Message"] = "Product updated.";
            return RedirectToAction(nameof(Index));
        });

    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        if (!_storage.IsConfigured)
        {
            return View("NotConfigured");
        }

        var product = await _products.GetAsync(id, cancellationToken);
        return product is null ? NotFound() : View(product);
    }

    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        if (!_storage.IsConfigured)
        {
            return View("NotConfigured");
        }

        var product = await _products.GetAsync(id, cancellationToken);
        return product is null ? NotFound() : View(product);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteConfirmed(string id, CancellationToken cancellationToken) =>
        StorageAction.RunAsync(this, _logger, async () =>
        {
            await _products.DeleteAsync(id, cancellationToken);
            TempData["Message"] = "Product and its image were removed.";
            return RedirectToAction(nameof(Index));
        });

    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Image(string id, CancellationToken cancellationToken)
    {
        if (!_storage.IsConfigured)
        {
            return NotFound();
        }

        var image = await _products.OpenImageAsync(id, cancellationToken);
        return image is null ? NotFound() : File(image.Content, image.ContentType);
    }

    private static Product ToProduct(ProductForm form) => new()
    {
        RowKey = form.RowKey ?? string.Empty,
        ProductName = form.ProductName.Trim(),
        Description = form.Description.Trim(),
        Category = form.Category.Trim(),
        Price = form.Price,
        StockQuantity = form.StockQuantity
    };

    private static ProductForm ToForm(Product product) => new()
    {
        RowKey = product.RowKey,
        ProductName = product.ProductName,
        Description = product.Description,
        Category = product.Category,
        Price = product.Price,
        StockQuantity = product.StockQuantity,
        HasImage = !string.IsNullOrWhiteSpace(product.ImageBlobName)
    };

    private static string? ValidateImage(IFormFile? image, bool required)
    {
        if (image is null || image.Length == 0)
        {
            return required ? "Choose an image to upload." : null;
        }

        return ImageUploadRules.Validate(image.ContentType, image.Length);
    }

    private static async Task<ImageUpload?> OpenImageAsync(IFormFile? image)
    {
        if (image is null || image.Length == 0)
        {
            return null;
        }

        var buffer = new MemoryStream();
        await image.CopyToAsync(buffer);
        buffer.Position = 0;
        return new ImageUpload(buffer, image.ContentType, image.Length);
    }
}
