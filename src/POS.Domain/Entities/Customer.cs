using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class Customer : EntityBase, IConcurrencyTracked
{
    public string Name { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public int LoyaltyPoints { get; set; }

    public decimal CumulativeSpend { get; set; }

    public bool IsActive { get; set; } = true;

    public long Version { get; set; } = 1;

    public ICollection<SalesOrder> SalesOrders { get; set; } = new List<SalesOrder>();
}
