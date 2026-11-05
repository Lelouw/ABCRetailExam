using Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ABCRetail.Functions.Infrastructure;

internal static class FunctionResponses
{
    public static async Task<IActionResult> RunAsync(ILogger logger, Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("The request body is not valid JSON.");
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(ex.Message);
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Storage rejected a function request.");
            return new ObjectResult("Azure Storage could not complete the request.")
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The function failed.");
            return new ObjectResult("The request could not be completed.")
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
