using System.Security.Claims;
using CustomerManagement.Api.Contracts.Feedback;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Feedback;

public static class FeedbackEndpoints
{
    public static IEndpointRouteBuilder MapFeedbackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/feedback").WithTags("Feedback");

        group.MapPost(string.Empty, CreateAsync)
            .RequireAuthorization("Permission:" + Permissions.FeedbackSubmit)
            .WithName("CreateFeedback")
            .WithSummary("Submit customer experience feedback");

        group.MapGet(string.Empty, ListAsync)
            .RequireAuthorization("Permission:" + Permissions.FeedbackRead)
            .WithName("ListFeedback")
            .WithSummary("List submitted customer feedback");

        return app;
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateFeedbackRequest request,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var actorUserId = ResolveActorUserId(httpContext.User);
        if (!actorUserId.HasValue)
        {
            return Results.Forbid();
        }

        var customerId = await dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.ApplicationUserId == actorUserId.Value)
            .Select(customer => (Guid?)customer.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!customerId.HasValue)
        {
            return Results.Forbid();
        }

        var feedback = new Domain.Feedback.Feedback
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId.Value,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.FeedbackEntries.Add(feedback);
        await dbContext.SaveChangesAsync(cancellationToken);

        var customerName = await dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.Id == customerId.Value)
            .Select(customer => customer.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return Results.Created(
            $"/api/feedback/{feedback.Id}",
            new FeedbackResponse(
                feedback.Id,
                feedback.CustomerId,
                customerName ?? string.Empty,
                feedback.Rating,
                feedback.Comment,
                feedback.CreatedAtUtc));
    }

    private static async Task<IResult> ListAsync(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var resolvedPage = page.GetValueOrDefault(1);
        var resolvedPageSize = pageSize.GetValueOrDefault(20);

        var errors = ValidateListRequest(resolvedPage, resolvedPageSize);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var query = dbContext.FeedbackEntries
            .AsNoTracking()
            .OrderByDescending(feedback => feedback.CreatedAtUtc)
            .ThenByDescending(feedback => feedback.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .Select(feedback => new FeedbackResponse(
                feedback.Id,
                feedback.CustomerId,
                feedback.Customer.Name,
                feedback.Rating,
                feedback.Comment,
                feedback.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(new FeedbackListResponse(resolvedPage, resolvedPageSize, totalCount, items));
    }

    private static Dictionary<string, string[]> ValidateCreateRequest(CreateFeedbackRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Rating is < 1 or > 5)
        {
            errors["rating"] = ["Rating must be between 1 and 5."];
        }

        if (!string.IsNullOrWhiteSpace(request.Comment) && request.Comment.Trim().Length > 2000)
        {
            errors["comment"] = ["Comment must be 2000 characters or fewer."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateListRequest(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (pageSize < 1 || pageSize > 100)
        {
            errors["pageSize"] = ["PageSize must be between 1 and 100."];
        }

        return errors;
    }

    private static Guid? ResolveActorUserId(ClaimsPrincipal actor)
    {
        var candidate = actor.FindFirstValue(ClaimTypes.NameIdentifier) ?? actor.FindFirstValue("sub");
        return Guid.TryParse(candidate, out var parsed) ? parsed : null;
    }
}
