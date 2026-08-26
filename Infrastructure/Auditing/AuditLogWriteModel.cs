namespace CustomerManagement.Api.Infrastructure.Auditing;

public sealed record AuditLogWriteModel(
    DateTime? OccurredAtUtc,
    Guid? ActorUserId,
    string? ActorEmail,
    string ActionType,
    string EntityName,
    string EntityId,
    string Result,
    object? Metadata = null);
