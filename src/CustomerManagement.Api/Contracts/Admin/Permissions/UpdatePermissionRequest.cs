namespace CustomerManagement.Api.Contracts.Admin.Permissions;

public sealed record UpdatePermissionRequest(string Name, string? Description);
