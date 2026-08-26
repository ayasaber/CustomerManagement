namespace CustomerManagement.Api.Contracts.Customers;

public sealed record CustomerInteractionHistoryResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<CustomerInteractionHistoryItemResponse> Items);

public sealed record CustomerInteractionHistoryItemResponse(
    Guid InteractionId,
    string Channel,
    string Direction,
    DateTime TimestampUtc,
    string? Summary,
    string? SourceRef,
    string? SourceSystem);
