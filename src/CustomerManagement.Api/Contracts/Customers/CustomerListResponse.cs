namespace CustomerManagement.Api.Contracts.Customers;

public sealed record CustomerListItemResponse(
    Guid Id,
    Guid? ApplicationUserId,
    string Name,
    string? Company,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? PrimaryEmail);

public sealed record CustomerListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<CustomerListItemResponse> Items);