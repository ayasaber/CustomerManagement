namespace CustomerManagement.Api.Domain.Customers;

public sealed class ContactDetail
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public ContactChannel Channel { get; set; }

    public string Value { get; set; } = string.Empty;

    public string? Label { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Customer Customer { get; set; } = null!;
}
