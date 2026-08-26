namespace CustomerManagement.Api.Contracts.Admin.Settings;

public sealed record UpdateSystemSettingRequest(
    string Value,
    byte[] RowVersion);
