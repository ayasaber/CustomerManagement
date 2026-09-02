using System.Security.Claims;
using System.Text.Json;
using CustomerManagement.Api.Contracts.TicketNotes;
using CustomerManagement.Api.Domain.Dashboard;
using CustomerManagement.Api.Domain.Security;
using CustomerManagement.Api.Domain.Tickets;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Dashboard;

public static class TicketNotesEndpoints
{
    private const int MaxMentionsPerNote = 10;

    public static IEndpointRouteBuilder MapTicketNotesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ticket-notes").WithTags("Ticket Notes");

        group.MapGet(string.Empty, ListAsync)
            .RequireAuthorization($"Permission:{Permissions.TicketInternalNotesRead}");

        group.MapPost(string.Empty, CreateAsync)
            .RequireAuthorization($"Permission:{Permissions.TicketInternalNotesWrite}");

        group.MapPost("/{noteId:guid}/handoff-requests", CreateHandoffRequestAsync)
            .RequireAuthorization($"Permission:{Permissions.TicketHandoffRequestCreate}");

        group.MapGet("/handoff-requests/me", ListMyHandoffRequestsAsync)
            .RequireAuthorization($"Permission:{Permissions.TicketHandoffRespond}");

        group.MapPost("/handoff-requests/{handoffRequestId:guid}/respond", RespondHandoffRequestAsync)
            .RequireAuthorization($"Permission:{Permissions.TicketHandoffRespond}");

        return app;
    }

    private static async Task<Results<Ok<TicketInternalNoteListResponse>, BadRequest<string>, ForbidHttpResult>> ListAsync(
        [FromQuery] Guid ticketId,
        ClaimsPrincipal user,
        CustomerManagementDbContext db,
        CancellationToken cancellationToken)
    {
        if (ticketId == Guid.Empty)
        {
            return TypedResults.BadRequest("ticketId is required.");
        }

        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return TypedResults.Forbid();
        }

        var roles = GetCurrentRoles(user);
        if (!await CanViewTicketAsync(db, ticketId, userId.Value, roles, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        var notes = await db.TicketInternalNotes
            .AsNoTracking()
            .Where(note => note.TicketId == ticketId)
            .Include(note => note.AuthorUser)
            .Include(note => note.Mentions)
                .ThenInclude(mention => mention.MentionedUser)
            .OrderBy(note => note.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var responseItems = notes
            .Select(MapNoteResponse)
            .ToList();

        return TypedResults.Ok(new TicketInternalNoteListResponse(responseItems.Count, responseItems));
    }

    private static async Task<Results<Created<TicketInternalNoteCreateResponse>, BadRequest<string>, ForbidHttpResult>> CreateAsync(
        CreateTicketInternalNoteRequest request,
        ClaimsPrincipal user,
        CustomerManagementDbContext db,
        CancellationToken cancellationToken)
    {
        if (request.TicketId == Guid.Empty)
        {
            return TypedResults.BadRequest("ticketId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return TypedResults.BadRequest("body is required.");
        }

        if (request.MentionedUserIds.Length > MaxMentionsPerNote)
        {
            return TypedResults.BadRequest($"At most {MaxMentionsPerNote} mentions are allowed.");
        }

        var mentionUserIds = request.MentionedUserIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (mentionUserIds.Length != request.MentionedUserIds.Length)
        {
            return TypedResults.BadRequest("mentionedUserIds must contain only unique non-empty IDs.");
        }

        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return TypedResults.Forbid();
        }

        var roles = GetCurrentRoles(user);
        if (!await CanViewTicketAsync(db, request.TicketId, userId.Value, roles, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        if (mentionUserIds.Contains(userId.Value))
        {
            return TypedResults.BadRequest("You cannot mention yourself. Mention teammates only.");
        }

        var ticket = await db.Tickets
            .SingleOrDefaultAsync(item => item.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return TypedResults.BadRequest("ticket not found.");
        }

        var now = DateTime.UtcNow;
        var note = new TicketInternalNote
        {
            Id = Guid.NewGuid(),
            TicketId = request.TicketId,
            AuthorUserId = userId.Value,
            Body = request.Body.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            RowVersion = [1]
        };

        db.TicketInternalNotes.Add(note);

        if (mentionUserIds.Length > 0)
        {
            var eligibleMentionedUserIds = await (
                from mentionedUser in db.Users
                join userRole in db.UserRoles on mentionedUser.Id equals userRole.UserId
                join role in db.Roles on userRole.RoleId equals role.Id
                where mentionUserIds.Contains(mentionedUser.Id)
                    && mentionedUser.IsActive
                    && role.Name == AuthRoles.Agent
                select mentionedUser.Id)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (eligibleMentionedUserIds.Count != mentionUserIds.Length)
            {
                return TypedResults.BadRequest("One or more mentioned users are not active agents.");
            }

            foreach (var mentionedUserId in mentionUserIds)
            {
                var mention = new TicketInternalNoteMention
                {
                    Id = Guid.NewGuid(),
                    TicketInternalNoteId = note.Id,
                    MentionedUserId = mentionedUserId,
                    MentionedAtUtc = now,
                    NotificationDelivered = true,
                    NotificationDeliveredAtUtc = now
                };

                db.TicketInternalNoteMentions.Add(mention);

                var notificationPayload = JsonSerializer.Serialize(new
                {
                    noteId = note.Id,
                    ticketId = note.TicketId,
                    mentionedByUserId = userId.Value,
                    mentionedUserId,
                    createdAtUtc = now
                });

                db.UserNotifications.Add(new UserNotification
                {
                    Id = Guid.NewGuid(),
                    Type = "ticket-note-mention",
                    RecipientUserId = mentionedUserId,
                    PayloadJson = notificationPayload,
                    CreatedAtUtc = now
                });
            }
        }

        TicketReassignProposalResponse? reassignProposal = null;
        if (request.OfferReassign)
        {
            if (request.ReassignToUserId is null || request.ReassignToUserId == Guid.Empty)
            {
                return TypedResults.BadRequest("reassignToUserId is required when offerReassign is true.");
            }

            if (!mentionUserIds.Contains(request.ReassignToUserId.Value))
            {
                return TypedResults.BadRequest("reassignToUserId must be included in mentionedUserIds.");
            }

            reassignProposal = new TicketReassignProposalResponse(request.TicketId, request.ReassignToUserId.Value);
        }

        await db.SaveChangesAsync(cancellationToken);

        await db.Entry(note)
            .Reference(item => item.AuthorUser)
            .LoadAsync(cancellationToken);

        await db.Entry(note)
            .Collection(item => item.Mentions)
            .Query()
            .Include(mention => mention.MentionedUser)
            .LoadAsync(cancellationToken);

        var response = new TicketInternalNoteCreateResponse(MapNoteResponse(note), reassignProposal);
        return TypedResults.Created($"/api/ticket-notes/{note.Id}", response);
    }

    private static async Task<Results<Created<TicketHandoffRequestResponse>, BadRequest<string>, ForbidHttpResult>> CreateHandoffRequestAsync(
        Guid noteId,
        CreateTicketHandoffRequest request,
        ClaimsPrincipal user,
        CustomerManagementDbContext db,
        CancellationToken cancellationToken)
    {
        if (noteId == Guid.Empty || request.NoteId == Guid.Empty || noteId != request.NoteId)
        {
            return TypedResults.BadRequest("noteId mismatch.");
        }

        if (request.TicketId == Guid.Empty)
        {
            return TypedResults.BadRequest("ticketId is required.");
        }

        if (request.TargetAssigneeUserId == Guid.Empty)
        {
            return TypedResults.BadRequest("targetAssigneeUserId is required.");
        }

        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return TypedResults.Forbid();
        }

        var roles = GetCurrentRoles(user);
        if (!await CanViewTicketAsync(db, request.TicketId, userId.Value, roles, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        var note = await db.TicketInternalNotes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.NoteId && item.TicketId == request.TicketId, cancellationToken);

        if (note is null)
        {
            return TypedResults.BadRequest("note not found for ticket.");
        }

        if (request.TargetAssigneeUserId == userId.Value)
        {
            return TypedResults.BadRequest("targetAssigneeUserId must be a teammate, not the requester.");
        }

        var targetAssigneeEligible = await (
            from target in db.Users
            join userRole in db.UserRoles on target.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where target.Id == request.TargetAssigneeUserId
                && target.IsActive
                && role.Name == AuthRoles.Agent
            select target.Id)
            .AnyAsync(cancellationToken);

        if (!targetAssigneeEligible)
        {
            return TypedResults.BadRequest("target assignee is not an active agent.");
        }

        var ticket = await db.Tickets
            .SingleOrDefaultAsync(item => item.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return TypedResults.BadRequest("ticket not found.");
        }

        if (!RowVersionMatches(ticket.RowVersion, request.TicketRowVersion))
        {
            return TypedResults.BadRequest("ticket row version mismatch.");
        }

        var now = DateTime.UtcNow;
        var handoff = new TicketHandoffRequest
        {
            Id = Guid.NewGuid(),
            TicketId = request.TicketId,
            NoteId = request.NoteId,
            RequestedByUserId = userId.Value,
            TargetAssigneeUserId = request.TargetAssigneeUserId,
            Status = TicketHandoffRequestStatus.Pending,
            Message = NormalizeOptional(request.Message, 1000),
            RequestedAtUtc = now
        };

        db.TicketHandoffRequests.Add(handoff);

        var notificationPayload = JsonSerializer.Serialize(new
        {
            handoffRequestId = handoff.Id,
            ticketId = handoff.TicketId,
            noteId = handoff.NoteId,
            requestedByUserId = handoff.RequestedByUserId,
            targetAssigneeUserId = handoff.TargetAssigneeUserId,
            createdAtUtc = now
        });

        db.UserNotifications.Add(new UserNotification
        {
            Id = Guid.NewGuid(),
            Type = "ticket-handoff-request",
            RecipientUserId = handoff.TargetAssigneeUserId,
            PayloadJson = notificationPayload,
            CreatedAtUtc = now
        });

        AddHistory(
            db,
            ticket,
            "ticket.handoff.requested",
            "targetAssigneeUserId",
            ticket.AssignedToUserId?.ToString(),
            handoff.TargetAssigneeUserId.ToString(),
            user,
            now);

        await db.SaveChangesAsync(cancellationToken);

        var response = MapHandoffResponse(handoff, ticket.AssignedToUserId);
        return TypedResults.Created($"/api/ticket-notes/handoff-requests/{handoff.Id}", response);
    }

    private static async Task<Results<Ok<TicketHandoffRequestListResponse>, ForbidHttpResult>> ListMyHandoffRequestsAsync(
        ClaimsPrincipal user,
        CustomerManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return TypedResults.Forbid();
        }

        var requests = await db.TicketHandoffRequests
            .AsNoTracking()
            .Where(item => item.TargetAssigneeUserId == userId.Value)
            .OrderByDescending(item => item.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        var ticketAssigneeByTicketId = await db.Tickets
            .AsNoTracking()
            .Where(item => requests.Select(request => request.TicketId).Contains(item.Id))
            .Select(item => new { item.Id, item.AssignedToUserId })
            .ToDictionaryAsync(item => item.Id, item => item.AssignedToUserId, cancellationToken);

        var responseItems = requests
            .Select(request =>
            {
                ticketAssigneeByTicketId.TryGetValue(request.TicketId, out var assignedToUserId);
                return MapHandoffResponse(request, assignedToUserId);
            })
            .ToList();

        return TypedResults.Ok(new TicketHandoffRequestListResponse(responseItems.Count, responseItems));
    }

    private static async Task<Results<Ok<TicketHandoffRequestResponse>, BadRequest<string>, ForbidHttpResult>> RespondHandoffRequestAsync(
        Guid handoffRequestId,
        RespondTicketHandoffRequest request,
        ClaimsPrincipal user,
        CustomerManagementDbContext db,
        CancellationToken cancellationToken)
    {
        if (handoffRequestId == Guid.Empty)
        {
            return TypedResults.BadRequest("handoffRequestId is required.");
        }

        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return TypedResults.Forbid();
        }

        var roles = GetCurrentRoles(user);
        var isAdmin = roles.Contains(AuthRoles.Admin);

        var handoff = await db.TicketHandoffRequests
            .SingleOrDefaultAsync(item => item.Id == handoffRequestId, cancellationToken);

        if (handoff is null)
        {
            return TypedResults.BadRequest("handoff request not found.");
        }

        if (!isAdmin && handoff.TargetAssigneeUserId != userId.Value)
        {
            return TypedResults.Forbid();
        }

        if (handoff.Status != TicketHandoffRequestStatus.Pending)
        {
            return TypedResults.BadRequest("handoff request is no longer pending.");
        }

        var ticket = await db.Tickets
            .SingleOrDefaultAsync(item => item.Id == handoff.TicketId, cancellationToken);

        if (ticket is null)
        {
            return TypedResults.BadRequest("ticket not found.");
        }

        if (!RowVersionMatches(ticket.RowVersion, request.TicketRowVersion))
        {
            return TypedResults.BadRequest("ticket row version mismatch.");
        }

        var now = DateTime.UtcNow;
        handoff.Status = request.Accept ? TicketHandoffRequestStatus.Accepted : TicketHandoffRequestStatus.Rejected;
        handoff.ResponseMessage = NormalizeOptional(request.Message, 1000);
        handoff.RespondedAtUtc = now;

        AddHistory(
            db,
            ticket,
            "ticket.handoff.responded",
            "handoffStatus",
            TicketHandoffRequestStatus.Pending.ToString(),
            handoff.Status.ToString(),
            user,
            now);

        Guid? updatedAssignee = ticket.AssignedToUserId;
        if (request.Accept)
        {
            var canForceAssign = isAdmin && user.HasClaim("permission", Permissions.TicketHandoffForceAssign);
            if (handoff.TargetAssigneeUserId != userId.Value && !canForceAssign)
            {
                return TypedResults.Forbid();
            }

            var oldAssignee = ticket.AssignedToUserId;
            ticket.AssignedToUserId = handoff.TargetAssigneeUserId;
            ticket.UpdatedAtUtc = now;
            updatedAssignee = ticket.AssignedToUserId;

            AddHistory(
                db,
                ticket,
                "ticket.assigned",
                "assignedToUserId",
                oldAssignee?.ToString(),
                ticket.AssignedToUserId?.ToString(),
                user,
                now);
        }

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(MapHandoffResponse(handoff, updatedAssignee));
    }

    private static TicketInternalNoteResponse MapNoteResponse(TicketInternalNote note)
    {
        var mentions = note.Mentions
            .OrderBy(item => item.MentionedAtUtc)
            .Select(item => new TicketInternalNoteMentionResponse(
                item.MentionedUserId,
                item.MentionedUser.DisplayName,
                item.MentionedAtUtc,
                item.NotificationDelivered))
            .ToList();

        return new TicketInternalNoteResponse(
            note.Id,
            note.TicketId,
            note.AuthorUserId,
            note.AuthorUser.DisplayName,
            note.Body,
            note.CreatedAtUtc,
            note.UpdatedAtUtc,
            note.RowVersion,
            mentions);
    }

    private static TicketHandoffRequestResponse MapHandoffResponse(TicketHandoffRequest request, Guid? updatedTicketAssigneeUserId)
    {
        return new TicketHandoffRequestResponse(
            request.Id,
            request.TicketId,
            request.NoteId,
            request.RequestedByUserId,
            request.TargetAssigneeUserId,
            request.Status.ToString(),
            request.ResponseMessage,
            request.RequestedAtUtc,
            request.RespondedAtUtc,
            updatedTicketAssigneeUserId);
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out var userId) ? userId : null;
    }

    private static HashSet<string> GetCurrentRoles(ClaimsPrincipal user)
    {
        return user.Claims
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<bool> CanViewTicketAsync(
        CustomerManagementDbContext db,
        Guid ticketId,
        Guid userId,
        IReadOnlySet<string> roles,
        CancellationToken cancellationToken)
    {
        if (roles.Contains(AuthRoles.Admin))
        {
            return await db.Tickets
                .AsNoTracking()
                .AnyAsync(item => item.Id == ticketId, cancellationToken);
        }

        return await db.Tickets
            .AsNoTracking()
            .AnyAsync(item => item.Id == ticketId && item.AssignedToUserId == userId, cancellationToken);
    }

    private static bool RowVersionMatches(byte[] currentRowVersion, byte[] providedRowVersion)
    {
        if (currentRowVersion is null || providedRowVersion is null)
        {
            return false;
        }

        if (currentRowVersion.Length != providedRowVersion.Length)
        {
            return false;
        }

        for (var i = 0; i < currentRowVersion.Length; i++)
        {
            if (currentRowVersion[i] != providedRowVersion[i])
            {
                return false;
            }
        }

        return true;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            normalized = normalized[..maxLength];
        }

        return normalized;
    }

    private static void AddHistory(
        CustomerManagementDbContext db,
        Ticket ticket,
        string actionType,
        string fieldName,
        string? oldValue,
        string? newValue,
        ClaimsPrincipal actor,
        DateTime occurredAtUtc)
    {
        db.TicketHistoryEntries.Add(new TicketHistoryEntry
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ActionType = actionType,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            ActorUserId = GetCurrentUserId(actor),
            ActorEmail = ResolveActorEmail(actor),
            OccurredAtUtc = occurredAtUtc
        });
    }

    private static string? ResolveActorEmail(ClaimsPrincipal actor)
    {
        return actor.FindFirstValue(ClaimTypes.Email) ?? actor.FindFirstValue("email") ?? actor.Identity?.Name;
    }
}
