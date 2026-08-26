namespace CustomerManagement.Api.Domain.Security;

public sealed class RolePermission
{
    public Guid RoleId { get; set; }

    public Guid PermissionId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Permission Permission { get; set; } = null!;
}
