namespace CustomerManagement.Api.Contracts.Admin.Audit;

public sealed record AuditLogItemResponse(
    Guid Id,
    DateTime OccurredAtUtc,
    Guid? ActorUserId,
    string? ActorEmail,
    string ActionType,
    string EntityName,
    string EntityId,
    string Result,
    string? MetadataJson);

public sealed record AuditLogResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<AuditLogItemResponse> Items);
