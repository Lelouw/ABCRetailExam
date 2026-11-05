using System.ComponentModel.DataAnnotations;
using Azure;
using Azure.Data.Tables;

namespace ABCRetail.Shared;

public class Product : ITableEntity
{
    public string PartitionKey { get; set; } = StorageResourceNames.ProductPartition;

    public string RowKey { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    [Display(Name = "Product name")]
    [StringLength(80)]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    [StringLength(400)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(40)]
    public string Category { get; set; } = string.Empty;

    [Range(0.01, 1_000_000)]
    public double Price { get; set; }

    [Display(Name = "Stock quantity")]
    [Range(0, 1_000_000)]
    public int StockQuantity { get; set; }

    public string? ImageBlobName { get; set; }

    public string? ImageContentType { get; set; }

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }
}
