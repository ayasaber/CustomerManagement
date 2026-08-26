using CustomerManagement.Api.Contracts.Admin.Permissions;
using CustomerManagement.Api.Domain.Security;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Admin;

public static class PermissionsEndpoints
{
    public static IEndpointRouteBuilder MapPermissionsEndpoints(this IEndpointRouteBuilder app)
    {
        var adminPermissionsGroup = app.MapGroup("/api/admin/permissions")
            .RequireAuthorization("Permission:" + Permissions.PermissionsManage)
            .WithTags("Admin.Permissions");

        adminPermissionsGroup.MapGet("", ListPermissionsAsync)
            .WithName("ListPermissions")
            .WithSummary("List available permissions");

        adminPermissionsGroup.MapPost("", CreatePermissionAsync)
            .WithName("CreatePermission")
            .WithSummary("Create a permission");

        adminPermissionsGroup.MapPut("/{permissionId:guid}", UpdatePermissionAsync)
            .WithName("UpdatePermission")
            .WithSummary("Update a permission name or description");

        var rolePermissionsGroup = app.MapGroup("/api/admin/roles")
            .RequireAuthorization("Permission:" + Permissions.RolesManage)
            .WithTags("Admin.RolePermissions");

        rolePermissionsGroup.MapGet("/{roleId:guid}/permissions", GetRolePermissionsAsync)
            .WithName("GetRolePermissions")
            .WithSummary("Get permissions assigned to a role");

        rolePermissionsGroup.MapPut("/{roleId:guid}/permissions", UpdateRolePermissionsAsync)
            .WithName("UpdateRolePermissions")
            .WithSummary("Replace role-permission assignments");

        return app;
    }

    private static async Task<IResult> ListPermissionsAsync(
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var permissions = await dbContext.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PermissionResponse(p.Id, p.Name, p.Description, p.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(permissions);
    }

    private static async Task<IResult> CreatePermissionAsync(
        [FromBody] CreatePermissionRequest request,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidatePermissionInput(request.Name, request.Description);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var normalizedName = NormalizePermissionName(request.Name);
        var exists = await dbContext.Permissions
            .AsNoTracking()
            .AnyAsync(p => p.Name == normalizedName, cancellationToken);

        if (exists)
        {
            return Results.Conflict(new { message = "Permission name already exists." });
        }

        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Description = NormalizeOptional(request.Description),
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Permissions.Add(permission);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new PermissionResponse(permission.Id, permission.Name, permission.Description, permission.CreatedAtUtc);
        return Results.Created($"/api/admin/permissions/{permission.Id}", response);
    }

    private static async Task<IResult> UpdatePermissionAsync(
        Guid permissionId,
        [FromBody] UpdatePermissionRequest request,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidatePermissionInput(request.Name, request.Description);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var permission = await dbContext.Permissions
            .FirstOrDefaultAsync(p => p.Id == permissionId, cancellationToken);

        if (permission is null)
        {
            return Results.NotFound();
        }

        var normalizedName = NormalizePermissionName(request.Name);
        var duplicates = await dbContext.Permissions
            .AsNoTracking()
            .AnyAsync(p => p.Id != permissionId && p.Name == normalizedName, cancellationToken);

        if (duplicates)
        {
            return Results.Conflict(new { message = "Permission name already exists." });
        }

        if (!string.Equals(permission.Name, normalizedName, StringComparison.OrdinalIgnoreCase))
        {
            var hasAssignments = await dbContext.RolePermissions
                .AsNoTracking()
                .AnyAsync(rp => rp.PermissionId == permissionId, cancellationToken);

            if (hasAssignments)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid operation",
                    Detail = "Cannot rename a permission while it is assigned to one or more roles.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        permission.Name = normalizedName;
        permission.Description = NormalizeOptional(request.Description);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new PermissionResponse(permission.Id, permission.Name, permission.Description, permission.CreatedAtUtc));
    }

    private static async Task<IResult> GetRolePermissionsAsync(
        Guid roleId,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

        if (role is null)
        {
            return Results.NotFound();
        }

        var permissions = await (
                from rp in dbContext.RolePermissions.AsNoTracking()
                join p in dbContext.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
                where rp.RoleId == roleId
                orderby p.Name
                select new PermissionResponse(p.Id, p.Name, p.Description, p.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(new RolePermissionsResponse(roleId, role.Name ?? string.Empty, permissions));
    }

    private static async Task<IResult> UpdateRolePermissionsAsync(
        Guid roleId,
        [FromBody] UpdateRolePermissionsRequest request,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.PermissionIds is null || request.PermissionIds.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["permissionIds"] = ["At least one permission id is required."]
            });
        }

        var role = await dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

        if (role is null)
        {
            return Results.NotFound();
        }

        var desiredPermissionIds = request.PermissionIds.Distinct().ToList();
        var existingPermissions = await dbContext.Permissions
            .AsNoTracking()
            .Where(p => desiredPermissionIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        if (existingPermissions.Count != desiredPermissionIds.Count)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["permissionIds"] = ["One or more permission ids do not exist."]
            });
        }

        var existingRolePermissions = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(cancellationToken);

        dbContext.RolePermissions.RemoveRange(existingRolePermissions);

        var now = DateTime.UtcNow;
        dbContext.RolePermissions.AddRange(desiredPermissionIds.Select(permissionId => new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId,
            CreatedAtUtc = now
        }));

        await dbContext.SaveChangesAsync(cancellationToken);

        var orderedPermissions = existingPermissions
            .OrderBy(p => p.Name)
            .Select(p => new PermissionResponse(p.Id, p.Name, p.Description, p.CreatedAtUtc))
            .ToList();

        return Results.Ok(new RolePermissionsResponse(roleId, role.Name ?? string.Empty, orderedPermissions));
    }

    private static Dictionary<string, string[]> ValidatePermissionInput(string name, string? description)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["Name is required."];
            return errors;
        }

        var normalizedName = NormalizePermissionName(name);
        if (normalizedName.Length > 200)
        {
            errors["name"] = ["Name must be 200 characters or fewer."];
        }

        if (!normalizedName.Contains('.'))
        {
            errors["name"] = ["Name must follow dotted notation such as 'customers.read'."];
        }

        if (!string.IsNullOrWhiteSpace(description) && description.Trim().Length > 500)
        {
            errors["description"] = ["Description must be 500 characters or fewer."];
        }

        return errors;
    }

    private static string NormalizePermissionName(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
