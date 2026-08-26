namespace CustomerManagement.Api.Contracts.Admin.Users;

public sealed record CreateAdminUserRequest(
    string Email,
    string Password,
    string DisplayName,
    IReadOnlyList<string> Roles);
