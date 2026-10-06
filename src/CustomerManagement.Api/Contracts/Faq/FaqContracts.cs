namespace CustomerManagement.Api.Contracts.Faq;

public sealed record CreateFaqEntryRequest(string Topic, string Question, string Answer, int SortOrder);

public sealed record UpdateFaqEntryRequest(
    string Topic,
    string Question,
    string Answer,
    int SortOrder,
    bool IsActive,
    byte[] RowVersion);

public sealed record FaqEntryResponse(
    Guid Id,
    string Topic,
    string Question,
    string Answer,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion);
