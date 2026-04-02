namespace POS.Infrastructure.Printing;

public sealed class ReceiptPrintingOptions
{
    public const string SectionName = "ReceiptPrinting";

    public string SpoolDirectory { get; set; } = string.Empty;

    public int MaxRetryAttempts { get; set; } = 3;

    public int RetryDelayMilliseconds { get; set; } = 500;
}
