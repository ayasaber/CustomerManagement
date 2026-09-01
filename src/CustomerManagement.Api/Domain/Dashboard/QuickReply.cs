using System.ComponentModel.DataAnnotations;
using CustomerManagement.Api.Domain.Security;

namespace CustomerManagement.Api.Domain.Dashboard;

public sealed class QuickReply
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string? TagsCsv { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid CreatedByUserId { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public ApplicationUser? UpdatedByUser { get; set; }
}
