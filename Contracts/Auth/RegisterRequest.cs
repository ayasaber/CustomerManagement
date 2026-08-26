namespace CustomerManagement.Api.Contracts.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    string DisplayName,
    string AccountType);
