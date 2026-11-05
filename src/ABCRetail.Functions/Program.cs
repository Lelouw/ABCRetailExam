using ABCRetail.Functions.Infrastructure;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.Services.AddSingleton<StorageGateway>();
builder.Services.AddSingleton<TelemetryPipeline>();

builder.Build().Run();
