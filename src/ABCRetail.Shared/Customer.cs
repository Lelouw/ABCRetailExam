using System.ComponentModel.DataAnnotations;
using Azure;
using Azure.Data.Tables;

namespace ABCRetail.Shared;

public class Customer : ITableEntity
{
    public string PartitionKey { get; set; } = StorageResourceNames.CustomerPartition;

    public string RowKey { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    [Display(Name = "First name")]
    [StringLength(60)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Surname")]
    [StringLength(60)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(120)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [Display(Name = "Phone number")]
    [StringLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Address { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
