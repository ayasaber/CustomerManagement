namespace CustomerManagement.Api.Contracts.QuickReplies;

public sealed record QuickReplyUsageSnapshot(
    Guid QuickReplyId,
    string TitleAtUse,
    string BodyAtUse,
    DateTime UsedAtUtc,
    Guid UsedByUserId);
