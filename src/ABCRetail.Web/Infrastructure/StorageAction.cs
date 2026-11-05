using Azure;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Web.Infrastructure;

public static class StorageAction
{
    private static bool IsStorageFailure(Exception exception) =>
        exception is RequestFailedException
        || exception.InnerException is RequestFailedException
        || exception is AggregateException aggregate && aggregate.InnerExceptions.Any(item => item is RequestFailedException);
    public static async Task<IActionResult> RunAsync(
        Controller controller,
        ILogger logger,
        Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            logger.LogError(ex, "Azure Storage could not be reached.");
            var actionName = controller.ControllerContext.RouteData.Values["action"]?.ToString();
            if (string.Equals(actionName, "Index", StringComparison.OrdinalIgnoreCase))
            {
                return controller.View("StorageUnavailable");
            }

            controller.TempData["Error"] = "Azure Storage could not complete that request. Confirm Azurite is running, or that the storage account connection string is valid.";
            return controller.RedirectToAction("Index");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "A retail operation was rejected.");
            controller.TempData["Error"] = ex.Message;
            return controller.RedirectToAction("Index");
        }
    }
}
