using System.ComponentModel.DataAnnotations;
using CustomerManagement.Api.Domain.Security;

namespace CustomerManagement.Api.Domain.Customers;

public sealed class Customer
{
    public Guid Id { get; set; }

    public Guid? ApplicationUserId { get; set; }

    public ApplicationUser? ApplicationUser { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Company { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public List<ContactDetail> ContactDetails { get; set; } = [];

    public List<CustomerNote> Notes { get; set; } = [];

    public List<CustomerAttachment> Attachments { get; set; } = [];

    public List<CustomerInteractionEvent> InteractionEvents { get; set; } = [];
}
