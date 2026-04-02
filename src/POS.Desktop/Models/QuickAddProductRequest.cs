namespace POS.Desktop.Models;

public sealed class QuickAddProductRequest
{
    public required string Barcode { get; init; }

    public required string Name { get; init; }

    public decimal Price { get; init; }

    public decimal Cost { get; init; }

    public int QuantityOnHand { get; init; }
}
