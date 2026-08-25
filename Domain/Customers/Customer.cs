namespace CustomerManagement.Api.Domain.Customers;

public sealed class Customer
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Company { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<ContactDetail> ContactDetails { get; set; } = [];
}
