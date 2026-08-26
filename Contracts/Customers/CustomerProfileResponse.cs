namespace CustomerManagement.Api.Contracts.Customers;

public sealed record CustomerProfileResponse(
    Guid Id,
    string Name,
    string? Company,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion,
    IReadOnlyList<CustomerContactDetailResponse> ContactDetails);

public sealed record CustomerContactDetailResponse(
    Guid Id,
    int Channel,
    string Value,
    string? Label,
    bool IsPrimary,
    DateTime CreatedAtUtc);
