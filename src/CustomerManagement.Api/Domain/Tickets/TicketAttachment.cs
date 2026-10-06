using System.ComponentModel.DataAnnotations;

namespace CustomerManagement.Api.Domain.Tickets;

public sealed class TicketAttachment
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public Guid UploadedByUserId { get; set; }

    public string UploadedByDisplayName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public Ticket Ticket { get; set; } = null!;
}
