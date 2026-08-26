namespace CustomerManagement.Api.Contracts.Admin.Permissions;

public sealed record PermissionResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAtUtc);
