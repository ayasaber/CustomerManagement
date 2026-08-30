using CustomerManagement.Api.Contracts.Customers;
using CustomerManagement.Api.Domain.Customers;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Customers;

public static class CustomerProfileEndpoints
{
    public static IEndpointRouteBuilder MapCustomerProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers");
        // TODO(Story-09): when customer deletion endpoint is added, capture an audit event via IAuditLogWriter.

        group.MapPost("", () =>
            Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid operation",
                Detail = "Customer profiles are created during customer registration only.",
                Status = StatusCodes.Status400BadRequest
            }))
        .RequireAuthorization("Permission:" + Permissions.CustomersWrite)
        .WithName("CreateCustomerProfile")
        .WithSummary("Customer profile creation is disabled. Use customer registration.");

        group.MapGet("", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            CustomerManagementDbContext dbContext) =>
        {
            var resolvedPage = page.GetValueOrDefault(1);
            var resolvedPageSize = pageSize.GetValueOrDefault(50);
            var errors = new Dictionary<string, string[]>();

            if (resolvedPage < 1)
            {
                errors["page"] = ["Page must be greater than or equal to 1."];
            }

            if (resolvedPageSize < 1 || resolvedPageSize > 200)
            {
                errors["pageSize"] = ["PageSize must be between 1 and 200."];
            }

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var query = dbContext.Customers
                .AsNoTracking()
                .Include(customer => customer.ContactDetails)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                query = query.Where(customer =>
                    customer.Name.ToLower().Contains(term)
                    || (customer.Company != null && customer.Company.ToLower().Contains(term))
                    || customer.ContactDetails.Any(detail => detail.Value.ToLower().Contains(term)));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(customer => customer.Name)
                .ThenBy(customer => customer.Id)
                .Skip((resolvedPage - 1) * resolvedPageSize)
                .Take(resolvedPageSize)
                .Select(customer => new CustomerListItemResponse(
                    customer.Id,
                    customer.ApplicationUserId,
                    customer.Name,
                    customer.Company,
                    customer.CreatedAtUtc,
                    customer.UpdatedAtUtc,
                    customer.ContactDetails
                        .Where(detail => detail.Channel == ContactChannel.Email)
                        .OrderByDescending(detail => detail.IsPrimary)
                        .ThenBy(detail => detail.CreatedAtUtc)
                        .Select(detail => detail.Value)
                        .FirstOrDefault()))
                .ToListAsync();

            return Results.Ok(new CustomerListResponse(resolvedPage, resolvedPageSize, totalCount, items));
        })
        .RequireAuthorization("Permission:" + Permissions.CustomersRead)
        .WithName("ListCustomerProfiles")
        .WithSummary("List customer profiles for browse workflows");

        group.MapGet("/{customerId:guid}", async (Guid customerId, CustomerManagementDbContext dbContext) =>
        {
            var customer = await dbContext.Customers
                .AsNoTracking()
                .Include(c => c.ContactDetails)
                .FirstOrDefaultAsync(c => c.Id == customerId);

            if (customer is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(MapToCustomerProfileResponse(customer));
        })
        .RequireAuthorization("Permission:" + Permissions.CustomersRead)
        .WithName("GetCustomerProfile")
        .WithSummary("Get a customer profile by id");

        group.MapPut("/{customerId:guid}", async (
            Guid customerId,
            [FromBody] UpdateCustomerProfileRequest request,
            CustomerManagementDbContext dbContext) =>
        {
            var errors = Validate(request.Name, request.Company, request.ContactDetails);
            if (request.RowVersion is null)
            {
                errors["rowVersion"] = ["RowVersion is required."];
            }

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var customer = await dbContext.Customers
                .Include(c => c.ContactDetails)
                .FirstOrDefaultAsync(c => c.Id == customerId);

            if (customer is null)
            {
                return Results.NotFound();
            }

            customer.Name = request.Name.Trim();
            customer.Company = string.IsNullOrWhiteSpace(request.Company) ? null : request.Company.Trim();
            customer.UpdatedAtUtc = DateTime.UtcNow;
            dbContext.Entry(customer).Property(c => c.RowVersion).OriginalValue = request.RowVersion!;

            if (request.ContactDetails is not null)
            {
                var existingDetails = await dbContext.ContactDetails
                    .Where(cd => cd.CustomerId == customer.Id)
                    .ToListAsync();
                dbContext.ContactDetails.RemoveRange(existingDetails);

                foreach (var detail in request.ContactDetails)
                {
                    dbContext.ContactDetails.Add(new ContactDetail
                    {
                        Id = Guid.NewGuid(),
                        CustomerId = customer.Id,
                        Channel = (ContactChannel)detail.Channel,
                        Value = detail.Value.Trim(),
                        Label = string.IsNullOrWhiteSpace(detail.Label) ? null : detail.Label.Trim(),
                        IsPrimary = detail.IsPrimary,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }
            }

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Conflict(new ProblemDetails
                {
                    Title = "Concurrency conflict",
                    Detail = "Customer profile was updated by another request. Refresh and retry with the latest RowVersion.",
                    Status = StatusCodes.Status409Conflict
                });
            }

            var updatedCustomer = await dbContext.Customers
                .AsNoTracking()
                .Include(c => c.ContactDetails)
                .FirstAsync(c => c.Id == customer.Id);

            return Results.Ok(MapToCustomerProfileResponse(updatedCustomer));
        })
        .RequireAuthorization("Permission:" + Permissions.CustomersWrite)
        .WithName("UpdateCustomerProfile")
        .WithSummary("Update an existing customer profile by id");

        group.MapGet("/{customerId:guid}/contact-details", async (Guid customerId, CustomerManagementDbContext dbContext) =>
        {
            var customerExists = await dbContext.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Id == customerId);

            if (!customerExists)
            {
                return Results.NotFound();
            }

            var contactDetails = await dbContext.ContactDetails
                .AsNoTracking()
                .Where(cd => cd.CustomerId == customerId)
                .OrderBy(cd => cd.Channel)
                .ThenByDescending(cd => cd.IsPrimary)
                .ThenBy(cd => cd.CreatedAtUtc)
                .Select(cd => new CustomerContactDetailResponse(
                    cd.Id,
                    (int)cd.Channel,
                    cd.Value,
                    cd.Label,
                    cd.IsPrimary,
                    cd.CreatedAtUtc))
                .ToListAsync();

            var response = new CustomerContactDetailsResponse(customerId, contactDetails);
            return Results.Ok(response);
        })
        .RequireAuthorization("Permission:" + Permissions.CustomersRead)
        .WithName("GetCustomerContactDetails")
        .WithSummary("Get all contact details for a customer by id");

        return app;
    }

    private static CustomerProfileResponse MapToCustomerProfileResponse(Customer customer)
    {
        var orderedDetails = customer.ContactDetails
            .OrderBy(cd => cd.Channel)
            .ThenByDescending(cd => cd.IsPrimary)
            .ThenBy(cd => cd.CreatedAtUtc)
            .Select(cd => new CustomerContactDetailResponse(
                cd.Id,
                (int)cd.Channel,
                cd.Value,
                cd.Label,
                cd.IsPrimary,
                cd.CreatedAtUtc))
            .ToList();

        return new CustomerProfileResponse(
            customer.Id,
            customer.ApplicationUserId,
            customer.Name,
            customer.Company,
            customer.CreatedAtUtc,
            customer.UpdatedAtUtc,
            customer.RowVersion,
            orderedDetails);
    }

    private static Dictionary<string, string[]> Validate(
        string name,
        string? company,
        IReadOnlyList<CreateCustomerContactDetailRequest>? details)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (name.Trim().Length > 200)
        {
            errors["name"] = ["Name must be 200 characters or fewer."];
        }

        if (!string.IsNullOrWhiteSpace(company) && company.Trim().Length > 200)
        {
            errors["company"] = ["Company must be 200 characters or fewer."];
        }

        if (details is null)
        {
            return errors;
        }

        var channelPrimaryCount = new Dictionary<int, int>();

        for (var i = 0; i < details.Count; i++)
        {
            var detail = details[i];

            if (!Enum.IsDefined(typeof(ContactChannel), detail.Channel))
            {
                errors[$"contactDetails[{i}].channel"] = ["Channel is invalid."];
            }

            if (string.IsNullOrWhiteSpace(detail.Value))
            {
                errors[$"contactDetails[{i}].value"] = ["Value is required."];
            }
            else if (detail.Value.Trim().Length > 320)
            {
                errors[$"contactDetails[{i}].value"] = ["Value must be 320 characters or fewer."];
            }

            if (!string.IsNullOrWhiteSpace(detail.Label) && detail.Label.Trim().Length > 100)
            {
                errors[$"contactDetails[{i}].label"] = ["Label must be 100 characters or fewer."];
            }

            if (!detail.IsPrimary)
            {
                continue;
            }

            channelPrimaryCount.TryGetValue(detail.Channel, out var currentCount);
            channelPrimaryCount[detail.Channel] = currentCount + 1;
        }

        foreach (var pair in channelPrimaryCount.Where(pair => pair.Value > 1))
        {
            errors[$"contactDetails.channel.{pair.Key}.primary"] = ["Only one primary contact is allowed per channel."];
        }

        return errors;
    }
}
