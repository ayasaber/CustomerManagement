using Microsoft.Extensions.Options;

namespace CustomerManagement.Api.Infrastructure.Attachments;

public sealed class LocalFileSystemAttachmentStorage : IAttachmentStorage
{
    private readonly string _rootPath;

    public LocalFileSystemAttachmentStorage(
        IOptions<AttachmentStorageOptions> options,
        IWebHostEnvironment hostEnvironment)
    {
        var configuredRoot = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            throw new InvalidOperationException("Attachment storage root path is not configured.");
        }

        _rootPath = Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, configuredRoot));

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredAttachment> SaveAsync(
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        var safeFileName = string.IsNullOrWhiteSpace(originalFileName)
            ? "file"
            : Path.GetFileName(originalFileName);
        var extension = Path.GetExtension(safeFileName);
        var datePrefix = DateTime.UtcNow.ToString("yyyy/MM/dd");
        var storageFileName = $"{Guid.NewGuid():N}{extension}";
        var storageKey = $"{datePrefix}/{storageFileName}";

        var absolutePath = GetAbsolutePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var destination = new FileStream(
            absolutePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true);

        await content.CopyToAsync(destination, cancellationToken);

        return new StoredAttachment(
            storageKey,
            destination.Length,
            safeFileName,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
    }

    public async Task<AttachmentContent> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var absolutePath = GetAbsolutePath(storageKey);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("Attachment not found.", storageKey);
        }

        var stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            useAsync: true);

        // Ensures stream is initialized for immediate consumers and honors cancellation requests.
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        return new AttachmentContent(
            stream,
            Path.GetFileName(absolutePath),
            "application/octet-stream",
            stream.Length);
    }

    public Task<bool> DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var absolutePath = GetAbsolutePath(storageKey);
        if (!File.Exists(absolutePath))
        {
            return Task.FromResult(false);
        }

        File.Delete(absolutePath);
        return Task.FromResult(true);
    }

    private string GetAbsolutePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("Storage key is required.", nameof(storageKey));
        }

        var normalizedKey = storageKey.Replace('\\', '/').TrimStart('/');
        var combinedPath = Path.GetFullPath(
            Path.Combine(_rootPath, normalizedKey.Replace('/', Path.DirectorySeparatorChar)));

        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!combinedPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(combinedPath, _rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage key path.");
        }

        return combinedPath;
    }
}
