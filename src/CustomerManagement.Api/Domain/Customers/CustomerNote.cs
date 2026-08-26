namespace CustomerManagement.Api.Domain.Customers;

public sealed class CustomerNote
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public string Body { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public Customer Customer { get; set; } = null!;
}
