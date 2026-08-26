using System.Security.Claims;
using CustomerManagement.Api.Contracts.Admin.Users;
using CustomerManagement.Api.Domain.Security;
using CustomerManagement.Api.Infrastructure.Auditing;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Admin;

public static class UsersEndpoints
{
    private static readonly string[] AllowedRoles = [AuthRoles.Admin, AuthRoles.Agent, AuthRoles.Customer];

    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/users")
            .RequireAuthorization("AdminOnly")
            .WithTags("Admin.Users");

        group.MapGet("", ListUsersAsync)
            .WithName("ListAdminUsers")
            .WithSummary("List users with optional active/role filters and pagination");

        group.MapGet("/{userId:guid}", GetUserByIdAsync)
            .WithName("GetAdminUserById")
            .WithSummary("Get an admin view of a user by id");

        group.MapPost("", CreateUserAsync)
            .WithName("CreateAdminUser")
            .WithSummary("Create a user and assign one-or-more roles");

        group.MapPut("/{userId:guid}", UpdateUserAsync)
            .WithName("UpdateAdminUser")
            .WithSummary("Update user display name or active state");

        group.MapPut("/{userId:guid}/roles", UpdateUserRolesAsync)
            .WithName("UpdateAdminUserRoles")
            .WithSummary("Replace user role assignments");

        group.MapDelete("/{userId:guid}", SoftDeactivateUserAsync)
            .WithName("DeactivateAdminUser")
            .WithSummary("Soft deactivate a user account");

        return app;
    }

    private static async Task<IResult> ListUsersAsync(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] bool? isActive,
        [FromQuery] string? role,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var resolvedPage = page.GetValueOrDefault(1);
        var resolvedPageSize = pageSize.GetValueOrDefault(50);

        if (resolvedPage < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (resolvedPageSize < 1 || resolvedPageSize > 200)
        {
            errors["pageSize"] = ["PageSize must be between 1 and 200."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var normalizedRoleFilter = string.IsNullOrWhiteSpace(role)
            ? null
            : role.Trim().ToLowerInvariant();

        var query = dbContext.Users.AsNoTracking().AsQueryable();
        query = query.Where(u => u.IsActive == isActive.GetValueOrDefault(true));

        if (!string.IsNullOrWhiteSpace(normalizedRoleFilter))
        {
            var roleId = await dbContext.Roles
                .AsNoTracking()
                .Where(r => r.Name == normalizedRoleFilter)
                .Select(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (roleId == Guid.Empty)
            {
                return Results.Ok(new
                {
                    page = resolvedPage,
                    pageSize = resolvedPageSize,
                    totalCount = 0,
                    items = Array.Empty<AdminUserResponse>()
                });
            }

            query = query.Where(u => dbContext.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderByDescending(u => u.CreatedAtUtc)
            .ThenBy(u => u.Email)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .ToListAsync(cancellationToken);

        var roleMap = await BuildRoleMapAsync(users.Select(u => u.Id).ToArray(), dbContext, cancellationToken);

        var items = users
            .Select(user => MapToResponse(user, roleMap.GetValueOrDefault(user.Id, [])))
            .ToList();

        return Results.Ok(new
        {
            page = resolvedPage,
            pageSize = resolvedPageSize,
            totalCount,
            items
        });
    }

    private static async Task<IResult> GetUserByIdAsync(
        Guid userId,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Results.NotFound();
        }

        var roleMap = await BuildRoleMapAsync([user.Id], dbContext, cancellationToken);
        return Results.Ok(MapToResponse(user, roleMap.GetValueOrDefault(user.Id, [])));
    }

    private static async Task<IResult> CreateUserAsync(
        [FromBody] CreateAdminUserRequest request,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var normalizedRoles = NormalizeRoles(request.Roles);
        var invalidRoles = normalizedRoles.Where(role => !AllowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase)).ToList();
        if (invalidRoles.Count > 0)
        {
            foreach (var invalidRole in invalidRoles)
            {
                var index = normalizedRoles.IndexOf(invalidRole);
                errors[$"roles[{index}]"] = [$"Unknown role '{invalidRole}'. Allowed roles: admin, agent, customer."];
            }

            return Results.ValidationProblem(errors);
        }

        var existingUser = await userManager.FindByEmailAsync(request.Email.Trim());
        if (existingUser is not null)
        {
            return Results.Conflict(new { message = "An account with this email already exists." });
        }

        foreach (var role in normalizedRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var now = DateTime.UtcNow;
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            if (createResult.Errors.Any(e =>
                    string.Equals(e.Code, "DuplicateEmail", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e.Code, "DuplicateUserName", StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Conflict(new { message = "An account with this email already exists." });
            }

            return Results.BadRequest(new
            {
                message = "User creation failed.",
                errors = createResult.Errors.Select(e => e.Description)
            });
        }

        var roleResult = await userManager.AddToRolesAsync(user, normalizedRoles);
        if (!roleResult.Succeeded)
        {
            return Results.BadRequest(new
            {
                message = "Role assignment failed.",
                errors = roleResult.Errors.Select(e => e.Description)
            });
        }

        var createdUser = await dbContext.Users
            .AsNoTracking()
            .FirstAsync(u => u.Id == user.Id, cancellationToken);

        return Results.Created($"/api/admin/users/{user.Id}", MapToResponse(createdUser, normalizedRoles));
    }

    private static async Task<IResult> UpdateUserAsync(
        Guid userId,
        [FromBody] UpdateAdminUserRequest request,
        CustomerManagementDbContext dbContext,
        IAuditLogWriter auditLogWriter,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateUpdateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var actorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      httpContext.User.FindFirstValue("sub");

        if (request.IsActive == false && Guid.TryParse(actorId, out var actorGuid) && actorGuid == userId)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid operation",
                Detail = "Administrators cannot deactivate their own account.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Results.NotFound();
        }

        user.DisplayName = request.DisplayName.Trim();
        user.IsActive = request.IsActive;
        user.UpdatedAtUtc = DateTime.UtcNow;
        user.DeactivatedAtUtc = request.IsActive ? null : DateTime.UtcNow;
        dbContext.Entry(user).Property(u => u.RowVersion).OriginalValue = request.RowVersion;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "User was updated by another request. Refresh and retry with the latest RowVersion.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var actorUserId = TryParseActorId(httpContext.User);
        if (!request.IsActive)
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteModel(
                    DateTime.UtcNow,
                    actorUserId,
                    httpContext.User.FindFirstValue("email"),
                    "admin.user.deactivate",
                    "User",
                    userId.ToString(),
                    "Success",
                    new { path = "update-user", isActive = request.IsActive }),
                cancellationToken);
        }

        var roleMap = await BuildRoleMapAsync([userId], dbContext, cancellationToken);
        return Results.Ok(MapToResponse(user, roleMap.GetValueOrDefault(userId, [])));
    }

    private static async Task<IResult> UpdateUserRolesAsync(
        Guid userId,
        [FromBody] UpdateUserRolesRequest request,
        CustomerManagementDbContext dbContext,
        HttpContext httpContext,
        IAuditLogWriter auditLogWriter,
        CancellationToken cancellationToken)
    {
        var errors = ValidateRoleUpdateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var normalizedRoles = NormalizeRoles(request.Roles);
        var roleRows = await dbContext.Roles
            .AsNoTracking()
            .Where(r => normalizedRoles.Contains(r.Name!))
            .Select(r => new { r.Id, r.Name })
            .ToListAsync(cancellationToken);

        if (roleRows.Count != normalizedRoles.Count)
        {
            var knownRoles = roleRows.Select(r => r.Name ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < normalizedRoles.Count; i++)
            {
                if (!knownRoles.Contains(normalizedRoles[i]))
                {
                    errors[$"roles[{i}]"] = [$"Unknown role '{normalizedRoles[i]}'. Allowed roles: admin, agent, customer."];
                }
            }

            return Results.ValidationProblem(errors);
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Results.NotFound();
        }

        var desiredRoleIds = roleRows.Select(r => r.Id).ToHashSet();
        var existingRows = await dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .ToListAsync(cancellationToken);

        var existingRoleIds = existingRows.Select(ur => ur.RoleId).ToHashSet();

        var toRemove = existingRows
            .Where(ur => !desiredRoleIds.Contains(ur.RoleId))
            .ToList();

        var toAdd = desiredRoleIds
            .Where(roleId => !existingRoleIds.Contains(roleId))
            .Select(roleId => new IdentityUserRole<Guid>
            {
                UserId = userId,
                RoleId = roleId
            })
            .ToList();

        if (toRemove.Count > 0)
        {
            dbContext.UserRoles.RemoveRange(toRemove);
        }

        if (toAdd.Count > 0)
        {
            dbContext.UserRoles.AddRange(toAdd);
        }

        user.UpdatedAtUtc = DateTime.UtcNow;
        dbContext.Entry(user).Property(u => u.RowVersion).OriginalValue = request.RowVersion;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "User was updated by another request. Refresh and retry with the latest RowVersion.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var actorUserId = TryParseActorId(httpContext.User);
        await auditLogWriter.WriteAsync(
            new AuditLogWriteModel(
                DateTime.UtcNow,
                actorUserId,
                httpContext.User.FindFirstValue("email"),
                "admin.user.roles.update",
                "User",
                userId.ToString(),
                "Success",
                new { roles = normalizedRoles }),
            cancellationToken);

        var roleMap = await BuildRoleMapAsync([userId], dbContext, cancellationToken);
        return Results.Ok(MapToResponse(user, roleMap.GetValueOrDefault(userId, [])));
    }

    private static async Task<IResult> SoftDeactivateUserAsync(
        Guid userId,
        CustomerManagementDbContext dbContext,
        HttpContext httpContext,
        IAuditLogWriter auditLogWriter,
        CancellationToken cancellationToken)
    {
        var actorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      httpContext.User.FindFirstValue("sub");

        if (Guid.TryParse(actorId, out var actorGuid) && actorGuid == userId)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid operation",
                Detail = "Administrators cannot deactivate their own account.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Results.NotFound();
        }

        user.IsActive = false;
        user.DeactivatedAtUtc ??= DateTime.UtcNow;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogWriter.WriteAsync(
            new AuditLogWriteModel(
                DateTime.UtcNow,
                TryParseActorId(httpContext.User),
                httpContext.User.FindFirstValue("email"),
                "admin.user.deactivate",
                "User",
                userId.ToString(),
                "Success",
                new { path = "deactivate-endpoint", actor = actorId }),
            cancellationToken);

        return Results.NoContent();
    }

    private static Guid? TryParseActorId(ClaimsPrincipal principal)
    {
        var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      principal.FindFirstValue("sub");

        return Guid.TryParse(actorId, out var parsed) ? parsed : null;
    }

    private static async Task<Dictionary<Guid, IReadOnlyList<string>>> BuildRoleMapAsync(
        IReadOnlyList<Guid> userIds,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var roleRows = await (
                from userRole in dbContext.UserRoles.AsNoTracking()
                join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userIds.Contains(userRole.UserId)
                select new { userRole.UserId, RoleName = role.Name ?? string.Empty })
            .ToListAsync(cancellationToken);

        return roleRows
            .GroupBy(r => r.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g
                    .Select(x => x.RoleName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList());
    }

    private static AdminUserResponse MapToResponse(ApplicationUser user, IReadOnlyList<string> roles)
    {
        return new AdminUserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.IsActive,
            user.CreatedAtUtc,
            user.UpdatedAtUtc,
            user.RowVersion,
            roles);
    }

    private static Dictionary<string, string[]> ValidateCreateRequest(CreateAdminUserRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = ["Email is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["password"] = ["Password is required."];
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors["displayName"] = ["DisplayName is required."];
        }
        else if (request.DisplayName.Trim().Length > 200)
        {
            errors["displayName"] = ["DisplayName must be 200 characters or fewer."];
        }

        if (request.Roles is null || request.Roles.Count == 0)
        {
            errors["roles"] = ["At least one role is required."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateUpdateRequest(UpdateAdminUserRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors["displayName"] = ["DisplayName is required."];
        }
        else if (request.DisplayName.Trim().Length > 200)
        {
            errors["displayName"] = ["DisplayName must be 200 characters or fewer."];
        }

        if (request.RowVersion is null || request.RowVersion.Length == 0)
        {
            errors["rowVersion"] = ["RowVersion is required."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateRoleUpdateRequest(UpdateUserRolesRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Roles is null || request.Roles.Count == 0)
        {
            errors["roles"] = ["At least one role is required."];
        }

        if (request.RowVersion is null || request.RowVersion.Length == 0)
        {
            errors["rowVersion"] = ["RowVersion is required."];
        }

        return errors;
    }

    private static List<string> NormalizeRoles(IEnumerable<string> roles)
    {
        return roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
