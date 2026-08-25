namespace CustomerManagement.Api.Contracts.Customers;

public sealed record UpdateCustomerProfileRequest(
    string Name,
    string? Company,
    IReadOnlyList<CreateCustomerContactDetailRequest>? ContactDetails);
