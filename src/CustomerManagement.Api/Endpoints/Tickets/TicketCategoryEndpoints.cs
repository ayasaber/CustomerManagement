using CustomerManagement.Api.Contracts.Tickets;
using CustomerManagement.Api.Domain.Tickets;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Tickets;

public static class TicketCategoryEndpoints
{
    public static IEndpointRouteBuilder MapTicketCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tickets/categories").WithTags("Tickets.Categories");

        group.MapGet("", ListCategoriesAsync)
            .RequireAuthorization()
            .WithName("ListTicketCategories")
            .WithSummary("List ticket categories");

        group.MapPost("", CreateCategoryAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketTaxonomyManage)
            .WithName("CreateTicketCategory")
            .WithSummary("Create a ticket category");

        group.MapPut("/{categoryId:guid}", UpdateCategoryAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketTaxonomyManage)
            .WithName("UpdateTicketCategory")
            .WithSummary("Update a ticket category");

        return app;
    }

    private static async Task<IResult> ListCategoriesAsync(
        [FromQuery] bool? activeOnly,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.TicketCategories.AsNoTracking().AsQueryable();
        if (activeOnly.GetValueOrDefault())
        {
            query = query.Where(category => category.IsActive);
        }

        var categories = await query
            .OrderBy(category => category.Name)
            .Select(category => new TicketCategoryResponse(
                category.Id,
                category.Name,
                category.Description,
                category.IsActive,
                category.CreatedAtUtc,
                category.UpdatedAtUtc,
                category.RowVersion))
            .ToListAsync(cancellationToken);

        return Results.Ok(categories);
    }

    private static async Task<IResult> CreateCategoryAsync(
        [FromBody] CreateTicketCategoryRequest request,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var normalizedName = request.Name.Trim();
        var duplicate = await dbContext.TicketCategories
            .AsNoTracking()
            .AnyAsync(category => category.Name == normalizedName, cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { message = "Category name already exists." });
        }

        var now = DateTime.UtcNow;
        var category = new TicketCategory
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Description = NormalizeOptional(request.Description),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.TicketCategories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/tickets/categories/{category.Id}",
            new TicketCategoryResponse(
                category.Id,
                category.Name,
                category.Description,
                category.IsActive,
                category.CreatedAtUtc,
                category.UpdatedAtUtc,
                category.RowVersion));
    }

    private static async Task<IResult> UpdateCategoryAsync(
        Guid categoryId,
        [FromBody] UpdateTicketCategoryRequest request,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateUpdateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var category = await dbContext.TicketCategories
            .FirstOrDefaultAsync(row => row.Id == categoryId, cancellationToken);

        if (category is null)
        {
            return Results.NotFound();
        }

        var normalizedName = request.Name.Trim();
        var duplicate = await dbContext.TicketCategories
            .AsNoTracking()
            .AnyAsync(row => row.Id != categoryId && row.Name == normalizedName, cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { message = "Category name already exists." });
        }

        category.Name = normalizedName;
        category.Description = NormalizeOptional(request.Description);
        category.IsActive = request.IsActive;
        category.UpdatedAtUtc = DateTime.UtcNow;
        dbContext.Entry(category).Property(row => row.RowVersion).OriginalValue = request.RowVersion;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "Category was updated by another request. Refresh and retry with the latest RowVersion.",
                Status = StatusCodes.Status409Conflict
            });
        }

        return Results.Ok(new TicketCategoryResponse(
            category.Id,
            category.Name,
            category.Description,
            category.IsActive,
            category.CreatedAtUtc,
            category.UpdatedAtUtc,
            category.RowVersion));
    }

    private static Dictionary<string, string[]> ValidateCreateRequest(CreateTicketCategoryRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (request.Name.Trim().Length > 100)
        {
            errors["name"] = ["Name must be 100 characters or fewer."];
        }

        if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Trim().Length > 500)
        {
            errors["description"] = ["Description must be 500 characters or fewer."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateUpdateRequest(UpdateTicketCategoryRequest request)
    {
        var errors = ValidateCreateRequest(new CreateTicketCategoryRequest(request.Name, request.Description));

        if (request.RowVersion is null || request.RowVersion.Length == 0)
        {
            errors["rowVersion"] = ["RowVersion is required."];
        }

        return errors;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
