namespace POS.Infrastructure.Printing;

public interface IReceiptPrinterGateway
{
    Task PrintAsync(string receiptNumber, string payload, CancellationToken cancellationToken = default);
}
