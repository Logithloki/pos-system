namespace POS.Application.Models;

public sealed class RefundResponse
{
    public long ReversalSalesOrderId { get; init; }

    public string ReversalReceiptNumber { get; init; } = string.Empty;

    public decimal ReversalTotal { get; init; }
}
