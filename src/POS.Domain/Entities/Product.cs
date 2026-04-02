using POS.Domain.Common;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class Product : EntityBase, IConcurrencyTracked
{
    public string Name { get; set; } = string.Empty;

    public string Barcode { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal Cost { get; set; }

    public int QuantityOnHand { get; set; }

    public int ReorderLevel { get; set; }

    public long? SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public bool IsActive { get; set; } = true;

    public long Version { get; set; } = 1;

    public ICollection<SalesOrderLine> SalesOrderLines { get; set; } = new List<SalesOrderLine>();

    public void SetPrice(decimal value)
    {
        Price = Money.Round(value);
    }

    public void SetCost(decimal value)
    {
        Cost = Money.Round(value);
    }

    public void DeductStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Quantity must be greater than zero.");
        }

        if (QuantityOnHand < quantity)
        {
            throw new InvalidOperationException("Insufficient stock.");
        }

        QuantityOnHand -= quantity;
    }

    public void AddStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Quantity must be greater than zero.");
        }

        QuantityOnHand += quantity;
    }
}
