namespace CustomerManagement.Api.Contracts.TicketTasks;

public sealed record CreateTicketTaskRequest(
    Guid TicketId,
    string Description,
    DateTime DueAtUtc,
    Guid? AssignedToUserId);

public sealed record UpdateTicketTaskRequest(
    string Description,
    DateTime DueAtUtc,
    Guid AssignedToUserId,
    byte[] RowVersion);

public sealed record CompleteTicketTaskRequest(byte[] RowVersion);

public sealed record TicketTaskResponse(
    Guid Id,
    Guid TicketId,
    string TicketNumber,
    Guid CreatedByUserId,
    Guid AssignedToUserId,
    string AssignedToDisplayName,
    string Description,
    DateTime DueAtUtc,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? CompletedAtUtc,
    Guid? CompletedByUserId,
    byte[] RowVersion);

public sealed record TicketTaskListResponse(
    int TotalCount,
    IReadOnlyList<TicketTaskResponse> Items);
