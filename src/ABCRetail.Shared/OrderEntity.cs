using Azure;
using Azure.Data.Tables;

namespace ABCRetail.Shared;

public class OrderEntity : ITableEntity
{
    public string PartitionKey { get; set; } = StorageResourceNames.OrderPartition;

    public string RowKey { get; set; } = string.Empty;

    public string CustomerId { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public double UnitPrice { get; set; }

    public double TotalAmount { get; set; }

    public string Status { get; set; } = OrderStatuses.Queued;

    public string Note { get; set; } = string.Empty;

    public bool StockApplied { get; set; }

    public DateTime SubmittedAtUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }
}
