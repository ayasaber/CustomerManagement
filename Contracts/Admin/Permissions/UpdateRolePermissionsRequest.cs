namespace CustomerManagement.Api.Contracts.Admin.Permissions;

public sealed record UpdateRolePermissionsRequest(IReadOnlyList<Guid> PermissionIds);
