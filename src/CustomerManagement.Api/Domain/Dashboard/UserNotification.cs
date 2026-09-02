using CustomerManagement.Api.Domain.Security;

namespace CustomerManagement.Api.Domain.Dashboard;

public sealed class UserNotification
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public Guid RecipientUserId { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ReadAtUtc { get; set; }

    public ApplicationUser RecipientUser { get; set; } = null!;
}
