using System.ComponentModel.DataAnnotations;
using CustomerManagement.Api.Domain.Security;

namespace CustomerManagement.Api.Domain.Tickets;

public sealed class TicketMessage
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }

    public TicketMessageSenderType SenderType { get; set; }

    public Guid SenderUserId { get; set; }

    public string SenderDisplayName { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public Ticket Ticket { get; set; } = null!;

    public ApplicationUser SenderUser { get; set; } = null!;
}
