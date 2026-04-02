using Microsoft.Extensions.Options;

namespace POS.Infrastructure.Printing;

public sealed class FileReceiptPrinterGateway : IReceiptPrinterGateway
{
    private readonly IOptions<ReceiptPrintingOptions> _options;

    public FileReceiptPrinterGateway(IOptions<ReceiptPrintingOptions> options)
    {
        _options = options;
    }

    public async Task PrintAsync(string receiptNumber, string payload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber))
        {
            throw new InvalidOperationException("Receipt number is required for printing.");
        }

        var spoolDirectory = ResolveSpoolDirectory();
        Directory.CreateDirectory(spoolDirectory);

        var sanitizedReceiptNumber = string.Join(string.Empty, receiptNumber.Where(char.IsLetterOrDigit));
        if (string.IsNullOrWhiteSpace(sanitizedReceiptNumber))
        {
            sanitizedReceiptNumber = "receipt";
        }

        var fileName = $"{sanitizedReceiptNumber}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.txt";
        var filePath = Path.Combine(spoolDirectory, fileName);

        await File.WriteAllTextAsync(filePath, payload, cancellationToken);
    }

    private string ResolveSpoolDirectory()
    {
        var configuredPath = _options.Value.SpoolDirectory;

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "POS", "spool", "receipts");
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(basePath, "POS", configuredPath);
    }
}
