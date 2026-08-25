using CustomerManagement.Api.Contracts.Customers;
using CustomerManagement.Api.Domain.Customers;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace CustomerManagement.Api.Endpoints.Customers;

public static class CustomerProfileEndpoints
{
    public static IEndpointRouteBuilder MapCustomerProfileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/customers", async (
            [FromBody] CreateCustomerProfileRequest request,
            CustomerManagementDbContext dbContext) =>
        {
            var errors = Validate(request);
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

            var response = new CustomerProfileResponse(
                customer.Id,
                customer.Name,
                customer.Company,
                customer.CreatedAtUtc,
                customer.UpdatedAtUtc,
                customer.ContactDetails
                    .Select(cd => new CustomerContactDetailResponse(
                        cd.Id,
                        (int)cd.Channel,
                        cd.Value,
                        cd.Label,
                        cd.IsPrimary,
                        cd.CreatedAtUtc))
                    .ToList());

            return Results.Created($"/api/customers/{customer.Id}", response);
        })
        .WithName("CreateCustomerProfile")
        .WithSummary("Create a customer profile with optional contact details");

        return app;
    }

    private static Dictionary<string, string[]> Validate(CreateCustomerProfileRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (request.Name.Trim().Length > 200)
        {
            errors["name"] = ["Name must be 200 characters or fewer."];
        }

        if (!string.IsNullOrWhiteSpace(request.Company) && request.Company.Trim().Length > 200)
        {
            errors["company"] = ["Company must be 200 characters or fewer."];
        }

        var details = request.ContactDetails;
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
