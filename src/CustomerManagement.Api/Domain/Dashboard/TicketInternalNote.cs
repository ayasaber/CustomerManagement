using System.ComponentModel.DataAnnotations;
using CustomerManagement.Api.Domain.Security;
using CustomerManagement.Api.Domain.Tickets;

namespace CustomerManagement.Api.Domain.Dashboard;

public sealed class TicketInternalNote
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }

    public Guid AuthorUserId { get; set; }

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public Ticket Ticket { get; set; } = null!;

    public ApplicationUser AuthorUser { get; set; } = null!;

    public List<TicketInternalNoteMention> Mentions { get; set; } = [];
}
