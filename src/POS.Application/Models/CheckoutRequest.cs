using POS.Domain.Enums;

namespace POS.Application.Models;

public sealed class CheckoutRequest
{
    public long UserId { get; init; }

    public long? CustomerId { get; init; }

    public string IdempotencyKey { get; init; } = string.Empty;

    public decimal DiscountAmount { get; init; }

    public decimal DiscountPercent { get; init; }

    public decimal TaxRatePercent { get; init; }

    public PaymentMethod PaymentMethod { get; init; }

    public decimal AmountTendered { get; init; }

    public IReadOnlyCollection<CheckoutItemRequest> Items { get; init; } = Array.Empty<CheckoutItemRequest>();
}
