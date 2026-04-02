namespace POS.Application.Abstractions;

public interface IReceiptPrintService
{
    Task PrintReceiptAsync(long receiptId, bool isReprint, CancellationToken cancellationToken = default);

    Task ReprintByReceiptNumberAsync(string receiptNumber, CancellationToken cancellationToken = default);
}
