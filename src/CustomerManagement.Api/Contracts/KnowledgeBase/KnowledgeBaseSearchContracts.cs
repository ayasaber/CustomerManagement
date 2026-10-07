namespace CustomerManagement.Api.Contracts.KnowledgeBase;

public enum KnowledgeBaseContentType
{
    Faq,
    HelpArticle,
    Guide
}

public sealed record KnowledgeBaseSearchResultResponse(
    string ContentType,
    Guid Id,
    string Title,
    string Snippet);

public sealed record KnowledgeBaseSearchResponse(string Query, IReadOnlyList<KnowledgeBaseSearchResultResponse> Results);
