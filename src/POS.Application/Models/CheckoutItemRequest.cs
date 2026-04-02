namespace POS.Application.Models;

public sealed class CheckoutItemRequest
{
    public long? ProductId { get; init; }

    public string? Barcode { get; init; }

    public int Quantity { get; init; }
}
