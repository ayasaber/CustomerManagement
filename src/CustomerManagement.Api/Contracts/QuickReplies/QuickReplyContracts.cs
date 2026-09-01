namespace CustomerManagement.Api.Contracts.QuickReplies;

public sealed record CreateQuickReplyRequest(
    string Title,
    string Body,
    IReadOnlyList<string>? Tags);

public sealed record UpdateQuickReplyRequest(
    string Title,
    string Body,
    IReadOnlyList<string>? Tags,
    bool IsActive,
    byte[] RowVersion);

public sealed record QuickReplyResponse(
    Guid Id,
    string Title,
    string Body,
    IReadOnlyList<string> Tags,
    bool IsActive,
    Guid CreatedByUserId,
    Guid? UpdatedByUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion);

public sealed record QuickReplyListResponse(
    int TotalCount,
    IReadOnlyList<QuickReplyResponse> Items);
