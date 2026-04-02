using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions;
using POS.Application.Exceptions;
using POS.Application.Models;
using POS.Desktop.Models;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Data;

namespace POS.Desktop.Services;

public sealed class CashierTerminalService : ICashierTerminalService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public CashierTerminalService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<TerminalBootstrapData> InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);

        var operatorUser = await dbContext.Users
            .Where(x => x.IsActive)
            .OrderBy(x => x.Role == UserRole.Cashier ? 0 : 1)
            .ThenBy(x => x.Id)
            .Select(
                x => new
                {
                    x.Id,
                    x.FullName,
                    x.Username,
                    x.Role,
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (operatorUser is null)
        {
            throw new AppValidationException("No active users are available. Create at least one user before opening checkout.");
        }

        var products = await dbContext.Products
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new CatalogProduct(x.Id, x.Barcode, x.Name, x.Price, x.Cost))
            .ToListAsync(cancellationToken);

        var taxRate = ResolveDefaultTaxRatePercent();

        return new TerminalBootstrapData
        {
            OperatorUserId = operatorUser.Id,
            OperatorDisplayName = string.IsNullOrWhiteSpace(operatorUser.FullName) ? operatorUser.Username : operatorUser.FullName,
            OperatorRole = operatorUser.Role,
            Products = products,
            DefaultTaxRatePercent = taxRate,
        };
    }

    public async Task<CatalogProduct> QuickAddProductAsync(QuickAddProductRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Barcode))
        {
            throw new AppValidationException("Barcode is required for quick add.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AppValidationException("Product name is required.");
        }

        if (request.Price <= 0)
        {
            throw new AppValidationException("Price must be greater than zero.");
        }

        if (request.Cost < 0)
        {
            throw new AppValidationException("Cost cannot be negative.");
        }

        if (request.QuantityOnHand < 0)
        {
            throw new AppValidationException("Opening stock cannot be negative.");
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        var normalizedBarcode = request.Barcode.Trim();
        var normalizedName = request.Name.Trim();

        var exists = await dbContext.Products.AnyAsync(x => x.Barcode == normalizedBarcode, cancellationToken);
        if (exists)
        {
            throw new AppValidationException("Barcode already exists. Scan again to add the existing product.");
        }

        var product = new Product
        {
            Barcode = normalizedBarcode,
            Name = normalizedName,
            QuantityOnHand = request.QuantityOnHand,
            ReorderLevel = 2,
            IsActive = true,
        };

        product.SetPrice(request.Price);
        product.SetCost(request.Cost);

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CatalogProduct(product.Id, product.Barcode, product.Name, Money.Round(product.Price), Money.Round(product.Cost));
    }

    public async Task<CheckoutResponse> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var checkoutService = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
        return await checkoutService.ProcessCheckoutAsync(request, cancellationToken);
    }

    public async Task PrintReceiptAsync(string receiptNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber))
        {
            throw new AppValidationException("Receipt number is required for printing.");
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        var receiptPrintService = scope.ServiceProvider.GetRequiredService<IReceiptPrintService>();

        var normalizedReceipt = receiptNumber.Trim();
        var receipt = await dbContext.Receipts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ReceiptNumber == normalizedReceipt, cancellationToken);

        if (receipt is null)
        {
            throw new AppValidationException($"Receipt {normalizedReceipt} was not found.");
        }

        await receiptPrintService.PrintReceiptAsync(receipt.Id, isReprint: false, cancellationToken);
    }

    private static decimal ResolveDefaultTaxRatePercent()
    {
        var environmentValue = Environment.GetEnvironmentVariable("POS_TAX_RATE_PERCENT");
        if (decimal.TryParse(environmentValue, out var parsed) && parsed >= 0)
        {
            return Math.Round(parsed, 2, MidpointRounding.ToEven);
        }

        return 5m;
    }
}
