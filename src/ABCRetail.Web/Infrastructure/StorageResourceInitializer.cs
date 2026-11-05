using ABCRetail.Shared;
using Azure.Storage.Blobs.Models;

namespace ABCRetail.Web.Infrastructure;

public sealed class StorageResourceInitializer
{
    private readonly AzureStorageContext _storage;
    private readonly ILogger<StorageResourceInitializer> _logger;

    public StorageResourceInitializer(AzureStorageContext storage, ILogger<StorageResourceInitializer> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public async Task EnsureAsync(CancellationToken cancellationToken)
    {
        if (!_storage.IsConfigured)
        {
            _logger.LogWarning("Azure Storage connection string is empty. Storage features stay unavailable until it is set.");
            return;
        }

        var options = _storage.Options;

        await _storage.TableService!.CreateTableIfNotExistsAsync(options.CustomerTableName, cancellationToken);
        await _storage.TableService.CreateTableIfNotExistsAsync(options.ProductTableName, cancellationToken);
        await _storage.TableService.CreateTableIfNotExistsAsync(options.OrderTableName, cancellationToken);
        await _storage.GetContainer(options.ImageContainerName).CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        await _storage.GetQueue(options.OrderQueueName).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await _storage.GetQueue(options.InventoryQueueName).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await _storage.GetQueue(options.TelemetryQueueName).CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        if (_storage.UsesLocalFiles)
        {
            Directory.CreateDirectory(LocalFileShare.GetDirectory(options.ContractShareName));
            Directory.CreateDirectory(LocalFileShare.GetDirectory(options.LogShareName));
        }
        else
        {
            await _storage.GetShare(options.ContractShareName).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            await _storage.GetShare(options.LogShareName).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }

        _logger.LogInformation("Azure Storage resources are ready.");
    }
}

public sealed class StorageStartupInitializer : IHostedService
{
    private readonly StorageResourceInitializer _initializer;
    private readonly ILogger<StorageStartupInitializer> _logger;

    public StorageStartupInitializer(StorageResourceInitializer initializer, ILogger<StorageStartupInitializer> logger)
    {
        _initializer = initializer;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _initializer.EnsureAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Storage resources could not be created. Start Azurite or check the connection string.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
