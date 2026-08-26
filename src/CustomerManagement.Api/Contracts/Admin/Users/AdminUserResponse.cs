namespace CustomerManagement.Api.Contracts.Admin.Users;

public sealed record AdminUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion,
    IReadOnlyList<string> Roles);
