using System.ComponentModel.DataAnnotations;

namespace CustomerManagement.Api.Domain.Tickets;

public sealed class TicketCategory
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public List<Ticket> Tickets { get; set; } = [];
}
