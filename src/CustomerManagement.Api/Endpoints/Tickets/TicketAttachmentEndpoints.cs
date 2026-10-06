using System.Security.Claims;
using CustomerManagement.Api.Contracts.Tickets;
using CustomerManagement.Api.Domain.Tickets;
using CustomerManagement.Api.Infrastructure.Attachments;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CustomerManagement.Api.Endpoints.Tickets;

public static class TicketAttachmentEndpoints
{
    public static IEndpointRouteBuilder MapTicketAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tickets/{ticketId:guid}/attachments").WithTags("Ticket Attachments");

        group.MapPost(string.Empty, CreateAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketsWrite)
            .WithName("CreateTicketAttachment")
            .WithSummary("Upload an attachment to a ticket")
            .Accepts<IFormFile>("multipart/form-data")
            .DisableAntiforgery();

        group.MapGet(string.Empty, ListAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketsRead)
            .WithName("ListTicketAttachments")
            .WithSummary("List attachments for a ticket");

        group.MapGet("/{attachmentId:guid}/content", GetContentAsync)
            .RequireAuthorization("Permission:" + Permissions.TicketsRead)
            .WithName("GetTicketAttachmentContent")
            .WithSummary("Retrieve a ticket attachment binary content");

        return app;
    }

    private static async Task<IResult> CreateAsync(
        Guid ticketId,
        [FromForm] IFormFile? file,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        IAttachmentStorage attachmentStorage,
        IOptions<AttachmentStorageOptions> attachmentStorageOptions,
        CancellationToken cancellationToken)
    {
        var errors = Validate(file, attachmentStorageOptions.Value);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var actorUserId = ResolveActorUserId(httpContext.User);
        if (!actorUserId.HasValue)
        {
            return Results.Forbid();
        }

        var ticket = await dbContext.Tickets
            .FirstOrDefaultAsync(row => row.Id == ticketId, cancellationToken);

        if (ticket is null)
        {
            return Results.NotFound();
        }

        var access = await CanAccessTicketAsync(
            httpContext.User, actorUserId.Value, ticket.CustomerId, ticket.AssignedToUserId, dbContext, cancellationToken);
        if (!access.Allowed)
        {
            return access.ReturnNotFound ? Results.NotFound() : Results.Forbid();
        }

        var uploader = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == actorUserId.Value && row.IsActive, cancellationToken);

        if (uploader is null)
        {
            return Results.NotFound();
        }

        await using var inputStream = file!.OpenReadStream();
        var stored = await attachmentStorage.SaveAsync(
            file.FileName,
            file.ContentType,
            inputStream,
            cancellationToken);

        var now = DateTime.UtcNow;
        var attachment = new TicketAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            OriginalFileName = stored.OriginalFileName,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes,
            StorageKey = stored.StorageKey,
            UploadedByUserId = uploader.Id,
            UploadedByDisplayName = ResolveDisplayName(uploader.DisplayName, uploader.Email),
            CreatedAtUtc = now
        };

        dbContext.TicketAttachments.Add(attachment);
        dbContext.TicketHistoryEntries.Add(new TicketHistoryEntry
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ActionType = "ticket.attachment.added",
            FieldName = "attachmentId",
            OldValue = null,
            NewValue = attachment.Id.ToString(),
            ActorUserId = actorUserId,
            ActorEmail = ResolveActorEmail(httpContext.User),
            OccurredAtUtc = now
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await attachmentStorage.DeleteAsync(stored.StorageKey, cancellationToken);
            throw;
        }

        return Results.Created(
            $"/api/tickets/{ticket.Id}/attachments/{attachment.Id}",
            MapToResponse(attachment));
    }

    private static async Task<IResult> ListAsync(
        Guid ticketId,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actorUserId = ResolveActorUserId(httpContext.User);
        if (!actorUserId.HasValue)
        {
            return Results.Forbid();
        }

        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .Where(row => row.Id == ticketId)
            .Select(row => new { row.CustomerId, row.AssignedToUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (ticket is null)
        {
            return Results.NotFound();
        }

        var access = await CanAccessTicketAsync(
            httpContext.User, actorUserId.Value, ticket.CustomerId, ticket.AssignedToUserId, dbContext, cancellationToken);
        if (!access.Allowed)
        {
            return access.ReturnNotFound ? Results.NotFound() : Results.Forbid();
        }

        var attachments = await dbContext.TicketAttachments
            .AsNoTracking()
            .Where(row => row.TicketId == ticketId)
            .OrderByDescending(row => row.CreatedAtUtc)
            .Select(row => new TicketAttachmentResponse(
                row.Id,
                row.TicketId,
                row.OriginalFileName,
                row.ContentType,
                row.SizeBytes,
                row.UploadedByUserId,
                row.UploadedByDisplayName,
                row.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(attachments);
    }

    private static async Task<IResult> GetContentAsync(
        Guid ticketId,
        Guid attachmentId,
        HttpContext httpContext,
        CustomerManagementDbContext dbContext,
        IAttachmentStorage attachmentStorage,
        CancellationToken cancellationToken)
    {
        var actorUserId = ResolveActorUserId(httpContext.User);
        if (!actorUserId.HasValue)
        {
            return Results.Forbid();
        }

        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .Where(row => row.Id == ticketId)
            .Select(row => new { row.CustomerId, row.AssignedToUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (ticket is null)
        {
            return Results.NotFound();
        }

        var access = await CanAccessTicketAsync(
            httpContext.User, actorUserId.Value, ticket.CustomerId, ticket.AssignedToUserId, dbContext, cancellationToken);
        if (!access.Allowed)
        {
            return access.ReturnNotFound ? Results.NotFound() : Results.Forbid();
        }

        var attachment = await dbContext.TicketAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == attachmentId && row.TicketId == ticketId, cancellationToken);

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

    private static TicketAttachmentResponse MapToResponse(TicketAttachment attachment)
    {
        return new TicketAttachmentResponse(
            attachment.Id,
            attachment.TicketId,
            attachment.OriginalFileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.UploadedByUserId,
            attachment.UploadedByDisplayName,
            attachment.CreatedAtUtc);
    }

    private static string ResolveDisplayName(string displayName, string? email)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            return email;
        }

        return "Unknown";
    }

    private static Guid? ResolveActorUserId(ClaimsPrincipal actor)
    {
        var candidate = actor.FindFirstValue(ClaimTypes.NameIdentifier) ?? actor.FindFirstValue("sub");
        return Guid.TryParse(candidate, out var parsed) ? parsed : null;
    }

    private static string? ResolveActorEmail(ClaimsPrincipal actor)
    {
        return actor.FindFirstValue(ClaimTypes.Email) ?? actor.FindFirstValue("email") ?? actor.Identity?.Name;
    }

    private static bool IsAdmin(ClaimsPrincipal actor)
    {
        return actor.IsInRole(AuthRoles.Admin)
            || actor.Claims.Any(claim =>
                string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                && string.Equals(claim.Value, AuthRoles.Admin, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAgent(ClaimsPrincipal actor)
    {
        return actor.IsInRole(AuthRoles.Agent)
            || actor.Claims.Any(claim =>
                string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                && string.Equals(claim.Value, AuthRoles.Agent, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCustomer(ClaimsPrincipal actor)
    {
        return actor.IsInRole(AuthRoles.Customer)
            || actor.Claims.Any(claim =>
                string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                && string.Equals(claim.Value, AuthRoles.Customer, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<AccessResult> CanAccessTicketAsync(
        ClaimsPrincipal actor,
        Guid actorUserId,
        Guid ticketCustomerId,
        Guid? assignedToUserId,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (IsAdmin(actor))
        {
            return AccessResult.Allow();
        }

        if (IsAgent(actor))
        {
            if (!assignedToUserId.HasValue || assignedToUserId.Value == actorUserId)
            {
                return AccessResult.Allow();
            }

            return AccessResult.Forbid();
        }

        if (IsCustomer(actor))
        {
            var ownedCustomerIds = await ResolveOwnedCustomerIdsAsync(actorUserId, dbContext, cancellationToken);
            return ownedCustomerIds.Contains(ticketCustomerId)
                ? AccessResult.Allow()
                : AccessResult.NotFound();
        }

        return AccessResult.Forbid();
    }

    private static Task<HashSet<Guid>> ResolveOwnedCustomerIdsAsync(
        Guid actorUserId,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.ApplicationUserId == actorUserId)
            .Select(customer => customer.Id)
            .Distinct()
            .ToHashSetAsync(cancellationToken);
    }

    private sealed record AccessResult(bool Allowed, bool ReturnNotFound)
    {
        public static AccessResult Allow() => new(true, false);
        public static AccessResult Forbid() => new(false, false);
        public static AccessResult NotFound() => new(false, true);
    }
}
