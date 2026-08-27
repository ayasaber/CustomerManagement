namespace CustomerManagement.Api.Contracts.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    string DisplayName,
    string AccountType,
    string? FullName = null,
    string? Company = null,
    IReadOnlyList<RegisterContactDetailRequest>? ContactDetails = null);

public sealed record RegisterContactDetailRequest(
    int Channel,
    string Value,
    string? Label,
    bool IsPrimary);
