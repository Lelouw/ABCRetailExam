using ABCRetail.Shared;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;

namespace ABCRetail.Web.Infrastructure;

public sealed class AzureStorageContext
{
    public AzureStorageContext(IOptions<AzureStorageOptions> options)
    {
        Options = options.Value;
        if (string.IsNullOrWhiteSpace(Options.ConnectionString))
        {
            return;
        }

        TableService = new TableServiceClient(Options.ConnectionString, TableOptions());
        BlobService = new BlobServiceClient(Options.ConnectionString, BlobOptions());
        QueueService = new QueueServiceClient(Options.ConnectionString, QueueOptions());

        // Azurite does not implement Azure Files, and the file SDK rejects the emulator connection string.
        UsesLocalFiles = LocalFileShare.IsEmulator(Options.ConnectionString);
        if (!UsesLocalFiles)
        {
            ShareService = new ShareServiceClient(Options.ConnectionString);
        }
    }

    public AzureStorageOptions Options { get; }

    public bool IsConfigured => TableService is not null;

    public TableServiceClient? TableService { get; }

    public BlobServiceClient? BlobService { get; }

    public QueueServiceClient? QueueService { get; }

    public ShareServiceClient? ShareService { get; }

    public bool UsesLocalFiles { get; }

    public TableClient GetTable(string tableName)
    {
        EnsureConfigured();
        return TableService!.GetTableClient(tableName);
    }

    public BlobContainerClient GetContainer(string containerName)
    {
        EnsureConfigured();
        return BlobService!.GetBlobContainerClient(containerName);
    }

    public QueueClient GetQueue(string queueName)
    {
        EnsureConfigured();
        return QueueService!.GetQueueClient(queueName);
    }

    public ShareClient GetShare(string shareName)
    {
        if (UsesLocalFiles || ShareService is null)
        {
            throw new InvalidOperationException("Azure Files is not available for the local storage emulator.");
        }

        return ShareService.GetShareClient(shareName);
    }

    private static TableClientOptions TableOptions()
    {
        var options = new TableClientOptions();
        ApplyShortRetry(options.Retry);
        return options;
    }

    private static BlobClientOptions BlobOptions()
    {
        var options = new BlobClientOptions();
        ApplyShortRetry(options.Retry);
        return options;
    }

    private static QueueClientOptions QueueOptions()
    {
        var options = new QueueClientOptions();
        ApplyShortRetry(options.Retry);
        return options;
    }

    private static void ApplyShortRetry(Azure.Core.RetryOptions retry)
    {
        retry.MaxRetries = 2;
        retry.Delay = TimeSpan.FromMilliseconds(200);
        retry.MaxDelay = TimeSpan.FromSeconds(1);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Azure Storage is not configured.");
        }
    }
}
