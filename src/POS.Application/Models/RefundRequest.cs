namespace POS.Application.Models;

public sealed class RefundRequest
{
    public long SalesOrderId { get; init; }

    public long RequestedByUserId { get; init; }

    public string Reason { get; init; } = string.Empty;
}
