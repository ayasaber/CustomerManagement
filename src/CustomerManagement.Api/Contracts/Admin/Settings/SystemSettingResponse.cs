namespace CustomerManagement.Api.Contracts.Admin.Settings;

public sealed record SystemSettingResponse(
    Guid Id,
    string Key,
    string Value,
    string? Description,
    DateTime UpdatedAtUtc,
    byte[] RowVersion);
