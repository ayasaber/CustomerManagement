using System.ComponentModel.DataAnnotations;
using CustomerManagement.Api.Domain.Customers;
using CustomerManagement.Api.Domain.Security;

namespace CustomerManagement.Api.Domain.Tickets;

public sealed class Ticket
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public Guid? AssignedToUserId { get; set; }

    public Guid CategoryId { get; set; }

    public Guid PriorityId { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public TicketStatus Status { get; set; } = TicketStatus.New;

    public bool IsEscalated { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public Customer Customer { get; set; } = null!;

    public ApplicationUser? AssignedToUser { get; set; }

    public TicketCategory Category { get; set; } = null!;

    public TicketPriority Priority { get; set; } = null!;

    public List<TicketHistoryEntry> HistoryEntries { get; set; } = [];
}
