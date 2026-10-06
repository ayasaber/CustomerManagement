namespace CustomerManagement.Api.Domain.Feedback;

public sealed class Feedback
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Domain.Customers.Customer Customer { get; set; } = null!;
}
