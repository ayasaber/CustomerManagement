namespace CustomerManagement.Api.Contracts.TicketNotes;

public sealed record CreateTicketInternalNoteRequest(
    Guid TicketId,
    string Body,
    Guid[] MentionedUserIds,
    bool OfferReassign,
    Guid? ReassignToUserId);

public sealed record TicketInternalNoteMentionResponse(
    Guid MentionedUserId,
    string MentionedDisplayName,
    DateTime MentionedAtUtc,
    bool NotificationDelivered);

public sealed record TicketInternalNoteResponse(
    Guid Id,
    Guid TicketId,
    Guid AuthorUserId,
    string AuthorDisplayName,
    string Body,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion,
    IReadOnlyList<TicketInternalNoteMentionResponse> Mentions);

public sealed record TicketInternalNoteListResponse(
    int TotalCount,
    IReadOnlyList<TicketInternalNoteResponse> Items);

public sealed record CreateTicketHandoffRequest(
    Guid NoteId,
    Guid TicketId,
    Guid TargetAssigneeUserId,
    string? Message,
    byte[] TicketRowVersion);

public sealed record RespondTicketHandoffRequest(
    bool Accept,
    string? Message,
    byte[] TicketRowVersion);

public sealed record TicketHandoffRequestResponse(
    Guid Id,
    Guid TicketId,
    Guid NoteId,
    Guid RequestedByUserId,
    Guid TargetAssigneeUserId,
    string Status,
    string? ResponseMessage,
    DateTime RequestedAtUtc,
    DateTime? RespondedAtUtc,
    Guid? UpdatedTicketAssigneeUserId);

public sealed record TicketHandoffRequestListResponse(
    int TotalCount,
    IReadOnlyList<TicketHandoffRequestResponse> Items);

public sealed record TicketInternalNoteCreateResponse(
    TicketInternalNoteResponse Note,
    TicketReassignProposalResponse? ReassignProposal);

public sealed record TicketReassignProposalResponse(
    Guid TicketId,
    Guid ReassignToUserId);
