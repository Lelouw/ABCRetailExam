using ABCRetail.Shared;
using ABCRetail.Web.Infrastructure;
using ABCRetail.Web.Models;
using ABCRetail.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Web.Controllers;

public class HomeController : Controller
{
    private readonly AzureStorageContext _storage;
    private readonly ICustomerStore _customers;
    private readonly IProductService _products;
    private readonly IOrderService _orders;
    private readonly IFileShareStore _files;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        AzureStorageContext storage,
        ICustomerStore customers,
        IProductService products,
        IOrderService orders,
        IFileShareStore files,
        ILogger<HomeController> logger)
    {
        _storage = storage;
        _customers = customers;
        _products = products;
        _orders = orders;
        _files = files;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new HomeDashboard
        {
            StorageConfigured = _storage.IsConfigured
        };

        if (!_storage.IsConfigured)
        {
            return View(model);
        }

        try
        {
            model.CustomerCount = (await _customers.ListAsync(cancellationToken)).Count;
            model.ProductCount = (await _products.ListAsync(cancellationToken)).Count;
            model.QueuedOrderCount = (await _orders.ListAsync(cancellationToken))
                .Count(order => order.Status == OrderStatuses.Queued);
            model.ContractCount = (await _files.ListAsync(_storage.Options.ContractShareName, cancellationToken)).Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The home page could not read Azure Storage.");
            model.StorageError = "The app could not reach Azure Storage. Start Azurite, or check the connection string.";
        }

        return View(model);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
