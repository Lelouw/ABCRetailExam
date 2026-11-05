using ABCRetail.Web.Infrastructure;
using ABCRetail.Web.Models;
using ABCRetail.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ABCRetail.Web.Controllers;

public class OrdersController : Controller
{
    private readonly AzureStorageContext _storage;
    private readonly IOrderService _orders;
    private readonly ICustomerStore _customers;
    private readonly IProductService _products;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        AzureStorageContext storage,
        IOrderService orders,
        ICustomerStore customers,
        IProductService products,
        ILogger<OrdersController> logger)
    {
        _storage = storage;
        _orders = orders;
        _customers = customers;
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

            return View(await _orders.ListAsync(cancellationToken));
        });

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (!_storage.IsConfigured)
        {
            return View("NotConfigured");
        }

        await PopulateListsAsync(cancellationToken);
        return View(new OrderForm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create(OrderForm form, CancellationToken cancellationToken) =>
        StorageAction.RunAsync(this, _logger, async () =>
        {
            if (!ModelState.IsValid)
            {
                await PopulateListsAsync(cancellationToken);
                return View(form);
            }

            var order = await _orders.SubmitAsync(form.CustomerId!, form.ProductId!, form.Quantity, cancellationToken);
            TempData["Message"] = $"Order {order.RowKey} was written to Azure Queue Storage. It stays Queued until the function app processes it.";
            return RedirectToAction(nameof(Index));
        });

    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        if (!_storage.IsConfigured)
        {
            return View("NotConfigured");
        }

        var order = await _orders.GetAsync(id, cancellationToken);
        return order is null ? NotFound() : View(order);
    }

    private async Task PopulateListsAsync(CancellationToken cancellationToken)
    {
        var customers = await _customers.ListAsync(cancellationToken);
        var products = await _products.ListAsync(cancellationToken);
        ViewBag.Customers = new SelectList(customers, "RowKey", "FullName");
        ViewBag.Products = new SelectList(
            products.Select(product => new { product.RowKey, Label = $"{product.ProductName} ({product.StockQuantity} in stock)" }),
            "RowKey",
            "Label");
    }
}
