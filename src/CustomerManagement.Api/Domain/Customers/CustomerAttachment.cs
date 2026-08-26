namespace CustomerManagement.Api.Domain.Customers;

public sealed class CustomerAttachment
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public Customer Customer { get; set; } = null!;
}
