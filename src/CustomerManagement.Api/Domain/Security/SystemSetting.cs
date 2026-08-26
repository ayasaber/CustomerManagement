using System.ComponentModel.DataAnnotations;

namespace CustomerManagement.Api.Domain.Security;

public sealed class SystemSetting
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
