namespace CustomerManagement.Api.Contracts.Customers;

public sealed record UpdateCustomerProfileRequest(
    string Name,
    string? Company,
    byte[] RowVersion,
    IReadOnlyList<CreateCustomerContactDetailRequest>? ContactDetails);
