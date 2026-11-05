using ABCRetail.Shared;
using ABCRetail.Web.Infrastructure;
using ABCRetail.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Web.Controllers;

public class CustomersController : Controller
{
    private readonly AzureStorageContext _storage;
    private readonly ICustomerStore _customers;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(AzureStorageContext storage, ICustomerStore customers, ILogger<CustomersController> logger)
    {
        _storage = storage;
        _customers = customers;
        _logger = logger;
    }

    public Task<IActionResult> Index(CancellationToken cancellationToken) =>
        StorageAction.RunAsync(this, _logger, async () =>
        {
            if (!_storage.IsConfigured)
            {
                return View("NotConfigured");
            }

            return View(await _customers.ListAsync(cancellationToken));
        });

    public IActionResult Create() => _storage.IsConfigured ? View(new Customer()) : View("NotConfigured");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create(
        [Bind(nameof(Customer.FirstName), nameof(Customer.LastName), nameof(Customer.Email), nameof(Customer.PhoneNumber), nameof(Customer.Address))] Customer customer,
        CancellationToken cancellationToken) =>
        StorageAction.RunAsync(this, _logger, async () =>
        {
            if (!ModelState.IsValid)
            {
                return View(customer);
            }

            await _customers.AddAsync(customer, cancellationToken);
            TempData["Message"] = "Customer saved in Azure Table Storage.";
            return RedirectToAction(nameof(Index));
        });

    public Task<IActionResult> Details(string id, CancellationToken cancellationToken) =>
        FindAsync(id, cancellationToken, customer => View(customer));

    public Task<IActionResult> Edit(string id, CancellationToken cancellationToken) =>
        FindAsync(id, cancellationToken, customer => View(customer));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(
        string id,
        [Bind(nameof(Customer.RowKey), nameof(Customer.FirstName), nameof(Customer.LastName), nameof(Customer.Email), nameof(Customer.PhoneNumber), nameof(Customer.Address))] Customer customer,
        CancellationToken cancellationToken) =>
        StorageAction.RunAsync(this, _logger, async () =>
        {
            if (!string.Equals(id, customer.RowKey, StringComparison.Ordinal))
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(customer);
            }

            await _customers.UpdateAsync(customer, cancellationToken);
            TempData["Message"] = "Customer updated in Azure Table Storage.";
            return RedirectToAction(nameof(Index));
        });

    public Task<IActionResult> Delete(string id, CancellationToken cancellationToken) =>
        FindAsync(id, cancellationToken, customer => View(customer));

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteConfirmed(string id, CancellationToken cancellationToken) =>
        StorageAction.RunAsync(this, _logger, async () =>
        {
            await _customers.DeleteAsync(id, cancellationToken);
            TempData["Message"] = "Customer removed from Azure Table Storage.";
            return RedirectToAction(nameof(Index));
        });

    private async Task<IActionResult> FindAsync(string id, CancellationToken cancellationToken, Func<Customer, IActionResult> onFound)
    {
        if (!_storage.IsConfigured)
        {
            return View("NotConfigured");
        }

        var customer = await _customers.GetAsync(id, cancellationToken);
        return customer is null ? NotFound() : onFound(customer);
    }
}
