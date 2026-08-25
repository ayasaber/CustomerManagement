using CustomerManagement.Api.Contracts.Customers;
using CustomerManagement.Api.Domain.Customers;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Customers;

public static class CustomerProfileEndpoints
{
    public static IEndpointRouteBuilder MapCustomerProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .RequireAuthorization("AgentOnly");

        group.MapPost("", async (
            [FromBody] CreateCustomerProfileRequest request,
            CustomerManagementDbContext dbContext) =>
        {
            var errors = Validate(request.Name, request.Company, request.ContactDetails);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var now = DateTime.UtcNow;
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Company = string.IsNullOrWhiteSpace(request.Company) ? null : request.Company.Trim(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            foreach (var detail in request.ContactDetails ?? [])
            {
                customer.ContactDetails.Add(new ContactDetail
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    Channel = (ContactChannel)detail.Channel,
                    Value = detail.Value.Trim(),
                    Label = string.IsNullOrWhiteSpace(detail.Label) ? null : detail.Label.Trim(),
                    IsPrimary = detail.IsPrimary,
                    CreatedAtUtc = now
                });
            }

            dbContext.Customers.Add(customer);
            await dbContext.SaveChangesAsync();

            var response = MapToCustomerProfileResponse(customer);

            return Results.Created($"/api/customers/{customer.Id}", response);
        })
        .WithName("CreateCustomerProfile")
        .WithSummary("Create a customer profile with optional contact details");

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
        .WithName("GetCustomerProfile")
        .WithSummary("Get a customer profile by id");

        group.MapPut("/{customerId:guid}", async (
            Guid customerId,
            [FromBody] UpdateCustomerProfileRequest request,
            CustomerManagementDbContext dbContext) =>
        {
            var errors = Validate(request.Name, request.Company, request.ContactDetails);
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

            await dbContext.SaveChangesAsync();

            var updatedCustomer = await dbContext.Customers
                .AsNoTracking()
                .Include(c => c.ContactDetails)
                .FirstAsync(c => c.Id == customer.Id);

            return Results.Ok(MapToCustomerProfileResponse(updatedCustomer));
        })
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
            customer.Name,
            customer.Company,
            customer.CreatedAtUtc,
            customer.UpdatedAtUtc,
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
