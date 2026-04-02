namespace POS.Desktop.Models;

public enum ReportPeriod
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
}

public sealed record InventoryProductItem(
    long Id,
    string Barcode,
    string Name,
    decimal Price,
    decimal Cost,
    int QuantityOnHand,
    int ReorderLevel,
    long? SupplierId,
    string SupplierName,
    bool IsActive)
{
    public bool IsLowStock => QuantityOnHand <= ReorderLevel;
}

public sealed record SupplierItem(
    long Id,
    string Name,
    string Contact,
    string Address,
    string? Phone,
    string? Email,
    bool IsActive,
    int ProductCount);

public sealed record CustomerItem(
    long Id,
    string Name,
    string Phone,
    string? Email,
    int LoyaltyPoints,
    decimal CumulativeSpend,
    bool IsActive,
    int PurchaseCount,
    DateTime? LastPurchaseUtc);

public sealed record CustomerPurchaseItem(
    long SalesOrderId,
    string ReceiptNumber,
    DateTime CreatedUtc,
    decimal Total,
    string PaymentMethod,
    string OrderType);

public sealed record TopProductItem(
    long ProductId,
    string ProductName,
    int QuantitySold,
    decimal Revenue,
    decimal Profit);

public sealed record ReportSummary(
    DateTime FromUtc,
    DateTime ToUtc,
    decimal TotalSales,
    decimal TotalProfit,
    int TransactionCount,
    decimal AverageSale,
    IReadOnlyCollection<TopProductItem> TopProducts);

public sealed record UserAdminItem(
    long Id,
    string Username,
    string FullName,
    string? Email,
    string Role,
    bool IsActive,
    DateTime? LastLoginUtc);

public sealed record BackupItem(
    string FilePath,
    DateTime CreatedUtc,
    bool IsAutomatic,
    string Status);

public sealed class ProductUpsertRequest
{
    public long? Id { get; init; }

    public required string Barcode { get; init; }

    public required string Name { get; init; }

    public decimal Price { get; init; }

    public decimal Cost { get; init; }

    public int QuantityOnHand { get; init; }

    public int ReorderLevel { get; init; }

    public long? SupplierId { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed class SupplierUpsertRequest
{
    public long? Id { get; init; }

    public required string Name { get; init; }

    public required string Contact { get; init; }

    public required string Address { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed class CustomerUpsertRequest
{
    public long? Id { get; init; }

    public required string Name { get; init; }

    public required string Phone { get; init; }

    public string? Email { get; init; }

    public int LoyaltyPoints { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed class CreateCashierRequest
{
    public required string Username { get; init; }

    public required string FullName { get; init; }

    public required string Password { get; init; }

    public string? Email { get; init; }
}
