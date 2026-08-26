namespace CustomerManagement.Api.Domain.Tickets;

public enum TicketStatus
{
    New = 1,
    InProgress = 2,
    WaitingOnCustomer = 3,
    Resolved = 4,
    Closed = 5
}
