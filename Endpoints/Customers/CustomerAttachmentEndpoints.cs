using CustomerManagement.Api.Contracts.Customers;
using CustomerManagement.Api.Domain.Customers;
using CustomerManagement.Api.Infrastructure.Attachments;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CustomerManagement.Api.Endpoints.Customers;

public static class CustomerAttachmentEndpoints
{
    public static IEndpointRouteBuilder MapCustomerAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers");

        group.MapPost("/{customerId:guid}/attachments", async (
            Guid customerId,
            [FromForm] IFormFile? file,
            CustomerManagementDbContext dbContext,
            IAttachmentStorage attachmentStorage,
            IOptions<AttachmentStorageOptions> attachmentStorageOptions,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var errors = Validate(file, attachmentStorageOptions.Value);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var customerExists = await dbContext.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Id == customerId, cancellationToken);

            if (!customerExists)
            {
                return Results.NotFound();
            }

            var createdBy = httpContext.User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(createdBy))
            {
                createdBy = "agent";
            }

            await using var inputStream = file!.OpenReadStream();
            var stored = await attachmentStorage.SaveAsync(
                file.FileName,
                file.ContentType,
                inputStream,
                cancellationToken);

            var attachment = new CustomerAttachment
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                OriginalFileName = stored.OriginalFileName,
                ContentType = stored.ContentType,
                SizeBytes = stored.SizeBytes,
                StorageKey = stored.StorageKey,
                CreatedBy = createdBy,
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.CustomerAttachments.Add(attachment);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await attachmentStorage.DeleteAsync(stored.StorageKey, cancellationToken);
                throw;
            }

            var response = MapToResponse(attachment);
            return Results.Created($"/api/customers/{customerId}/attachments/{attachment.Id}", response);
        })
        .WithName("CreateCustomerAttachment")
        .WithSummary("Upload an attachment to a customer record")
        .RequireAuthorization("Permission:" + Permissions.CustomersWrite)
        .Accepts<IFormFile>("multipart/form-data")
        .DisableAntiforgery();

        group.MapGet("/{customerId:guid}/attachments", async (
            Guid customerId,
            CustomerManagementDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var customerExists = await dbContext.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Id == customerId, cancellationToken);

            if (!customerExists)
            {
                return Results.NotFound();
            }

            var attachments = await dbContext.CustomerAttachments
                .AsNoTracking()
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.CreatedAtUtc)
                .Select(a => new CustomerAttachmentResponse(
                    a.Id,
                    a.CustomerId,
                    a.OriginalFileName,
                    a.ContentType,
                    a.SizeBytes,
                    a.StorageKey,
                    a.CreatedBy,
                    a.CreatedAtUtc))
                .ToListAsync(cancellationToken);

            return Results.Ok(attachments);
        })
        .RequireAuthorization("Permission:" + Permissions.CustomersRead)
        .WithName("ListCustomerAttachments")
        .WithSummary("List attachments for a customer record");

        group.MapGet("/{customerId:guid}/attachments/{attachmentId:guid}/content", async (
            Guid customerId,
            Guid attachmentId,
            CustomerManagementDbContext dbContext,
            IAttachmentStorage attachmentStorage,
            CancellationToken cancellationToken) =>
        {
            var attachment = await dbContext.CustomerAttachments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    a => a.Id == attachmentId && a.CustomerId == customerId,
                    cancellationToken);

            if (attachment is null)
            {
                return Results.NotFound();
            }

            try
            {
                var content = await attachmentStorage.OpenReadAsync(attachment.StorageKey, cancellationToken);
                return Results.File(
                    content.Content,
                    attachment.ContentType,
                    attachment.OriginalFileName,
                    enableRangeProcessing: true);
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization("Permission:" + Permissions.CustomersRead)
        .WithName("GetCustomerAttachmentContent")
        .WithSummary("Retrieve a customer attachment binary content");

        return app;
    }

    private static Dictionary<string, string[]> Validate(IFormFile? file, AttachmentStorageOptions options)
    {
        var errors = new Dictionary<string, string[]>();

        if (file is null)
        {
            errors["file"] = ["File is required."];
            return errors;
        }

        if (file.Length <= 0)
        {
            errors["file"] = ["File cannot be empty."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(file.FileName))
        {
            errors["fileName"] = ["File name is required."];
        }

        if (file.Length > options.MaxFileSizeBytes)
        {
            errors["file"] = [$"File exceeds maximum allowed size of {options.MaxFileSizeBytes} bytes."];
        }

        if (options.AllowedContentTypes.Count > 0)
        {
            var contentType = file.ContentType?.Trim();
            var isAllowed = !string.IsNullOrWhiteSpace(contentType) &&
                            options.AllowedContentTypes.Any(
                                allowed => string.Equals(allowed, contentType, StringComparison.OrdinalIgnoreCase));

            if (!isAllowed)
            {
                errors["contentType"] = ["File content type is not allowed."];
            }
        }

        return errors;
    }

    private static CustomerAttachmentResponse MapToResponse(CustomerAttachment attachment)
    {
        return new CustomerAttachmentResponse(
            attachment.Id,
            attachment.CustomerId,
            attachment.OriginalFileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.StorageKey,
            attachment.CreatedBy,
            attachment.CreatedAtUtc);
    }
}
