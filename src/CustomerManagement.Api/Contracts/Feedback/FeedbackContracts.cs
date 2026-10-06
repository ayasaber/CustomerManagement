namespace CustomerManagement.Api.Contracts.Feedback;

public sealed record CreateFeedbackRequest(int Rating, string? Comment);

public sealed record FeedbackResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    int Rating,
    string? Comment,
    DateTime CreatedAtUtc);

public sealed record FeedbackListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<FeedbackResponse> Items);
