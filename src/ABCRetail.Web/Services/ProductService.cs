using ABCRetail.Shared;
using ABCRetail.Web.Infrastructure;
using ABCRetail.Web.Models;
using Azure.Data.Tables;
using Azure.Storage.Blobs.Models;

namespace ABCRetail.Web.Services;

public interface IProductService
{
    Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken);

    Task<Product?> GetAsync(string rowKey, CancellationToken cancellationToken);

    Task AddAsync(Product product, ImageUpload? image, CancellationToken cancellationToken);

    Task UpdateAsync(Product product, ImageUpload? image, CancellationToken cancellationToken);

    Task DeleteAsync(string rowKey, CancellationToken cancellationToken);

    Task<StoredObject?> OpenImageAsync(string rowKey, CancellationToken cancellationToken);
}

public sealed class ProductService : IProductService
{
    private readonly AzureStorageContext _storage;

    public ProductService(AzureStorageContext storage)
    {
        _storage = storage;
    }

    public async Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken)
    {
        var products = new List<Product>();
        await foreach (var product in Table().QueryAsync<Product>(
            item => item.PartitionKey == StorageResourceNames.ProductPartition,
            cancellationToken: cancellationToken))
        {
            products.Add(product);
        }

        products.Sort((left, right) => string.Compare(left.ProductName, right.ProductName, StringComparison.OrdinalIgnoreCase));
        return products;
    }

    public async Task<Product?> GetAsync(string rowKey, CancellationToken cancellationToken)
    {
        var response = await Table().GetEntityIfExistsAsync<Product>(
            StorageResourceNames.ProductPartition,
            rowKey,
            cancellationToken: cancellationToken);

        return response.HasValue ? response.Value : null;
    }

    public async Task AddAsync(Product product, ImageUpload? image, CancellationToken cancellationToken)
    {
        product.PartitionKey = StorageResourceNames.ProductPartition;
        if (string.IsNullOrWhiteSpace(product.RowKey))
        {
            product.RowKey = Guid.NewGuid().ToString("N");
        }

        if (image is not null)
        {
            await SaveImageAsync(product, image, cancellationToken);
        }

        await Table().AddEntityAsync(product, cancellationToken);
    }

    public async Task UpdateAsync(Product product, ImageUpload? image, CancellationToken cancellationToken)
    {
        var existing = await GetAsync(product.RowKey, cancellationToken)
            ?? throw new InvalidOperationException("Product was not found.");

        product.PartitionKey = StorageResourceNames.ProductPartition;
        product.ImageBlobName = existing.ImageBlobName;
        product.ImageContentType = existing.ImageContentType;

        if (image is not null)
        {
            await SaveImageAsync(product, image, cancellationToken);
            if (!string.IsNullOrWhiteSpace(existing.ImageBlobName)
                && !string.Equals(existing.ImageBlobName, product.ImageBlobName, StringComparison.Ordinal))
            {
                await DeleteBlobAsync(existing.ImageBlobName, cancellationToken);
            }
        }

        await Table().UpdateEntityAsync(product, Azure.ETag.All, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task DeleteAsync(string rowKey, CancellationToken cancellationToken)
    {
        var existing = await GetAsync(rowKey, cancellationToken);
        if (existing is null)
        {
            return;
        }

        await Table().DeleteEntityAsync(StorageResourceNames.ProductPartition, rowKey, Azure.ETag.All, cancellationToken);

        if (!string.IsNullOrWhiteSpace(existing.ImageBlobName))
        {
            await DeleteBlobAsync(existing.ImageBlobName, cancellationToken);
        }
    }

    public async Task<StoredObject?> OpenImageAsync(string rowKey, CancellationToken cancellationToken)
    {
        var product = await GetAsync(rowKey, cancellationToken);
        if (product is null || string.IsNullOrWhiteSpace(product.ImageBlobName))
        {
            return null;
        }

        var blob = Container().GetBlobClient(product.ImageBlobName);
        if (!await blob.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var download = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        var contentType = product.ImageContentType
            ?? download.Value.Details.ContentType
            ?? "application/octet-stream";

        return new StoredObject(download.Value.Content, contentType);
    }

    private async Task SaveImageAsync(Product product, ImageUpload image, CancellationToken cancellationToken)
    {
        var error = ImageUploadRules.Validate(image.ContentType, image.Length);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        if (!ImageUploadRules.TryGetExtension(image.ContentType, out var extension))
        {
            throw new InvalidOperationException("Upload a JPG, PNG, or WEBP image.");
        }

        var blobName = $"product-{Guid.NewGuid():N}{extension}";
        var blob = Container().GetBlobClient(blobName);
        await blob.UploadAsync(
            image.Content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = image.ContentType }
            },
            cancellationToken);

        product.ImageBlobName = blobName;
        product.ImageContentType = image.ContentType;
    }

    private async Task DeleteBlobAsync(string blobName, CancellationToken cancellationToken)
    {
        await Container().GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private TableClient Table() => _storage.GetTable(_storage.Options.ProductTableName);

    private Azure.Storage.Blobs.BlobContainerClient Container() =>
        _storage.GetContainer(_storage.Options.ImageContainerName);
}
