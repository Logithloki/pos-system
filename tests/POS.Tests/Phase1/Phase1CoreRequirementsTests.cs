using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Application.Abstractions;
using POS.Application.Models;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Data;
using POS.Infrastructure.Services;

namespace POS.Tests.Phase1;

public sealed class Phase1CoreRequirementsTests
{
    [Theory]
    [InlineData(10.005, 10.00)]
    [InlineData(10.015, 10.02)]
    [InlineData(5.555, 5.56)]
    [InlineData(5.545, 5.54)]
    public void Money_Should_Use_Decimal_And_BankersRounding(decimal input, decimal expected)
    {
        var actual = Money.Round(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Receipt_Should_Be_Immutable()
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
            Username = "admin",
            PasswordHash = "hash",
            FullName = "Admin",
            Role = UserRole.Admin,
        };

        var order = new SalesOrder
        {
            ReceiptNumber = "R-0000000001",
            IdempotencyKey = "key-1",
            User = user,
            OrderType = SalesOrderType.Sale,
            TotalBeforeDiscount = 10m,
            DiscountAmount = 0m,
            TaxAmount = 0m,
            TotalAfterTax = 10m,
            PaymentMethod = PaymentMethod.Cash,
            AmountTendered = 10m,
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

        receipt.ThermalPayload = "tampered";

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Checkout_Should_Be_Idempotent_And_Deduct_Stock_Once()
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

        var product = new Product
        {
            Name = "Coffee",
            Barcode = "1234567890123",
            Price = 12.50m,
            Cost = 7.40m,
            QuantityOnHand = 10,
            ReorderLevel = 2,
            IsActive = true,
        };

        context.Users.Add(user);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var audit = new AuditLogService(context);
        var checkoutService = new CheckoutService(
            context,
            new TestClock(),
            audit,
            new NoOpCheckoutExecutionHook(),
            NullLogger<CheckoutService>.Instance);

        var request = new CheckoutRequest
        {
            UserId = user.Id,
            IdempotencyKey = "checkout-key-1",
            TaxRatePercent = 5m,
            PaymentMethod = PaymentMethod.Cash,
            AmountTendered = 100m,
            Items =
            [
                new CheckoutItemRequest
                {
                    ProductId = product.Id,
                    Quantity = 2,
                },
            ],
        };

        var first = await checkoutService.ProcessCheckoutAsync(request);
        var second = await checkoutService.ProcessCheckoutAsync(request);

        Assert.False(first.IsIdempotentReplay);
        Assert.True(second.IsIdempotentReplay);

        var reloadedProduct = await context.Products.SingleAsync(x => x.Id == product.Id);
        Assert.Equal(8, reloadedProduct.QuantityOnHand);

        var orderCount = await context.SalesOrders.CountAsync();
        Assert.Equal(1, orderCount);
    }

    private sealed class TestClock : POS.Application.Abstractions.ISystemClock
    {
        public DateTime UtcNow => new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
    }

    private sealed class NoOpCheckoutExecutionHook : ICheckoutExecutionHook
    {
        public Task OnAfterInventoryDeductionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
