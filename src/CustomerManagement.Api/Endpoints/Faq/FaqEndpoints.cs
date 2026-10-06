using System.Security.Claims;
using CustomerManagement.Api.Contracts.Faq;
using CustomerManagement.Api.Domain.Faq;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Faq;

public static class FaqEndpoints
{
    public static IEndpointRouteBuilder MapFaqEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/faq").WithTags("Faq");

        group.MapGet("", ListAsync)
            .RequireAuthorization("Permission:" + Permissions.FaqRead)
            .WithName("ListFaqEntries")
            .WithSummary("List FAQ entries");

        group.MapPost("", CreateAsync)
            .RequireAuthorization("Permission:" + Permissions.FaqManage)
            .WithName("CreateFaqEntry")
            .WithSummary("Create an FAQ entry");

        group.MapPut("/{faqId:guid}", UpdateAsync)
            .RequireAuthorization("Permission:" + Permissions.FaqManage)
            .WithName("UpdateFaqEntry")
            .WithSummary("Update an FAQ entry");

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

        var query = dbContext.FaqEntries.AsNoTracking().AsQueryable();
        if (effectiveActiveOnly)
        {
            query = query.Where(entry => entry.IsActive);
        }

        var entries = await query
            .OrderBy(entry => entry.Topic)
            .ThenBy(entry => entry.SortOrder)
            .ThenBy(entry => entry.Question)
            .Select(entry => new FaqEntryResponse(
                entry.Id,
                entry.Topic,
                entry.Question,
                entry.Answer,
                entry.SortOrder,
                entry.IsActive,
                entry.CreatedAtUtc,
                entry.UpdatedAtUtc,
                entry.RowVersion))
            .ToListAsync(cancellationToken);

        return Results.Ok(entries);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateFaqEntryRequest request,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var now = DateTime.UtcNow;
        var entry = new FaqEntry
        {
            Id = Guid.NewGuid(),
            Topic = request.Topic.Trim(),
            Question = request.Question.Trim(),
            Answer = request.Answer.Trim(),
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.FaqEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/faq/{entry.Id}",
            new FaqEntryResponse(
                entry.Id,
                entry.Topic,
                entry.Question,
                entry.Answer,
                entry.SortOrder,
                entry.IsActive,
                entry.CreatedAtUtc,
                entry.UpdatedAtUtc,
                entry.RowVersion));
    }

    private static async Task<IResult> UpdateAsync(
        Guid faqId,
        [FromBody] UpdateFaqEntryRequest request,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateUpdateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var entry = await dbContext.FaqEntries
            .FirstOrDefaultAsync(row => row.Id == faqId, cancellationToken);

        if (entry is null)
        {
            return Results.NotFound();
        }

        entry.Topic = request.Topic.Trim();
        entry.Question = request.Question.Trim();
        entry.Answer = request.Answer.Trim();
        entry.SortOrder = request.SortOrder;
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
                Detail = "FAQ entry was updated by another request. Refresh and retry with the latest RowVersion.",
                Status = StatusCodes.Status409Conflict
            });
        }

        return Results.Ok(new FaqEntryResponse(
            entry.Id,
            entry.Topic,
            entry.Question,
            entry.Answer,
            entry.SortOrder,
            entry.IsActive,
            entry.CreatedAtUtc,
            entry.UpdatedAtUtc,
            entry.RowVersion));
    }

    private static Dictionary<string, string[]> ValidateCreateRequest(CreateFaqEntryRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Topic))
        {
            errors["topic"] = ["Topic is required."];
        }
        else if (request.Topic.Trim().Length > 150)
        {
            errors["topic"] = ["Topic must be 150 characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            errors["question"] = ["Question is required."];
        }
        else if (request.Question.Trim().Length > 500)
        {
            errors["question"] = ["Question must be 500 characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(request.Answer))
        {
            errors["answer"] = ["Answer is required."];
        }
        else if (request.Answer.Trim().Length > 4000)
        {
            errors["answer"] = ["Answer must be 4000 characters or fewer."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateUpdateRequest(UpdateFaqEntryRequest request)
    {
        var errors = ValidateCreateRequest(new CreateFaqEntryRequest(request.Topic, request.Question, request.Answer, request.SortOrder));

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
