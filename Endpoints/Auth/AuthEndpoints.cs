using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Domain.Security;
using CustomerManagement.Api.Infrastructure.Auditing;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous();

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous();

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous();

        group.MapPost("/logout", LogoutAsync)
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        CustomerManagementDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        JwtTokenService tokenService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var minPasswordLength = await ResolvePasswordMinLengthAsync(dbContext, cancellationToken);
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < minPasswordLength)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Password)] = [$"Password must be at least {minPasswordLength} characters long."]
            });
        }

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ConfirmPassword)] = ["Password and confirm password do not match."]
            });
        }

        var normalizedAccountType = request.AccountType.Trim().ToLowerInvariant();
        if (normalizedAccountType is not (AuthRoles.Agent or AuthRoles.Customer))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.AccountType)] = ["AccountType must be 'agent' or 'customer'."]
            });
        }

        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return Results.Conflict(new { message = "An account with this email already exists." });
        }

        var now = DateTime.UtcNow;
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Results.BadRequest(new
            {
                message = "Registration failed.",
                errors = createResult.Errors.Select(e => e.Description)
            });
        }

        if (!await roleManager.RoleExistsAsync(normalizedAccountType))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(normalizedAccountType));
        }

        var roleResult = await userManager.AddToRoleAsync(user, normalizedAccountType);
        if (!roleResult.Succeeded)
        {
            return Results.BadRequest(new
            {
                message = "Registration failed.",
                errors = roleResult.Errors.Select(e => e.Description)
            });
        }

        var roles = await userManager.GetRolesAsync(user);
        var roleList = roles.ToList();
        var tokens = await tokenService.IssueTokensAsync(user, roleList, httpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return Results.Created($"/api/users/{user.Id}", tokens);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        JwtTokenService tokenService,
        IAuditLogWriter auditLogWriter,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteModel(
                    DateTime.UtcNow,
                    user?.Id,
                    request.Email,
                    "auth.login",
                    "User",
                    request.Email,
                    "Failure",
                    new { reason = user is null ? "user_not_found" : "inactive" }),
                cancellationToken);

            return Results.Unauthorized();
        }

        var validPassword = await userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteModel(
                    DateTime.UtcNow,
                    user.Id,
                    user.Email,
                    "auth.login",
                    "User",
                    user.Id.ToString(),
                    "Failure",
                    new { reason = "invalid_password" }),
                cancellationToken);

            return Results.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        var roleList = roles.ToList();
        var tokens = await tokenService.IssueTokensAsync(user, roleList, httpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        await auditLogWriter.WriteAsync(
            new AuditLogWriteModel(
                DateTime.UtcNow,
                user.Id,
                user.Email,
                "auth.login",
                "User",
                user.Id.ToString(),
                "Success",
                new { ip = httpContext.Connection.RemoteIpAddress?.ToString() }),
            cancellationToken);

        return Results.Ok(tokens);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequest request,
        JwtTokenService tokenService,
        IAuditLogWriter auditLogWriter,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var response = await tokenService.RefreshAsync(request.RefreshToken, httpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (response is null)
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteModel(
                    DateTime.UtcNow,
                    null,
                    null,
                    "auth.refresh",
                    "RefreshToken",
                    "provided-token",
                    "Failure",
                    new { reason = "invalid_or_expired_or_revoked" }),
                cancellationToken);

            return Results.Unauthorized();
        }

        var actorUserId = Guid.TryParse(response.UserId, out var parsedUserId) ? parsedUserId : (Guid?)null;
        await auditLogWriter.WriteAsync(
            new AuditLogWriteModel(
                DateTime.UtcNow,
                actorUserId,
                response.Email,
                "auth.refresh",
                "User",
                response.UserId,
                "Success",
                new { ip = httpContext.Connection.RemoteIpAddress?.ToString() }),
            cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest request,
        JwtTokenService tokenService,
        IAuditLogWriter auditLogWriter,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var revoked = await tokenService.RevokeAsync(request.RefreshToken, cancellationToken);

        var actorIdClaim = httpContext.User.FindFirst("sub")?.Value;
        var actorUserId = Guid.TryParse(actorIdClaim, out var parsedActorId) ? parsedActorId : (Guid?)null;
        var actorEmail = httpContext.User.FindFirst("email")?.Value;

        await auditLogWriter.WriteAsync(
            new AuditLogWriteModel(
                DateTime.UtcNow,
                actorUserId,
                actorEmail,
                "auth.logout",
                "RefreshToken",
                "provided-token",
                revoked ? "Success" : "Failure"),
            cancellationToken);

        return revoked ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<int> ResolvePasswordMinLengthAsync(
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        const int fallback = 8;
        var value = await dbContext.SystemSettings
            .AsNoTracking()
            .Where(setting => setting.Key == SystemSettingKeys.AuthPasswordMinLength)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return value is not null && int.TryParse(value, out var parsed)
            ? Math.Clamp(parsed, 6, 128)
            : fallback;
    }
}
