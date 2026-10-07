namespace CustomerManagement.Api.Contracts.KnowledgeBase;

public sealed record CreateHelpArticleRequest(string Title, string Body);

public sealed record UpdateHelpArticleRequest(string Title, string Body, bool IsActive, byte[] RowVersion);

public sealed record HelpArticleResponse(
    Guid Id,
    string Title,
    string Body,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion);
