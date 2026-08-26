namespace CustomerManagement.Api.Contracts.Customers;

public sealed record CustomerNoteResponse(
    Guid Id,
    Guid CustomerId,
    string Body,
    string CreatedBy,
    DateTime CreatedAtUtc);
