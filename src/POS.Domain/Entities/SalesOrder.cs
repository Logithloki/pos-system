using POS.Domain.Common;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

public sealed class SalesOrder : EntityBase, IConcurrencyTracked
{
    public string ReceiptNumber { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public long UserId { get; set; }

    public User? User { get; set; }

    public long? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public SalesOrderType OrderType { get; set; } = SalesOrderType.Sale;

    public long? OriginalSalesOrderId { get; set; }

    public SalesOrder? OriginalSalesOrder { get; set; }

    public ICollection<SalesOrder> ReversalOrders { get; set; } = new List<SalesOrder>();

    public decimal TotalBeforeDiscount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAfterTax { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public decimal AmountTendered { get; set; }

    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Completed;

    public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();

    public Receipt? Receipt { get; set; }

    public long Version { get; set; } = 1;
}
