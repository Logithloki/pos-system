using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions;
using POS.Application.Exceptions;
using POS.Application.Models;
using POS.Domain.Entities;
using POS.Infrastructure.Data;
using POS.Infrastructure.Printing;

namespace POS.Infrastructure.Services;

public sealed class ReceiptPrintService : IReceiptPrintService
{
    private readonly PosDbContext _dbContext;
    private readonly IReceiptPrinterGateway _printerGateway;
    private readonly IOptions<ReceiptPrintingOptions> _printingOptions;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<ReceiptPrintService> _logger;

    public ReceiptPrintService(
        PosDbContext dbContext,
        IReceiptPrinterGateway printerGateway,
        IOptions<ReceiptPrintingOptions> printingOptions,
        IAuditLogService auditLogService,
        ILogger<ReceiptPrintService> logger)
    {
        _dbContext = dbContext;
        _printerGateway = printerGateway;
        _printingOptions = printingOptions;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task PrintReceiptAsync(long receiptId, bool isReprint, CancellationToken cancellationToken = default)
    {
        var receipt = await _dbContext.Receipts.SingleOrDefaultAsync(x => x.Id == receiptId, cancellationToken);
        if (receipt is null)
        {
            throw new AppValidationException("Receipt not found.");
        }

        var maxAttempts = Math.Max(1, _printingOptions.Value.MaxRetryAttempts);
        var retryDelay = Math.Max(0, _printingOptions.Value.RetryDelayMilliseconds);

        for (var attempt = 1; attempt <= maxAttempts; attempt += 1)
        {
            try
            {
                await _printerGateway.PrintAsync(receipt.ReceiptNumber, receipt.ThermalPayload, cancellationToken);

                await LogPrintAttemptAsync(receipt.Id, attempt, isReprint, true, null, cancellationToken);
                await _auditLogService.WriteAsync(
                    new AuditLogEntry
                    {
                        Action = isReprint ? "ReceiptReprint" : "ReceiptPrint",
                        ResourceType = "Receipt",
                        ResourceId = receipt.Id.ToString(),
                        Status = "Success",
                        MetadataJson = $"{{\"attempt\":{attempt},\"receiptNumber\":\"{receipt.ReceiptNumber}\"}}",
                    },
                    cancellationToken);

                return;
            }
            catch (Exception ex)
            {
                await LogPrintAttemptAsync(receipt.Id, attempt, isReprint, false, ex.Message, cancellationToken);
                _logger.LogWarning(ex, "Receipt print failed for receipt {ReceiptNumber} on attempt {Attempt}.", receipt.ReceiptNumber, attempt);

                if (attempt == maxAttempts)
                {
                    await _auditLogService.WriteAsync(
                        new AuditLogEntry
                        {
                            Action = isReprint ? "ReceiptReprint" : "ReceiptPrint",
                            ResourceType = "Receipt",
                            ResourceId = receipt.Id.ToString(),
                            Status = "Failure",
                            ErrorMessage = ex.Message,
                        },
                        cancellationToken);

                    throw new AppValidationException($"Receipt print failed after {maxAttempts} attempts.");
                }

                if (retryDelay > 0)
                {
                    await Task.Delay(retryDelay, cancellationToken);
                }
            }
        }
    }

    public async Task ReprintByReceiptNumberAsync(string receiptNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber))
        {
            throw new AppValidationException("Receipt number is required.");
        }

        var receipt = await _dbContext.Receipts.SingleOrDefaultAsync(
            x => x.ReceiptNumber == receiptNumber.Trim(),
            cancellationToken);

        if (receipt is null)
        {
            throw new AppValidationException("Receipt not found.");
        }

        await PrintReceiptAsync(receipt.Id, isReprint: true, cancellationToken);
    }

    private async Task LogPrintAttemptAsync(
        long receiptId,
        int attempt,
        bool isReprint,
        bool isSuccessful,
        string? error,
        CancellationToken cancellationToken)
    {
        _dbContext.ReceiptPrintLogs.Add(
            new ReceiptPrintLog
            {
                ReceiptId = receiptId,
                AttemptNumber = attempt,
                IsReprint = isReprint,
                IsSuccessful = isSuccessful,
                ErrorMessage = error,
            });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
