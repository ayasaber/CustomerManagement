namespace CustomerManagement.Api.Contracts.Customers;

public sealed record CustomerAttachmentResponse(
    Guid Id,
    Guid CustomerId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string StorageKey,
    string CreatedBy,
    DateTime CreatedAtUtc);
