using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Domain.Customers;
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

        if (normalizedAccountType == AuthRoles.Customer)
        {
            var registrationErrors = ValidateCustomerRegistrationProfile(request);
            if (registrationErrors.Count > 0)
            {
                return Results.ValidationProblem(registrationErrors);
            }
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

        if (normalizedAccountType == AuthRoles.Customer)
        {
            var nowUtc = DateTime.UtcNow;
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                ApplicationUserId = user.Id,
                Name = request.FullName!.Trim(),
                Company = request.Company!.Trim(),
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };

            foreach (var detail in request.ContactDetails!)
            {
                customer.ContactDetails.Add(new ContactDetail
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    Channel = (ContactChannel)detail.Channel,
                    Value = detail.Value.Trim(),
                    Label = string.IsNullOrWhiteSpace(detail.Label) ? null : detail.Label.Trim(),
                    IsPrimary = detail.IsPrimary,
                    CreatedAtUtc = nowUtc
                });
            }

            dbContext.Customers.Add(customer);
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

    private static Dictionary<string, string[]> ValidateCustomerRegistrationProfile(RegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            errors[nameof(request.FullName)] = ["FullName is required for customer registration."];
        }
        else if (request.FullName.Trim().Length > 200)
        {
            errors[nameof(request.FullName)] = ["FullName must be 200 characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(request.Company))
        {
            errors[nameof(request.Company)] = ["Company is required for customer registration."];
        }
        else if (request.Company.Trim().Length > 200)
        {
            errors[nameof(request.Company)] = ["Company must be 200 characters or fewer."];
        }

        if (request.ContactDetails is null || request.ContactDetails.Count == 0)
        {
            errors[nameof(request.ContactDetails)] = ["At least one primary contact is required for customer registration."];
            return errors;
        }

        var primaryByChannel = new Dictionary<int, int>();
        var hasPrimaryEmailOrPhone = false;

        for (var i = 0; i < request.ContactDetails.Count; i++)
        {
            var detail = request.ContactDetails[i];
            var prefix = $"{nameof(request.ContactDetails)}[{i}]";

            if (!Enum.IsDefined(typeof(ContactChannel), detail.Channel))
            {
                errors[$"{prefix}.Channel"] = ["Channel is invalid."];
            }

            if (string.IsNullOrWhiteSpace(detail.Value))
            {
                errors[$"{prefix}.Value"] = ["Value is required."];
            }
            else if (detail.Value.Trim().Length > 320)
            {
                errors[$"{prefix}.Value"] = ["Value must be 320 characters or fewer."];
            }

            if (!string.IsNullOrWhiteSpace(detail.Label) && detail.Label.Trim().Length > 100)
            {
                errors[$"{prefix}.Label"] = ["Label must be 100 characters or fewer."];
            }

            if (!detail.IsPrimary)
            {
                continue;
            }

            primaryByChannel.TryGetValue(detail.Channel, out var count);
            primaryByChannel[detail.Channel] = count + 1;

            if (detail.Channel is (int)ContactChannel.Email or (int)ContactChannel.Phone)
            {
                hasPrimaryEmailOrPhone = true;
            }
        }

        foreach (var pair in primaryByChannel.Where(pair => pair.Value > 1))
        {
            errors[$"{nameof(request.ContactDetails)}.Channel.{pair.Key}.Primary"] = ["Only one primary contact is allowed per channel."];
        }

        if (!hasPrimaryEmailOrPhone)
        {
            errors[nameof(request.ContactDetails)] = ["At least one primary Email or Phone contact is required."];
        }

        return errors;
    }
}
