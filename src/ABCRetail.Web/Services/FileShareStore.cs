using ABCRetail.Shared;
using ABCRetail.Web.Infrastructure;
using ABCRetail.Web.Models;

namespace ABCRetail.Web.Services;

public interface IFileShareStore
{
    Task UploadAsync(string shareName, string fileName, Stream content, CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredFile>> ListAsync(string shareName, CancellationToken cancellationToken);

    Task<StoredObject?> DownloadAsync(string shareName, string fileName, CancellationToken cancellationToken);

    Task DeleteAsync(string shareName, string fileName, CancellationToken cancellationToken);
}

public sealed class FileShareStore : IFileShareStore
{
    private readonly AzureStorageContext _storage;

    public FileShareStore(AzureStorageContext storage)
    {
        _storage = storage;
    }

    public async Task UploadAsync(string shareName, string fileName, Stream content, CancellationToken cancellationToken)
    {
        if (_storage.UsesLocalFiles)
        {
            var path = LocalFileShare.GetPath(shareName, fileName);
            await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await content.CopyToAsync(output, cancellationToken);
            return;
        }

        var file = await GetFileAsync(shareName, fileName, cancellationToken);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        await file.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        await file.CreateAsync(bytes.Length, cancellationToken: cancellationToken);
        await file.UploadRangeAsync(new Azure.HttpRange(0, bytes.Length), new MemoryStream(bytes), cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<StoredFile>> ListAsync(string shareName, CancellationToken cancellationToken)
    {
        if (_storage.UsesLocalFiles)
        {
            var directory = LocalFileShare.GetDirectory(shareName);
            if (!Directory.Exists(directory))
            {
                return [];
            }

            return Directory.GetFiles(directory)
                .Select(path => new StoredFile(Path.GetFileName(path), new FileInfo(path).Length))
                .OrderByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var share = _storage.GetShare(shareName);
        if (!await share.ExistsAsync(cancellationToken))
        {
            return [];
        }

        var files = new List<StoredFile>();
        await foreach (var item in share.GetRootDirectoryClient().GetFilesAndDirectoriesAsync(cancellationToken: cancellationToken))
        {
            if (!item.IsDirectory)
            {
                files.Add(new StoredFile(item.Name, item.FileSize));
            }
        }

        files.Sort((left, right) => string.Compare(right.Name, left.Name, StringComparison.OrdinalIgnoreCase));
        return files;
    }

    public async Task<StoredObject?> DownloadAsync(string shareName, string fileName, CancellationToken cancellationToken)
    {
        if (_storage.UsesLocalFiles)
        {
            var path = LocalFileShare.GetPath(shareName, fileName);
            if (!File.Exists(path))
            {
                return null;
            }

            return new StoredObject(File.OpenRead(path), "application/octet-stream");
        }

        var file = await GetFileAsync(shareName, fileName, cancellationToken);
        if (!await file.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var download = await file.DownloadAsync(cancellationToken: cancellationToken);
        return new StoredObject(download.Value.Content, "application/octet-stream");
    }

    public async Task DeleteAsync(string shareName, string fileName, CancellationToken cancellationToken)
    {
        if (_storage.UsesLocalFiles)
        {
            var path = LocalFileShare.GetPath(shareName, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return;
        }

        var file = await GetFileAsync(shareName, fileName, cancellationToken);
        await file.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private async Task<Azure.Storage.Files.Shares.ShareFileClient> GetFileAsync(
        string shareName,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (!ABCRetail.Shared.FileNameSanitizer.IsSafeFileName(fileName))
        {
            throw new InvalidOperationException("That file name is not allowed.");
        }

        var share = _storage.GetShare(shareName);
        await share.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return share.GetRootDirectoryClient().GetFileClient(fileName);
    }
}
