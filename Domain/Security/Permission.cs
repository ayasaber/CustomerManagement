namespace CustomerManagement.Api.Domain.Security;

public sealed class Permission
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public List<RolePermission> RolePermissions { get; set; } = [];
}
