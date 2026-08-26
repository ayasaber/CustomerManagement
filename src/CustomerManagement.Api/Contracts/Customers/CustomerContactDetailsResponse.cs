namespace CustomerManagement.Api.Contracts.Customers;

public sealed record CustomerContactDetailsResponse(
    Guid CustomerId,
    IReadOnlyList<CustomerContactDetailResponse> ContactDetails);
