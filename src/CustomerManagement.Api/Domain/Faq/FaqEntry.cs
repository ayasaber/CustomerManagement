using System.ComponentModel.DataAnnotations;

namespace CustomerManagement.Api.Domain.Faq;

public sealed class FaqEntry
{
    public Guid Id { get; set; }

    public string Topic { get; set; } = string.Empty;

    public string Question { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
