namespace ABCRetail.Shared;

public static class StorageResourceNames
{
    public const string CustomersTable = "Customers";
    public const string ProductsTable = "Products";
    public const string OrdersTable = "Orders";
    public const string ImageContainer = "product-images";
    public const string OrderQueue = "order-processing";
    public const string InventoryQueue = "inventory-processing";
    public const string TelemetryQueue = "telemetry-ingest";
    public const string ContractShare = "contracts";
    public const string LogShare = "logs";

    public const string CustomerPartition = "Customer";
    public const string ProductPartition = "Product";
    public const string OrderPartition = "Order";
}

public static class OrderStatuses
{
    public const string Queued = "Queued";
    public const string Processed = "Processed";
    public const string Rejected = "Rejected";
}
