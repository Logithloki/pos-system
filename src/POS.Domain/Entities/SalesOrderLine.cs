using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class SalesOrderLine : EntityBase
{
    public long SalesOrderId { get; set; }

    public SalesOrder? SalesOrder { get; set; }

    public long ProductId { get; set; }

    public Product? Product { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineDiscountAmount { get; set; }

    public decimal LineTotal { get; set; }
}
