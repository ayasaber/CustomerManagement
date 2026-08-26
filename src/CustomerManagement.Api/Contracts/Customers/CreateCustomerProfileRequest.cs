namespace CustomerManagement.Api.Contracts.Customers;

public sealed record CreateCustomerProfileRequest(
    string Name,
    string? Company,
    IReadOnlyList<CreateCustomerContactDetailRequest>? ContactDetails);

public sealed record CreateCustomerContactDetailRequest(
    int Channel,
    string Value,
    string? Label,
    bool IsPrimary);
