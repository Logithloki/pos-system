using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class Receipt : EntityBase, IConcurrencyTracked
{
    public long SalesOrderId { get; set; }

    public SalesOrder? SalesOrder { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public string ThermalPayload { get; set; } = string.Empty;

    public string PayloadHash { get; set; } = string.Empty;

    public long Version { get; set; } = 1;

    public ICollection<ReceiptPrintLog> PrintLogs { get; set; } = new List<ReceiptPrintLog>();
}
