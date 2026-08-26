namespace CustomerManagement.Api.Contracts.Admin.Users;

public sealed record UpdateAdminUserRequest(
    string DisplayName,
    bool IsActive,
    byte[] RowVersion);
