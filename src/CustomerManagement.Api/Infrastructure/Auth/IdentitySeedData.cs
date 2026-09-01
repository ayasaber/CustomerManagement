using CustomerManagement.Api.Domain.Security;
using CustomerManagement.Api.Domain.Tickets;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Infrastructure.Auth;

public static class IdentitySeedData
{
    public static async Task EnsureSeededAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerManagementDbContext>();

        await EnsureRoleAsync(roleManager, AuthRoles.Admin);
        await EnsureRoleAsync(roleManager, AuthRoles.Agent);
        await EnsureRoleAsync(roleManager, AuthRoles.Customer);

        const string adminEmail = "admin@crm.local";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null)
        {
            var now = DateTime.UtcNow;
            adminUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = adminEmail,
                Email = adminEmail,
                DisplayName = "System Admin",
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(adminUser, "Admin!23456");
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to seed admin user: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, AuthRoles.Admin))
        {
            var roleResult = await userManager.AddToRoleAsync(adminUser, AuthRoles.Admin);
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to assign admin role: {errors}");
            }
        }

        await EnsurePermissionsAsync(dbContext, cancellationToken: default);
        await EnsureSystemSettingsAsync(dbContext, cancellationToken: default);
        await EnsureTicketTaxonomyAsync(dbContext, cancellationToken: default);
    }

    private static async Task EnsureRoleAsync(RoleManager<IdentityRole<Guid>> roleManager, string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to seed role '{roleName}': {errors}");
        }
    }

    private static async Task EnsurePermissionsAsync(CustomerManagementDbContext dbContext, CancellationToken cancellationToken)
    {
        var baselinePermissions = new Dictionary<string, string>
        {
            [Permissions.UsersManage] = "Manage user lifecycle and role assignments.",
            [Permissions.RolesManage] = "Manage role-permission assignments.",
            [Permissions.PermissionsManage] = "Manage permission catalog.",
            [Permissions.AuditRead] = "Read audit records.",
            [Permissions.SettingsManage] = "Manage system settings.",
            [Permissions.CustomersRead] = "Read customer records.",
            [Permissions.CustomersWrite] = "Create and update customer records.",
            [Permissions.TicketsRead] = "Read ticket records.",
            [Permissions.TicketsWrite] = "Create and update tickets.",
            [Permissions.TicketsAssign] = "Assign tickets to agents.",
            [Permissions.TicketsEscalate] = "Escalate tickets.",
            [Permissions.TicketsClose] = "Resolve and close tickets.",
            [Permissions.TicketTaxonomyManage] = "Manage ticket categories and priorities.",
            [Permissions.DashboardRead] = "Read dashboard assigned work and summaries.",
            [Permissions.DashboardCustomerContextRead] = "Read customer context for dashboard ticket views.",
            [Permissions.TicketTasksRead] = "Read ticket-linked tasks.",
            [Permissions.TicketTasksWrite] = "Create and update ticket-linked tasks.",
            [Permissions.TicketTasksComplete] = "Mark ticket-linked tasks as completed."
        };

        var existingPermissions = await dbContext.Permissions
            .ToDictionaryAsync(p => p.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var permission in baselinePermissions)
        {
            if (existingPermissions.ContainsKey(permission.Key))
            {
                continue;
            }

            dbContext.Permissions.Add(new Permission
            {
                Id = Guid.NewGuid(),
                Name = permission.Key,
                Description = permission.Value,
                CreatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var allPermissions = await dbContext.Permissions
            .AsNoTracking()
            .ToDictionaryAsync(p => p.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var roles = await dbContext.Roles
            .AsNoTracking()
            .Where(r => r.Name == AuthRoles.Admin || r.Name == AuthRoles.Agent || r.Name == AuthRoles.Customer)
            .ToDictionaryAsync(r => r.Name!, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var assignments = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [AuthRoles.Admin] =
            [
                Permissions.UsersManage,
                Permissions.RolesManage,
                Permissions.PermissionsManage,
                Permissions.AuditRead,
                Permissions.SettingsManage,
                Permissions.CustomersRead,
                Permissions.CustomersWrite,
                Permissions.TicketsRead,
                Permissions.TicketsWrite,
                Permissions.TicketsAssign,
                Permissions.TicketsEscalate,
                Permissions.TicketsClose,
                Permissions.TicketTaxonomyManage,
                Permissions.DashboardRead,
                Permissions.DashboardCustomerContextRead,
                Permissions.TicketTasksRead,
                Permissions.TicketTasksWrite,
                Permissions.TicketTasksComplete
            ],
            [AuthRoles.Agent] =
            [
                Permissions.CustomersRead,
                Permissions.CustomersWrite,
                Permissions.TicketsRead,
                Permissions.TicketsWrite,
                Permissions.TicketsAssign,
                Permissions.TicketsEscalate,
                Permissions.TicketsClose,
                Permissions.DashboardRead,
                Permissions.DashboardCustomerContextRead,
                Permissions.TicketTasksRead,
                Permissions.TicketTasksWrite,
                Permissions.TicketTasksComplete
            ],
            [AuthRoles.Customer] =
            [
                Permissions.TicketsRead,
                Permissions.TicketsWrite,
                Permissions.TicketsClose
            ]
        };

        var existingRolePermissions = await dbContext.RolePermissions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var existingSet = existingRolePermissions
            .Select(rp => (rp.RoleId, rp.PermissionId))
            .ToHashSet();

        foreach (var assignment in assignments)
        {
            if (!roles.TryGetValue(assignment.Key, out var role))
            {
                continue;
            }

            foreach (var permissionName in assignment.Value)
            {
                if (!allPermissions.TryGetValue(permissionName, out var permission))
                {
                    continue;
                }

                if (existingSet.Contains((role.Id, permission.Id)))
                {
                    continue;
                }

                dbContext.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureSystemSettingsAsync(CustomerManagementDbContext dbContext, CancellationToken cancellationToken)
    {
        var defaults = new Dictionary<string, (string Value, string Description)>
        {
            [SystemSettingKeys.AuthAccessTokenMinutes] = ("15", "Access token lifetime in minutes."),
            [SystemSettingKeys.AuthRefreshTokenDays] = ("14", "Refresh token lifetime in days."),
            [SystemSettingKeys.AuthPasswordMinLength] = ("8", "Minimum password length for registration.")
        };

        var existing = await dbContext.SystemSettings
            .AsNoTracking()
            .ToDictionaryAsync(setting => setting.Key, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var row in defaults)
        {
            if (existing.ContainsKey(row.Key))
            {
                continue;
            }

            dbContext.SystemSettings.Add(new SystemSetting
            {
                Id = Guid.NewGuid(),
                Key = row.Key,
                Value = row.Value.Value,
                Description = row.Value.Description,
                UpdatedAtUtc = now,
                UpdatedByUserId = null
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureTicketTaxonomyAsync(CustomerManagementDbContext dbContext, CancellationToken cancellationToken)
    {
        var defaultCategories = new (string Name, string? Description)[]
        {
            ("General Inquiry", "General support requests and non-specialized issues."),
            ("Technical Issue", "Bugs, errors, and product malfunction reports."),
            ("Billing", "Invoices, payments, and subscription billing concerns."),
            ("Account Access", "Login, verification, and account access problems."),
            ("Feature Request", "Requests for product improvements and new capabilities.")
        };

        var defaultPriorities = new (string Name, int SortOrder)[]
        {
            ("Low", 10),
            ("Normal", 20),
            ("High", 30),
            ("Urgent", 40)
        };

        var existingCategoryNames = await dbContext.TicketCategories
            .AsNoTracking()
            .Select(row => row.Name)
            .ToListAsync(cancellationToken);

        var categoryNameSet = existingCategoryNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        foreach (var category in defaultCategories)
        {
            if (categoryNameSet.Contains(category.Name))
            {
                continue;
            }

            dbContext.TicketCategories.Add(new TicketCategory
            {
                Id = Guid.NewGuid(),
                Name = category.Name,
                Description = category.Description,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        var existingPriorityNames = await dbContext.TicketPriorities
            .AsNoTracking()
            .Select(row => row.Name)
            .ToListAsync(cancellationToken);

        var priorityNameSet = existingPriorityNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var priority in defaultPriorities)
        {
            if (priorityNameSet.Contains(priority.Name))
            {
                continue;
            }

            dbContext.TicketPriorities.Add(new TicketPriority
            {
                Id = Guid.NewGuid(),
                Name = priority.Name,
                SortOrder = priority.SortOrder,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
