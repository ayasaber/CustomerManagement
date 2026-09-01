namespace CustomerManagement.Api.Contracts.Dashboard;

public sealed record DashboardAssignedTicketItemResponse(
    Guid TicketId,
    string TicketNumber,
    string Subject,
    string Status,
    string Priority,
    string Category,
    bool IsEscalated,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid CustomerId,
    string CustomerDisplayName);

public sealed record DashboardAssignedTicketsResponse(
    int TotalCount,
    IReadOnlyList<DashboardAssignedTicketItemResponse> Items);

public sealed record DashboardOpenTaskSummaryItemResponse(
    Guid TaskId,
    Guid TicketId,
    string TicketNumber,
    string Description,
    DateTime DueAtUtc,
    DateTime CreatedAtUtc);

public sealed record DashboardOpenTaskSummaryResponse(
    int TotalCount,
    IReadOnlyList<DashboardOpenTaskSummaryItemResponse> Items);

public sealed record DashboardCustomerContextResponse(
    Guid TicketId,
    Guid CustomerId,
    string CustomerDisplayName,
    string? Company,
    string? PrimaryEmail,
    string? PrimaryPhone,
    IReadOnlyList<DashboardCustomerInteractionItemResponse> RecentInteractions);

public sealed record DashboardCustomerInteractionItemResponse(
    Guid InteractionId,
    string Type,
    string Summary,
    DateTime OccurredAtUtc);
