namespace POS.Application.Models;

public sealed class CheckoutResponse
{
    public long SalesOrderId { get; init; }

    public string ReceiptNumber { get; init; } = string.Empty;

    public decimal Subtotal { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal Total { get; init; }

    public decimal ChangeDue { get; init; }

    public bool IsIdempotentReplay { get; init; }
}
