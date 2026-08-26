using CustomerManagement.Api.Contracts.Admin.Audit;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Admin;

public static class AuditLogEndpoints
{
    public static IEndpointRouteBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/audit-logs")
            .RequireAuthorization("Permission:" + Permissions.AuditRead)
            .WithTags("Admin.AuditLogs");

        group.MapGet("", QueryAuditLogsAsync)
            .WithName("QueryAuditLogs")
            .WithSummary("Query security and high-impact audit logs with filters and pagination");

        return app;
    }

    private static async Task<IResult> QueryAuditLogsAsync(
        [FromQuery] Guid? userId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? actionType,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var resolvedPage = page.GetValueOrDefault(1);
        var resolvedPageSize = pageSize.GetValueOrDefault(50);

        if (resolvedPage < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (resolvedPageSize is < 1 or > 200)
        {
            errors["pageSize"] = ["PageSize must be between 1 and 200."];
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
        {
            errors["dateRange"] = ["fromUtc must be less than or equal to toUtc."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var query = dbContext.AuditLogEntries.AsNoTracking().AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(entry => entry.ActorUserId == userId.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(entry => entry.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(entry => entry.OccurredAtUtc <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(actionType))
        {
            var normalizedActionType = actionType.Trim();
            query = query.Where(entry => entry.ActionType == normalizedActionType);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ThenByDescending(entry => entry.Id)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .Select(entry => new AuditLogItemResponse(
                entry.Id,
                entry.OccurredAtUtc,
                entry.ActorUserId,
                entry.ActorEmail,
                entry.ActionType,
                entry.EntityName,
                entry.EntityId,
                entry.Result,
                entry.MetadataJson))
            .ToListAsync(cancellationToken);

        return Results.Ok(new AuditLogResponse(resolvedPage, resolvedPageSize, totalCount, items));
    }
}
