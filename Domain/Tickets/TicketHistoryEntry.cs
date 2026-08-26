namespace CustomerManagement.Api.Domain.Tickets;

public sealed class TicketHistoryEntry
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string FieldName { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public Guid? ActorUserId { get; set; }

    public string? ActorEmail { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public Ticket Ticket { get; set; } = null!;
}
