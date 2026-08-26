namespace CustomerManagement.Api.Infrastructure.Auth;

public static class Permissions
{
    public const string UsersManage = "admin.users.manage";
    public const string RolesManage = "admin.roles.manage";
    public const string PermissionsManage = "admin.permissions.manage";
    public const string AuditRead = "audit.read";
    public const string SettingsManage = "settings.manage";
    public const string CustomersRead = "customers.read";
    public const string CustomersWrite = "customers.write";
}
