using System.Text.Json;
using ABCRetail.Functions.Infrastructure;
using ABCRetail.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetail.Functions;

public class HttpFunctions
{
    private readonly StorageGateway _storage;
    private readonly ILogger<HttpFunctions> _logger;

    public HttpFunctions(StorageGateway storage, ILogger<HttpFunctions> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    [Function(nameof(Health))]
    public IActionResult Health([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest request)
    {
        return new OkObjectResult(new
        {
            status = "Healthy",
            service = "ABC Retail Functions"
        });
    }

    [Function(nameof(SaveCustomer))]
    public Task<IActionResult> SaveCustomer(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "customers")] HttpRequest request,
        CancellationToken cancellationToken) =>
        FunctionResponses.RunAsync(_logger, async () =>
        {
            var customer = await JsonSerializer.DeserializeAsync<CustomerWrite>(request.Body, RetailJson.Options, cancellationToken);
            var id = await _storage.SaveCustomerAsync(customer ?? new CustomerWrite(), cancellationToken);
            return new OkObjectResult(new { message = "Customer saved.", customerId = id });
        });

    [Function(nameof(ProductMedia))]
    public Task<IActionResult> ProductMedia(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "product-images")] HttpRequest request,
        CancellationToken cancellationToken) =>
        FunctionResponses.RunAsync(_logger, async () =>
        {
            if (!request.HasFormContentType)
            {
                return new BadRequestObjectResult("Upload the image as form data with the field name image.");
            }

            var form = await request.ReadFormAsync(cancellationToken);
            var image = form.Files.GetFile("image");
            if (image is null)
            {
                return new BadRequestObjectResult("No product image was provided.");
            }

            await using var stream = image.OpenReadStream();
            var blobName = await _storage.UploadImageAsync(stream, image.ContentType, image.Length, cancellationToken);
            return new OkObjectResult(new { message = "Product image uploaded.", blobName });
        });

    [Function(nameof(QueueOrder))]
    public Task<IActionResult> QueueOrder(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] HttpRequest request,
        CancellationToken cancellationToken) =>
        FunctionResponses.RunAsync(_logger, async () =>
        {
            var order = await JsonSerializer.DeserializeAsync<OrderMessage>(request.Body, RetailJson.Options, cancellationToken);
            var orderId = await _storage.EnqueueOrderAsync(order ?? new OrderMessage(), cancellationToken);
            return new OkObjectResult(new { message = "Order submitted for processing.", orderId });
        });

    [Function(nameof(QueueInventory))]
    public Task<IActionResult> QueueInventory(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "inventory")] HttpRequest request,
        CancellationToken cancellationToken) =>
        FunctionResponses.RunAsync(_logger, async () =>
        {
            var inventory = await JsonSerializer.DeserializeAsync<InventoryMessage>(request.Body, RetailJson.Options, cancellationToken);
            var transactionId = await _storage.EnqueueInventoryAsync(inventory ?? new InventoryMessage(), cancellationToken);
            return new OkObjectResult(new { message = "Inventory transaction submitted.", transactionId });
        });

    [Function(nameof(SaveActivityLog))]
    public Task<IActionResult> SaveActivityLog(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "activity-logs")] HttpRequest request,
        CancellationToken cancellationToken) =>
        FunctionResponses.RunAsync(_logger, async () =>
        {
            var log = await JsonSerializer.DeserializeAsync<ActivityLogWrite>(request.Body, RetailJson.Options, cancellationToken);
            var fileName = await _storage.SaveActivityLogAsync(log ?? new ActivityLogWrite(), cancellationToken);
            return new OkObjectResult(new { message = "Activity log saved.", fileName });
        });
}
