namespace CustomerManagement.Api.Contracts.Tickets;

public sealed record CreateTicketCategoryRequest(string Name, string? Description);

public sealed record UpdateTicketCategoryRequest(string Name, string? Description, bool IsActive, byte[] RowVersion);

public sealed record TicketCategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion);

public sealed record CreateTicketPriorityRequest(string Name, int SortOrder);

public sealed record UpdateTicketPriorityRequest(string Name, int SortOrder, bool IsActive, byte[] RowVersion);

public sealed record TicketPriorityResponse(
    Guid Id,
    string Name,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion);

public sealed record CreateTicketRequest(
    Guid? CustomerUserId,
    Guid CategoryId,
    Guid? PriorityId,
    string Subject,
    string Description);

public sealed record UpdateTicketCoreRequest(
    Guid CategoryId,
    Guid PriorityId,
    string Subject,
    string Description,
    byte[] RowVersion);

public sealed record UpdateTicketStatusRequest(string TargetStatus, byte[] RowVersion);

public sealed record AssignTicketRequest(Guid AssigneeUserId, byte[] RowVersion);

public sealed record SelfAssignTicketRequest(byte[] RowVersion);

public sealed record EscalateTicketRequest(string? Reason, byte[] RowVersion);

public sealed record ReopenTicketRequest(string? Reason, byte[] RowVersion);

public sealed record TicketResponse(
    Guid Id,
    Guid CustomerId,
    Guid? AssignedToUserId,
    string? AssignedToUserEmail,
    Guid CategoryId,
    string CategoryName,
    bool CategoryIsActive,
    Guid PriorityId,
    string PriorityName,
    bool PriorityIsActive,
    int PrioritySortOrder,
    string Subject,
    string Description,
    string Status,
    bool IsEscalated,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion);

public sealed record TicketListItemResponse(
    Guid Id,
    Guid CustomerId,
    Guid? AssignedToUserId,
    string? AssignedToUserEmail,
    Guid CategoryId,
    string CategoryName,
    Guid PriorityId,
    string PriorityName,
    string Subject,
    string Status,
    bool IsEscalated,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion);

public sealed record TicketListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<TicketListItemResponse> Items);

public sealed record TicketHistoryItemResponse(
    Guid Id,
    string ActionType,
    string FieldName,
    string? OldValue,
    string? NewValue,
    Guid? ActorUserId,
    string? ActorEmail,
    DateTime OccurredAtUtc);

public sealed record TicketHistoryResponse(Guid TicketId, IReadOnlyList<TicketHistoryItemResponse> Items);
