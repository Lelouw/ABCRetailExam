using System.ComponentModel.DataAnnotations;
using ABCRetail.Shared;

namespace ABCRetail.Web.Models;

public sealed record ImageUpload(Stream Content, string ContentType, long Length);

public sealed record StoredFile(string Name, long? Size);

public sealed record StoredObject(Stream Content, string ContentType);

public sealed class HomeDashboard
{
    public bool StorageConfigured { get; set; }

    public string? StorageError { get; set; }

    public int CustomerCount { get; set; }

    public int ProductCount { get; set; }

    public int QueuedOrderCount { get; set; }

    public int ContractCount { get; set; }
}

public sealed class OrderForm
{
    [Required(ErrorMessage = "Select a customer.")]
    [Display(Name = "Customer")]
    public string? CustomerId { get; set; }

    [Required(ErrorMessage = "Select a product.")]
    [Display(Name = "Product")]
    public string? ProductId { get; set; }

    [Range(1, 10000)]
    public int Quantity { get; set; } = 1;
}

public sealed class InventoryForm
{
    [Required(ErrorMessage = "Select a product.")]
    [Display(Name = "Product")]
    public string? ProductId { get; set; }

    [Required]
    public string Operation { get; set; } = InventoryOperations.Restock;

    [Range(1, 10000)]
    public int Quantity { get; set; } = 1;

    [StringLength(200)]
    public string? Reason { get; set; }
}

public sealed class ProductForm
{
    public string? RowKey { get; set; }

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

    [Display(Name = "Product image")]
    public IFormFile? Image { get; set; }

    public bool HasImage { get; set; }
}
