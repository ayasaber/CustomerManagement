using CustomerManagement.Api.Domain.Security;

namespace CustomerManagement.Api.Domain.Dashboard;

public sealed class TicketInternalNoteMention
{
    public Guid Id { get; set; }

    public Guid TicketInternalNoteId { get; set; }

    public Guid MentionedUserId { get; set; }

    public DateTime MentionedAtUtc { get; set; }

    public bool NotificationDelivered { get; set; }

    public DateTime? NotificationDeliveredAtUtc { get; set; }

    public TicketInternalNote TicketInternalNote { get; set; } = null!;

    public ApplicationUser MentionedUser { get; set; } = null!;
}
