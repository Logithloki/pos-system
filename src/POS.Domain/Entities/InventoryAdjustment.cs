using POS.Domain.Common;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

public sealed class InventoryAdjustment : EntityBase
{
    public long ProductId { get; set; }

    public Product? Product { get; set; }

    public long UserId { get; set; }

    public User? User { get; set; }

    public int QuantityDelta { get; set; }

    public int QuantityBefore { get; set; }

    public int QuantityAfter { get; set; }

    public InventoryAdjustmentReason Reason { get; set; }

    public string? Note { get; set; }
}
