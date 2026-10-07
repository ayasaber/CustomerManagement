using System.Security.Claims;
using CustomerManagement.Api.Contracts.KnowledgeBase;
using CustomerManagement.Api.Domain.KnowledgeBase;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.KnowledgeBase;

public static class HelpArticleEndpoints
{
    public static IEndpointRouteBuilder MapHelpArticleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/help-articles").WithTags("HelpArticles");

        group.MapGet("", ListAsync)
            .RequireAuthorization("Permission:" + Permissions.HelpArticlesRead)
            .WithName("ListHelpArticles")
            .WithSummary("List help articles");

        group.MapPost("", CreateAsync)
            .RequireAuthorization("Permission:" + Permissions.HelpArticlesManage)
            .WithName("CreateHelpArticle")
            .WithSummary("Create a help article");

        group.MapPut("/{articleId:guid}", UpdateAsync)
            .RequireAuthorization("Permission:" + Permissions.HelpArticlesManage)
            .WithName("UpdateHelpArticle")
            .WithSummary("Update a help article");

        return app;
    }

    private static async Task<IResult> ListAsync(
        [FromQuery] bool? activeOnly,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Customers can never see retired entries, even by guessing the query parameter.
        var effectiveActiveOnly = IsCustomer(httpContext.User) || activeOnly.GetValueOrDefault();

        var query = dbContext.HelpArticles.AsNoTracking().AsQueryable();
        if (effectiveActiveOnly)
        {
            query = query.Where(entry => entry.IsActive);
        }

        var entries = await query
            .OrderBy(entry => entry.Title)
            .Select(entry => new HelpArticleResponse(
                entry.Id,
                entry.Title,
                entry.Body,
                entry.IsActive,
                entry.CreatedAtUtc,
                entry.UpdatedAtUtc,
                entry.RowVersion))
            .ToListAsync(cancellationToken);

        return Results.Ok(entries);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateHelpArticleRequest request,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var now = DateTime.UtcNow;
        var entry = new HelpArticle
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.HelpArticles.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/help-articles/{entry.Id}",
            new HelpArticleResponse(
                entry.Id,
                entry.Title,
                entry.Body,
                entry.IsActive,
                entry.CreatedAtUtc,
                entry.UpdatedAtUtc,
                entry.RowVersion));
    }

    private static async Task<IResult> UpdateAsync(
        Guid articleId,
        [FromBody] UpdateHelpArticleRequest request,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateUpdateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var entry = await dbContext.HelpArticles
            .FirstOrDefaultAsync(row => row.Id == articleId, cancellationToken);

        if (entry is null)
        {
            return Results.NotFound();
        }

        entry.Title = request.Title.Trim();
        entry.Body = request.Body.Trim();
        entry.IsActive = request.IsActive;
        entry.UpdatedAtUtc = DateTime.UtcNow;
        dbContext.Entry(entry).Property(row => row.RowVersion).OriginalValue = request.RowVersion;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "Help article was updated by another request. Refresh and retry with the latest RowVersion.",
                Status = StatusCodes.Status409Conflict
            });
        }

        return Results.Ok(new HelpArticleResponse(
            entry.Id,
            entry.Title,
            entry.Body,
            entry.IsActive,
            entry.CreatedAtUtc,
            entry.UpdatedAtUtc,
            entry.RowVersion));
    }

    private static Dictionary<string, string[]> ValidateCreateRequest(CreateHelpArticleRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors["title"] = ["Title is required."];
        }
        else if (request.Title.Trim().Length > 200)
        {
            errors["title"] = ["Title must be 200 characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            errors["body"] = ["Body is required."];
        }
        else if (request.Body.Trim().Length > 10000)
        {
            errors["body"] = ["Body must be 10000 characters or fewer."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateUpdateRequest(UpdateHelpArticleRequest request)
    {
        var errors = ValidateCreateRequest(new CreateHelpArticleRequest(request.Title, request.Body));

        if (request.RowVersion is null || request.RowVersion.Length == 0)
        {
            errors["rowVersion"] = ["RowVersion is required."];
        }

        return errors;
    }

    private static bool IsCustomer(ClaimsPrincipal actor)
    {
        return actor.IsInRole(AuthRoles.Customer)
            || actor.Claims.Any(claim =>
                string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                && string.Equals(claim.Value, AuthRoles.Customer, StringComparison.OrdinalIgnoreCase));
    }
}
