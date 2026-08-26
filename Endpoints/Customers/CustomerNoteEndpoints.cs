using CustomerManagement.Api.Contracts.Customers;
using CustomerManagement.Api.Domain.Customers;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Customers;

public static class CustomerNoteEndpoints
{
    public static IEndpointRouteBuilder MapCustomerNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .RequireAuthorization("AgentOnly");

        group.MapPost("/{customerId:guid}/notes", async (
            Guid customerId,
            [FromBody] CreateCustomerNoteRequest request,
            CustomerManagementDbContext dbContext,
            HttpContext httpContext) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var customerExists = await dbContext.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Id == customerId);

            if (!customerExists)
            {
                return Results.NotFound();
            }

            var createdBy = httpContext.User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(createdBy))
            {
                createdBy = "agent";
            }

            var note = new CustomerNote
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                Body = request.Body.Trim(),
                CreatedBy = createdBy,
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.CustomerNotes.Add(note);
            await dbContext.SaveChangesAsync();

            var response = MapToResponse(note);
            return Results.Created($"/api/customers/{customerId}/notes/{note.Id}", response);
        })
        .WithName("CreateCustomerNote")
        .WithSummary("Add a note to a customer record");

        group.MapGet("/{customerId:guid}/notes", async (
            Guid customerId,
            CustomerManagementDbContext dbContext) =>
        {
            var customerExists = await dbContext.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Id == customerId);

            if (!customerExists)
            {
                return Results.NotFound();
            }

            var notes = await dbContext.CustomerNotes
                .AsNoTracking()
                .Where(n => n.CustomerId == customerId)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Select(n => new CustomerNoteResponse(
                    n.Id,
                    n.CustomerId,
                    n.Body,
                    n.CreatedBy,
                    n.CreatedAtUtc))
                .ToListAsync();

            return Results.Ok(notes);
        })
        .WithName("ListCustomerNotes")
        .WithSummary("List notes for a customer record");

        return app;
    }

    private static CustomerNoteResponse MapToResponse(CustomerNote note)
    {
        return new CustomerNoteResponse(
            note.Id,
            note.CustomerId,
            note.Body,
            note.CreatedBy,
            note.CreatedAtUtc);
    }

    private static Dictionary<string, string[]> Validate(CreateCustomerNoteRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            errors["body"] = ["Body is required."];
            return errors;
        }

        if (request.Body.Trim().Length > 4000)
        {
            errors["body"] = ["Body must be 4000 characters or fewer."];
        }

        return errors;
    }
}
