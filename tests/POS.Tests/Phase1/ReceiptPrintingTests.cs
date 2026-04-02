using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Data;
using POS.Infrastructure.Printing;
using POS.Infrastructure.Services;

namespace POS.Tests.Phase1;

public sealed class ReceiptPrintingTests
{
    [Fact]
    public async Task PrintReceipt_Should_Retry_And_Log_Attempts()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new PosDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var user = new User
        {
            Username = "cashier",
            PasswordHash = "hash",
            FullName = "Cashier",
            Role = UserRole.Cashier,
            IsActive = true,
        };

        var order = new SalesOrder
        {
            ReceiptNumber = "R-0000000001",
            IdempotencyKey = "idem-print-1",
            User = user,
            OrderType = SalesOrderType.Sale,
            TotalBeforeDiscount = 20m,
            DiscountAmount = 0m,
            TaxAmount = 0m,
            TotalAfterTax = 20m,
            PaymentMethod = PaymentMethod.Cash,
            AmountTendered = 20m,
        };

        var receipt = new Receipt
        {
            SalesOrder = order,
            ReceiptNumber = "R-0000000001",
            ThermalPayload = "payload",
            PayloadHash = "hash",
        };

        context.Receipts.Add(receipt);
        await context.SaveChangesAsync();

        var gateway = new FlakyReceiptPrinterGateway(2);
        var service = new ReceiptPrintService(
            context,
            gateway,
            Options.Create(new ReceiptPrintingOptions { MaxRetryAttempts = 3, RetryDelayMilliseconds = 0 }),
            new AuditLogService(context),
            NullLogger<ReceiptPrintService>.Instance);

        await service.PrintReceiptAsync(receipt.Id, isReprint: false);

        Assert.Equal(3, gateway.Attempts);

        var attempts = await context.ReceiptPrintLogs
            .Where(x => x.ReceiptId == receipt.Id)
            .OrderBy(x => x.AttemptNumber)
            .ToListAsync();

        Assert.Equal(3, attempts.Count);
        Assert.False(attempts[0].IsSuccessful);
        Assert.False(attempts[1].IsSuccessful);
        Assert.True(attempts[2].IsSuccessful);
    }

    private sealed class FlakyReceiptPrinterGateway : IReceiptPrinterGateway
    {
        private int _remainingFailures;

        public FlakyReceiptPrinterGateway(int failuresBeforeSuccess)
        {
            _remainingFailures = failuresBeforeSuccess;
        }

        public int Attempts { get; private set; }

        public Task PrintAsync(string receiptNumber, string payload, CancellationToken cancellationToken = default)
        {
            Attempts += 1;

            if (_remainingFailures > 0)
            {
                _remainingFailures -= 1;
                throw new InvalidOperationException("Printer unavailable.");
            }

            return Task.CompletedTask;
        }
    }
}
