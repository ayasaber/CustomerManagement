namespace CustomerManagement.Api.Infrastructure.Auth;

public static class PermissionsCatalog
{
    public const string AdminUsersManage = "admin.users.manage";
    public const string AdminPermissionsManage = "admin.permissions.manage";
    public const string AuditRead = "audit.read";
    public const string SettingsManage = "settings.manage";
    public const string CustomersRead = "customers.read";
    public const string CustomersWrite = "customers.write";

    public static IReadOnlyList<string> ForRoles(IEnumerable<string> roles)
    {
        var permissionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            switch (role.ToLowerInvariant())
            {
                case AuthRoles.Admin:
                    permissionSet.Add(AdminUsersManage);
                    permissionSet.Add(AdminPermissionsManage);
                    permissionSet.Add(AuditRead);
                    permissionSet.Add(SettingsManage);
                    permissionSet.Add(CustomersRead);
                    permissionSet.Add(CustomersWrite);
                    break;
                case AuthRoles.Agent:
                    permissionSet.Add(CustomersRead);
                    permissionSet.Add(CustomersWrite);
                    break;
                case AuthRoles.Customer:
                    break;
            }
        }

        return permissionSet.ToList();
    }
}
