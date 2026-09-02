using CustomerManagement.Api.Domain.Security;
using CustomerManagement.Api.Domain.Tickets;

namespace CustomerManagement.Api.Domain.Dashboard;

public sealed class TicketHandoffRequest
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }

    public Guid NoteId { get; set; }

    public Guid RequestedByUserId { get; set; }

    public Guid TargetAssigneeUserId { get; set; }

    public TicketHandoffRequestStatus Status { get; set; } = TicketHandoffRequestStatus.Pending;

    public string? Message { get; set; }

    public string? ResponseMessage { get; set; }

    public DateTime RequestedAtUtc { get; set; }

    public DateTime? RespondedAtUtc { get; set; }

    public Ticket Ticket { get; set; } = null!;

    public TicketInternalNote Note { get; set; } = null!;

    public ApplicationUser RequestedByUser { get; set; } = null!;

    public ApplicationUser TargetAssigneeUser { get; set; } = null!;
}
