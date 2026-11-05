using ABCRetail.Shared;

namespace ABCRetail.Web.Infrastructure;

public sealed class AzureStorageOptions
{
    public const string SectionName = "AzureStorage";

    public string ConnectionString { get; set; } = string.Empty;

    public string CustomerTableName { get; set; } = StorageResourceNames.CustomersTable;

    public string ProductTableName { get; set; } = StorageResourceNames.ProductsTable;

    public string OrderTableName { get; set; } = StorageResourceNames.OrdersTable;

    public string ImageContainerName { get; set; } = StorageResourceNames.ImageContainer;

    public string OrderQueueName { get; set; } = StorageResourceNames.OrderQueue;

    public string InventoryQueueName { get; set; } = StorageResourceNames.InventoryQueue;

    public string TelemetryQueueName { get; set; } = StorageResourceNames.TelemetryQueue;

    public string ContractShareName { get; set; } = StorageResourceNames.ContractShare;

    public string LogShareName { get; set; } = StorageResourceNames.LogShare;
}
