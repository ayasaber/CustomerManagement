namespace CustomerManagement.Api.Infrastructure.Auditing;

public interface IAuditLogWriter
{
    Task WriteAsync(AuditLogWriteModel entry, CancellationToken cancellationToken);
}
