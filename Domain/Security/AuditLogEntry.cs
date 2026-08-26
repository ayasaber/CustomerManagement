namespace CustomerManagement.Api.Domain.Security;

public sealed class AuditLogEntry
{
    public Guid Id { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public Guid? ActorUserId { get; set; }

    public string? ActorEmail { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string Result { get; set; } = string.Empty;

    public string? MetadataJson { get; set; }
}
