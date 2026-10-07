using System.ComponentModel.DataAnnotations;

namespace CustomerManagement.Api.Domain.KnowledgeBase;

public sealed class Guide
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public List<GuideStep> Steps { get; set; } = [];
}
