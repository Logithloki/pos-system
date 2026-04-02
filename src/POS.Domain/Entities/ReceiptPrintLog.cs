using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class ReceiptPrintLog : EntityBase
{
    public long ReceiptId { get; set; }

    public Receipt? Receipt { get; set; }

    public int AttemptNumber { get; set; }

    public bool IsReprint { get; set; }

    public bool IsSuccessful { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime PrintedUtc { get; set; } = DateTime.UtcNow;
}
