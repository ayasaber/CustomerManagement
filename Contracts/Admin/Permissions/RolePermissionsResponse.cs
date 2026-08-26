namespace CustomerManagement.Api.Contracts.Admin.Permissions;

public sealed record RolePermissionsResponse(
    Guid RoleId,
    string RoleName,
    IReadOnlyList<PermissionResponse> Permissions);
