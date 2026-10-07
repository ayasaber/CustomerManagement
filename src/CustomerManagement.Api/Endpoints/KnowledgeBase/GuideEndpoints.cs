using System.Security.Claims;
using CustomerManagement.Api.Contracts.KnowledgeBase;
using CustomerManagement.Api.Domain.KnowledgeBase;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.KnowledgeBase;

public static class GuideEndpoints
{
    public static IEndpointRouteBuilder MapGuideEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/guides").WithTags("Guides");

        group.MapGet("", ListAsync)
            .RequireAuthorization("Permission:" + Permissions.GuidesRead)
            .WithName("ListGuides")
            .WithSummary("List solutions and guides");

        group.MapPost("", CreateAsync)
            .RequireAuthorization("Permission:" + Permissions.GuidesManage)
            .WithName("CreateGuide")
            .WithSummary("Create a solution/guide");

        group.MapPut("/{guideId:guid}", UpdateAsync)
            .RequireAuthorization("Permission:" + Permissions.GuidesManage)
            .WithName("UpdateGuide")
            .WithSummary("Update a solution/guide. Replaces the full step list on every update.");

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

        var query = dbContext.Guides.Include(g => g.Steps).AsNoTracking().AsQueryable();
        if (effectiveActiveOnly)
        {
            query = query.Where(entry => entry.IsActive);
        }

        var entries = await query
            .OrderBy(entry => entry.Title)
            .ToListAsync(cancellationToken);

        return Results.Ok(entries.Select(ToResponse));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateGuideRequest request,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var now = DateTime.UtcNow;
        var entry = new Guide
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Steps = BuildSteps(request.Steps)
        };

        dbContext.Guides.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/guides/{entry.Id}", ToResponse(entry));
    }

    private static async Task<IResult> UpdateAsync(
        Guid guideId,
        [FromBody] UpdateGuideRequest request,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateUpdateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var entry = await dbContext.Guides
            .FirstOrDefaultAsync(row => row.Id == guideId, cancellationToken);

        if (entry is null)
        {
            return Results.NotFound();
        }

        entry.Title = request.Title.Trim();
        entry.IsActive = request.IsActive;
        entry.UpdatedAtUtc = DateTime.UtcNow;
        dbContext.Entry(entry).Property(row => row.RowVersion).OriginalValue = request.RowVersion;

        // Replace the full step list rather than diffing individual steps - a guide is a single
        // ordered list owned entirely by one edit, so an omitted step is an intentional removal.
        var existingSteps = await dbContext.GuideSteps
            .Where(step => step.GuideId == guideId)
            .ToListAsync(cancellationToken);
        dbContext.GuideSteps.RemoveRange(existingSteps);

        var newSteps = BuildSteps(request.Steps);
        foreach (var step in newSteps)
        {
            step.GuideId = guideId;
        }

        dbContext.GuideSteps.AddRange(newSteps);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "Guide was updated by another request. Refresh and retry with the latest RowVersion.",
                Status = StatusCodes.Status409Conflict
            });
        }

        entry.Steps = newSteps;
        return Results.Ok(ToResponse(entry));
    }

    private static List<GuideStep> BuildSteps(IReadOnlyList<string> steps)
    {
        return steps
            .Select((instruction, index) => new GuideStep
            {
                Id = Guid.NewGuid(),
                StepNumber = index + 1,
                Instruction = instruction.Trim()
            })
            .ToList();
    }

    private static GuideResponse ToResponse(Guide entry)
    {
        return new GuideResponse(
            entry.Id,
            entry.Title,
            entry.IsActive,
            entry.Steps
                .OrderBy(step => step.StepNumber)
                .Select(step => new GuideStepResponse(step.StepNumber, step.Instruction))
                .ToList(),
            entry.CreatedAtUtc,
            entry.UpdatedAtUtc,
            entry.RowVersion);
    }

    private static Dictionary<string, string[]> ValidateCreateRequest(CreateGuideRequest request)
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

        if (request.Steps is null || request.Steps.Count == 0)
        {
            errors["steps"] = ["At least 1 step is required."];
        }
        else if (request.Steps.Count > 50)
        {
            errors["steps"] = ["A guide may have at most 50 steps."];
        }
        else if (request.Steps.Any(string.IsNullOrWhiteSpace))
        {
            errors["steps"] = ["Each step instruction is required."];
        }
        else if (request.Steps.Any(step => step.Trim().Length > 1000))
        {
            errors["steps"] = ["Each step instruction must be 1000 characters or fewer."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateUpdateRequest(UpdateGuideRequest request)
    {
        var errors = ValidateCreateRequest(new CreateGuideRequest(request.Title, request.Steps));

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
