namespace CustomerManagement.Api.Contracts.Admin.Users;

public sealed record UpdateUserRolesRequest(IReadOnlyList<string> Roles, byte[] RowVersion);
