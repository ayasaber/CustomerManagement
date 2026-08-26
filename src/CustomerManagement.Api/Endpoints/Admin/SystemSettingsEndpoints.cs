using System.Security.Claims;
using CustomerManagement.Api.Contracts.Admin.Settings;
using CustomerManagement.Api.Infrastructure.Auditing;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.Admin;

public static class SystemSettingsEndpoints
{
    private const int MaxValueLength = 2000;

    public static IEndpointRouteBuilder MapSystemSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/system-settings")
            .RequireAuthorization("Permission:" + Permissions.SettingsManage)
            .WithTags("Admin.SystemSettings");

        group.MapGet("", ListSettingsAsync)
            .WithName("ListSystemSettings")
            .WithSummary("List editable system settings");

        group.MapGet("/{key}", GetSettingByKeyAsync)
            .WithName("GetSystemSettingByKey")
            .WithSummary("Get a system setting by key");

        group.MapPut("/{key}", UpdateSettingByKeyAsync)
            .WithName("UpdateSystemSettingByKey")
            .WithSummary("Update a system setting with optimistic concurrency");

        return app;
    }

    private static async Task<IResult> ListSettingsAsync(
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.SystemSettings
            .AsNoTracking()
            .OrderBy(setting => setting.Key)
            .Select(setting => new SystemSettingResponse(
                setting.Id,
                setting.Key,
                setting.Value,
                setting.Description,
                setting.UpdatedAtUtc,
                setting.RowVersion))
            .ToListAsync(cancellationToken);

        return Results.Ok(items);
    }

    private static async Task<IResult> GetSettingByKeyAsync(
        string key,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeKey(key);
        var setting = await dbContext.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Key == normalizedKey, cancellationToken);

        if (setting is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new SystemSettingResponse(
            setting.Id,
            setting.Key,
            setting.Value,
            setting.Description,
            setting.UpdatedAtUtc,
            setting.RowVersion));
    }

    private static async Task<IResult> UpdateSettingByKeyAsync(
        string key,
        [FromBody] UpdateSystemSettingRequest request,
        CustomerManagementDbContext dbContext,
        IAuditLogWriter auditLogWriter,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var errors = ValidateRequest(key, request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var normalizedKey = NormalizeKey(key);
        var setting = await dbContext.SystemSettings
            .FirstOrDefaultAsync(row => row.Key == normalizedKey, cancellationToken);

        if (setting is null)
        {
            return Results.NotFound();
        }

        var normalizedValue = request.Value.Trim();
        if (!TryValidateSettingValue(normalizedKey, normalizedValue, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Value)] = [validationError]
            });
        }

        var oldValue = setting.Value;
        setting.Value = normalizedValue;
        setting.UpdatedAtUtc = DateTime.UtcNow;
        setting.UpdatedByUserId = TryParseActorId(httpContext.User);
        setting.RowVersion = Guid.NewGuid().ToByteArray();

        dbContext.Entry(setting).Property(row => row.RowVersion).OriginalValue = request.RowVersion;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "Setting was updated by another request. Refresh and retry with the latest RowVersion.",
                Status = StatusCodes.Status409Conflict
            });
        }

        await auditLogWriter.WriteAsync(
            new AuditLogWriteModel(
                DateTime.UtcNow,
                TryParseActorId(httpContext.User),
                httpContext.User.FindFirstValue("email"),
                "admin.system-setting.update",
                "SystemSetting",
                normalizedKey,
                "Success",
                new
                {
                    key = normalizedKey,
                    oldValue,
                    newValue = normalizedValue
                }),
            cancellationToken);

        return Results.Ok(new SystemSettingResponse(
            setting.Id,
            setting.Key,
            setting.Value,
            setting.Description,
            setting.UpdatedAtUtc,
            setting.RowVersion));
    }

    private static Dictionary<string, string[]> ValidateRequest(string key, UpdateSystemSettingRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(key))
        {
            errors["key"] = ["Key is required."];
        }

        if (request is null)
        {
            errors["request"] = ["Request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Value))
        {
            errors[nameof(request.Value)] = ["Value is required."];
        }
        else if (request.Value.Trim().Length > MaxValueLength)
        {
            errors[nameof(request.Value)] = [$"Value must be {MaxValueLength} characters or fewer."];
        }

        if (request.RowVersion is null)
        {
            errors[nameof(request.RowVersion)] = ["RowVersion is required."];
        }

        return errors;
    }

    private static string NormalizeKey(string key)
    {
        return key.Trim();
    }

    private static Guid? TryParseActorId(ClaimsPrincipal user)
    {
        var actorId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(actorId, out var actorGuid) ? actorGuid : null;
    }

    private static bool TryValidateSettingValue(string key, string value, out string error)
    {
        error = string.Empty;

        if (key == SystemSettingKeys.AuthAccessTokenMinutes)
        {
            if (!int.TryParse(value, out var parsed) || parsed is < 1 or > 1440)
            {
                error = "Auth.AccessTokenMinutes must be an integer between 1 and 1440.";
                return false;
            }

            return true;
        }

        if (key == SystemSettingKeys.AuthRefreshTokenDays)
        {
            if (!int.TryParse(value, out var parsed) || parsed is < 1 or > 365)
            {
                error = "Auth.RefreshTokenDays must be an integer between 1 and 365.";
                return false;
            }

            return true;
        }

        if (key == SystemSettingKeys.AuthPasswordMinLength)
        {
            if (!int.TryParse(value, out var parsed) || parsed is < 6 or > 128)
            {
                error = "Auth.Password.MinLength must be an integer between 6 and 128.";
                return false;
            }

            return true;
        }

        return true;
    }
}
