using System.ComponentModel.DataAnnotations;
using CustomerManagement.Api.Domain.Security;
using CustomerManagement.Api.Domain.Tickets;

namespace CustomerManagement.Api.Domain.Dashboard;

public sealed class TicketTask
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public Guid AssignedToUserId { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTime DueAtUtc { get; set; }

    public TicketTaskStatus Status { get; set; } = TicketTaskStatus.Open;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public Guid? CompletedByUserId { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public Ticket Ticket { get; set; } = null!;

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public ApplicationUser AssignedToUser { get; set; } = null!;

    public ApplicationUser? CompletedByUser { get; set; }
}
