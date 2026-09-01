using System.Security.Claims;
using CustomerManagement.Api.Contracts.QuickReplies;
using CustomerManagement.Api.Domain.Dashboard;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Dashboard;

public static class QuickRepliesEndpoints
{
    public static IEndpointRouteBuilder MapQuickRepliesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quick-replies").WithTags("QuickReplies");

        group.MapGet("", ListAsync)
            .RequireAuthorization("Permission:" + Permissions.QuickRepliesRead)
            .WithName("ListQuickReplies")
            .WithSummary("List shared quick replies");

        group.MapPost("", CreateAsync)
            .RequireAuthorization("Permission:" + Permissions.QuickRepliesManage)
            .WithName("CreateQuickReply")
            .WithSummary("Create a shared quick reply");

        group.MapPut("/{quickReplyId:guid}", UpdateAsync)
            .RequireAuthorization("Permission:" + Permissions.QuickRepliesManage)
            .WithName("UpdateQuickReply")
            .WithSummary("Update or deactivate a shared quick reply");

        return app;
    }

    private static async Task<IResult> ListAsync(
        bool? activeOnly,
        string? search,
        string? tag,
        int? page,
        int? pageSize,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var resolvedPage = page.GetValueOrDefault(1);
        var resolvedPageSize = pageSize.GetValueOrDefault(20);
        var errors = new Dictionary<string, string[]>();

        if (resolvedPage < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (resolvedPageSize < 1 || resolvedPageSize > 200)
        {
            errors["pageSize"] = ["PageSize must be between 1 and 200."];
        }

        var normalizedTag = string.IsNullOrWhiteSpace(tag) ? null : NormalizeTag(tag);
        if (tag is not null && normalizedTag is null)
        {
            errors["tag"] = ["Tag is invalid."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var isAdmin = IsAdmin(httpContext.User);
        var includeInactive = isAdmin && activeOnly.HasValue && !activeOnly.Value;

        var query = dbContext.QuickReplies
            .AsNoTracking()
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(reply => reply.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(reply =>
                reply.Title.ToLower().Contains(term)
                || reply.Body.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(normalizedTag))
        {
            var token = $",{normalizedTag},";
            query = query.Where(reply => reply.TagsCsv != null && reply.TagsCsv.Contains(token));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rawItems = await query
            .OrderBy(reply => reply.Title)
            .ThenBy(reply => reply.Id)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .Select(reply => new
            {
                reply.Id,
                reply.Title,
                reply.Body,
                reply.TagsCsv,
                reply.IsActive,
                reply.CreatedByUserId,
                reply.UpdatedByUserId,
                reply.CreatedAtUtc,
                reply.UpdatedAtUtc,
                reply.RowVersion
            })
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(reply => new QuickReplyResponse(
                reply.Id,
                reply.Title,
                reply.Body,
                ParseTags(reply.TagsCsv),
                reply.IsActive,
                reply.CreatedByUserId,
                reply.UpdatedByUserId,
                reply.CreatedAtUtc,
                reply.UpdatedAtUtc,
                reply.RowVersion))
            .ToList();

        return Results.Ok(new QuickReplyListResponse(totalCount, items));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateQuickReplyRequest request,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actorUserId = ResolveActorUserId(httpContext.User);
        if (!actorUserId.HasValue)
        {
            return Results.Forbid();
        }

        var normalizedTags = NormalizeTags(request.Tags, out var tagsError);
        if (tagsError is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tags"] = [tagsError]
            });
        }

        var errors = ValidateCreateOrUpdate(request.Title, request.Body);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var title = request.Title.Trim();
        var body = request.Body.Trim();

        var titleExists = await dbContext.QuickReplies
            .AsNoTracking()
            .AnyAsync(reply => reply.Title.ToLower() == title.ToLower(), cancellationToken);

        if (titleExists)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = "Quick reply title already exists.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var now = DateTime.UtcNow;
        var reply = new QuickReply
        {
            Id = Guid.NewGuid(),
            Title = title,
            Body = body,
            TagsCsv = ToStoredTagsCsv(normalizedTags),
            IsActive = true,
            CreatedByUserId = actorUserId.Value,
            UpdatedByUserId = actorUserId.Value,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.QuickReplies.Add(reply);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/quick-replies/{reply.Id}", new QuickReplyResponse(
            reply.Id,
            reply.Title,
            reply.Body,
            normalizedTags,
            reply.IsActive,
            reply.CreatedByUserId,
            reply.UpdatedByUserId,
            reply.CreatedAtUtc,
            reply.UpdatedAtUtc,
            reply.RowVersion));
    }

    private static async Task<IResult> UpdateAsync(
        Guid quickReplyId,
        [FromBody] UpdateQuickReplyRequest request,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.RowVersion is null || request.RowVersion.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["RowVersion is required."]
            });
        }

        var actorUserId = ResolveActorUserId(httpContext.User);
        if (!actorUserId.HasValue)
        {
            return Results.Forbid();
        }

        var normalizedTags = NormalizeTags(request.Tags, out var tagsError);
        if (tagsError is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tags"] = [tagsError]
            });
        }

        var errors = ValidateCreateOrUpdate(request.Title, request.Body);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var reply = await dbContext.QuickReplies.FirstOrDefaultAsync(row => row.Id == quickReplyId, cancellationToken);
        if (reply is null)
        {
            return Results.NotFound();
        }

        var title = request.Title.Trim();
        var body = request.Body.Trim();

        var titleConflict = await dbContext.QuickReplies
            .AsNoTracking()
            .AnyAsync(row => row.Id != quickReplyId && row.Title.ToLower() == title.ToLower(), cancellationToken);

        if (titleConflict)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = "Quick reply title already exists.",
                Status = StatusCodes.Status409Conflict
            });
        }

        dbContext.Entry(reply).Property(row => row.RowVersion).OriginalValue = request.RowVersion;

        reply.Title = title;
        reply.Body = body;
        reply.TagsCsv = ToStoredTagsCsv(normalizedTags);
        reply.IsActive = request.IsActive;
        reply.UpdatedByUserId = actorUserId.Value;
        reply.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "Quick reply was updated by another request. Refresh and retry with the latest RowVersion.",
                Status = StatusCodes.Status409Conflict
            });
        }

        return Results.Ok(new QuickReplyResponse(
            reply.Id,
            reply.Title,
            reply.Body,
            normalizedTags,
            reply.IsActive,
            reply.CreatedByUserId,
            reply.UpdatedByUserId,
            reply.CreatedAtUtc,
            reply.UpdatedAtUtc,
            reply.RowVersion));
    }

    private static Dictionary<string, string[]> ValidateCreateOrUpdate(string title, string body)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(title))
        {
            errors["title"] = ["Title is required."];
        }
        else if (title.Trim().Length > 120)
        {
            errors["title"] = ["Title must be 120 characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            errors["body"] = ["Body is required."];
        }
        else if (body.Trim().Length > 4000)
        {
            errors["body"] = ["Body must be 4000 characters or fewer."];
        }

        return errors;
    }

    private static List<string> NormalizeTags(IReadOnlyList<string>? tags, out string? error)
    {
        error = null;

        if (tags is null || tags.Count == 0)
        {
            return [];
        }

        if (tags.Count > 20)
        {
            error = "tags_limit_exceeded";
            return [];
        }

        var normalized = new List<string>(tags.Count);
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            var cleanTag = NormalizeTag(tag);
            if (cleanTag is null)
            {
                error = "tags_limit_exceeded";
                return [];
            }

            normalized.Add(cleanTag);
        }

        var unique = normalized
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unique.Count > 20)
        {
            error = "tags_limit_exceeded";
            return [];
        }

        return unique;
    }

    private static string? NormalizeTag(string tag)
    {
        var normalized = tag.Trim().ToLowerInvariant();
        if (normalized.Length == 0 || normalized.Length > 50)
        {
            return null;
        }

        return normalized;
    }

    private static string? ToStoredTagsCsv(IReadOnlyList<string> tags)
    {
        if (tags.Count == 0)
        {
            return null;
        }

        return "," + string.Join(',', tags) + ",";
    }

    private static IReadOnlyList<string> ParseTags(string? tagsCsv)
    {
        if (string.IsNullOrWhiteSpace(tagsCsv))
        {
            return [];
        }

        return tagsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Guid? ResolveActorUserId(ClaimsPrincipal actor)
    {
        var candidate = actor.FindFirstValue(ClaimTypes.NameIdentifier) ?? actor.FindFirstValue("sub");
        return Guid.TryParse(candidate, out var parsed) ? parsed : null;
    }

    private static bool IsAdmin(ClaimsPrincipal actor)
    {
        return actor.IsInRole(AuthRoles.Admin)
            || actor.Claims.Any(claim =>
                string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                && string.Equals(claim.Value, AuthRoles.Admin, StringComparison.OrdinalIgnoreCase));
    }
}
