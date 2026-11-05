using System.Globalization;
using ABCRetail.Shared;
using ABCRetail.Web.Infrastructure;
using ABCRetail.Web.Services;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

var culture = new CultureInfo("en-ZA");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

builder.Services.AddControllersWithViews();
builder.Services.Configure<AzureStorageOptions>(builder.Configuration.GetSection(AzureStorageOptions.SectionName));
builder.Services.AddSingleton<AzureStorageContext>();
builder.Services.AddSingleton<StorageResourceInitializer>();
builder.Services.AddHostedService<StorageStartupInitializer>();
builder.Services.AddSingleton<ICustomerStore, CustomerStore>();
builder.Services.AddSingleton<IProductService, ProductService>();
builder.Services.AddSingleton<IOrderService, OrderService>();
builder.Services.AddSingleton<IInventoryService, InventoryService>();
builder.Services.AddSingleton<IFileShareStore, FileShareStore>();
builder.Services.AddSingleton(sp =>
    new TelemetryStore(sp.GetRequiredService<IConfiguration>().GetConnectionString("TelemetryDb") ?? string.Empty));
builder.Services.AddSingleton<TelemetryIngestService>();

var app = builder.Build();

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = [culture],
    SupportedUICultures = [culture]
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
