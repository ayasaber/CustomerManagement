namespace CustomerManagement.Api.Infrastructure.Attachments;

public interface IAttachmentStorage
{
    Task<StoredAttachment> SaveAsync(
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<AttachmentContent> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}

public sealed record StoredAttachment(
    string StorageKey,
    long SizeBytes,
    string OriginalFileName,
    string ContentType);

public sealed record AttachmentContent(
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long SizeBytes);
