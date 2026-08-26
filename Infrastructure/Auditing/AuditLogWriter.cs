using System.Text.Json;
using CustomerManagement.Api.Domain.Security;
using CustomerManagement.Api.Infrastructure.Persistence;

namespace CustomerManagement.Api.Infrastructure.Auditing;

public sealed class AuditLogWriter(
    CustomerManagementDbContext dbContext,
    ILogger<AuditLogWriter> logger) : IAuditLogWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task WriteAsync(AuditLogWriteModel entry, CancellationToken cancellationToken)
    {
        try
        {
            var occurredAtUtc = NormalizeUtc(entry.OccurredAtUtc ?? DateTime.UtcNow);
            var metadataJson = SerializeMetadata(entry.Metadata);

            var row = new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = occurredAtUtc,
                ActorUserId = entry.ActorUserId,
                ActorEmail = TruncateAndNormalize(entry.ActorEmail, 320),
                ActionType = TruncateAndNormalizeRequired(entry.ActionType, 200, "unknown.action"),
                EntityName = TruncateAndNormalizeRequired(entry.EntityName, 200, "unknown.entity"),
                EntityId = TruncateAndNormalizeRequired(entry.EntityId, 128, "unknown"),
                Result = TruncateAndNormalizeRequired(entry.Result, 50, "Unknown"),
                MetadataJson = metadataJson
            };

            dbContext.AuditLogEntries.Add(row);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Audit failures must never break primary operations.
            logger.LogError(exception, "Failed to persist audit log entry for action {ActionType}", entry.ActionType);
        }
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string? SerializeMetadata(object? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(metadata, SerializerOptions);
        if (json.Length <= 4000)
        {
            return json;
        }

        const string marker = "...[TRUNCATED]";
        return json[..(4000 - marker.Length)] + marker;
    }

    private static string TruncateAndNormalizeRequired(string? value, int maxLength, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? TruncateAndNormalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
