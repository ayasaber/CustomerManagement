namespace CustomerManagement.Api.Domain.Customers;

public sealed class CustomerInteractionEvent
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public InteractionChannel Channel { get; set; }

    public InteractionDirection Direction { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string? Summary { get; set; }

    public string? SourceRef { get; set; }

    public string? SourceSystem { get; set; }

    public DateTime ProjectedAtUtc { get; set; }

    public Customer Customer { get; set; } = null!;
}
