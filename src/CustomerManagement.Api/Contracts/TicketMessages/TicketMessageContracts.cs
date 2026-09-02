namespace CustomerManagement.Api.Contracts.TicketMessages;

public sealed record TicketMessageResponse(
    Guid Id,
    Guid TicketId,
    string SenderType,
    Guid SenderUserId,
    string SenderDisplayName,
    string Body,
    DateTime CreatedAtUtc,
    byte[] RowVersion);

public sealed record TicketMessageListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<TicketMessageResponse> Items);

public sealed record CreateTicketMessageRequest(
    Guid TicketId,
    string Body);

public sealed record CreateTicketMessageResponse(
    TicketMessageResponse Message);
