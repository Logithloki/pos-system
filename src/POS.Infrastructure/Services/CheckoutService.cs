using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions;
using POS.Application.Exceptions;
using POS.Application.Models;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Services;

public sealed class CheckoutService : ICheckoutService
{
    private readonly PosDbContext _dbContext;
    private readonly ISystemClock _clock;
    private readonly IAuditLogService _auditLogService;
    private readonly ICheckoutExecutionHook _checkoutExecutionHook;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        PosDbContext dbContext,
        ISystemClock clock,
        IAuditLogService auditLogService,
        ICheckoutExecutionHook checkoutExecutionHook,
        ILogger<CheckoutService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _auditLogService = auditLogService;
        _checkoutExecutionHook = checkoutExecutionHook;
        _logger = logger;
    }

    public async Task<CheckoutResponse> ProcessCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var existingOrder = await _dbContext.SalesOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);

        if (existingOrder is not null)
        {
            return BuildResponse(existingOrder, true);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.Id == request.UserId && x.IsActive, cancellationToken);
            if (user is null)
            {
                throw new AppValidationException("Cashier account is invalid or inactive.");
            }

            if (request.CustomerId.HasValue)
            {
                var customerExists = await _dbContext.Customers.AnyAsync(
                    x => x.Id == request.CustomerId.Value && x.IsActive,
                    cancellationToken);

                if (!customerExists)
                {
                    throw new AppValidationException("Customer not found.");
                }
            }

            var resolvedItems = await ResolveAndAggregateItemsAsync(request.Items, cancellationToken);
            var selectedItems = new List<SelectedItem>(resolvedItems.Count);
            var subtotal = Money.Zero;

            foreach (var item in resolvedItems)
            {
                var product = item.Product;

                if (product.QuantityOnHand < item.Quantity)
                {
                    throw new AppValidationException($"Insufficient stock for product: {product.Name}.");
                }

                var quantityBefore = product.QuantityOnHand;
                product.DeductStock(item.Quantity);

                var lineTotalMoney = Money.From(product.Price) * item.Quantity;
                subtotal += lineTotalMoney;

                selectedItems.Add(new SelectedItem(product, item.Quantity, lineTotalMoney.Amount));

                _dbContext.InventoryAdjustments.Add(
                    new InventoryAdjustment
                    {
                        ProductId = product.Id,
                        UserId = user.Id,
                        QuantityDelta = -item.Quantity,
                        QuantityBefore = quantityBefore,
                        QuantityAfter = product.QuantityOnHand,
                        Reason = InventoryAdjustmentReason.Other,
                        Note = "Checkout sale",
                    });
            }

            await _checkoutExecutionHook.OnAfterInventoryDeductionAsync(cancellationToken);

            var totalDiscount = ComputeDiscount(subtotal, request.DiscountAmount, request.DiscountPercent);
            var taxableTotal = subtotal - totalDiscount;
            var taxAmount = taxableTotal * (request.TaxRatePercent / 100m);
            var finalTotal = taxableTotal + taxAmount;

            var amountTendered = ResolveAmountTendered(request.PaymentMethod, request.AmountTendered, finalTotal.Amount);

            var order = new SalesOrder
            {
                ReceiptNumber = $"TMP-{Guid.NewGuid():N}",
                IdempotencyKey = request.IdempotencyKey.Trim(),
                UserId = user.Id,
                CustomerId = request.CustomerId,
                OrderType = SalesOrderType.Sale,
                TotalBeforeDiscount = subtotal.Amount,
                DiscountAmount = totalDiscount.Amount,
                TaxAmount = taxAmount.Amount,
                TotalAfterTax = finalTotal.Amount,
                PaymentMethod = request.PaymentMethod,
                AmountTendered = amountTendered,
                Status = SalesOrderStatus.Completed,
            };

            foreach (var item in selectedItems)
            {
                order.Lines.Add(
                    new SalesOrderLine
                    {
                        ProductId = item.Product.Id,
                        Quantity = item.Quantity,
                        UnitPrice = Money.Round(item.Product.Price),
                        LineDiscountAmount = 0m,
                        LineTotal = item.LineTotal,
                    });
            }

            _dbContext.SalesOrders.Add(order);
            await _dbContext.SaveChangesAsync(cancellationToken);

            order.ReceiptNumber = $"R-{order.Id:0000000000}";

            var storeName = Environment.GetEnvironmentVariable("POS_STORE_NAME") ?? "POS Store";
            var thermalPayload = ReceiptRenderer.Render(
                storeName,
                order,
                selectedItems
                    .Select(x => new ReceiptRenderer.RenderLine(x.Product.Name, x.Quantity, x.Product.Price, x.LineTotal))
                    .ToArray());

            var receipt = new Receipt
            {
                SalesOrderId = order.Id,
                ReceiptNumber = order.ReceiptNumber,
                ThermalPayload = thermalPayload,
                PayloadHash = ComputeSha256(thermalPayload),
            };

            _dbContext.Receipts.Add(receipt);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            await _auditLogService.WriteAsync(
                new AuditLogEntry
                {
                    UserId = user.Id,
                    Action = "CheckoutComplete",
                    ResourceType = "SalesOrder",
                    ResourceId = order.Id.ToString(),
                    Status = "Success",
                    MetadataJson = $"{{\"receiptNumber\":\"{order.ReceiptNumber}\",\"idempotencyKey\":\"{order.IdempotencyKey}\"}}",
                },
                cancellationToken);

            return BuildResponse(order, false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "Checkout failed due to a concurrency conflict for idempotency key {IdempotencyKey}.", request.IdempotencyKey);
            throw new AppValidationException("A concurrent operation changed inventory during checkout. Please retry.");
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            var replay = await _dbContext.SalesOrders
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);

            if (replay is not null)
            {
                return BuildResponse(replay, true);
            }

            _logger.LogError(ex, "Checkout persistence failed for idempotency key {IdempotencyKey}.", request.IdempotencyKey);
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void ValidateRequest(CheckoutRequest request)
    {
        if (request.UserId <= 0)
        {
            throw new AppValidationException("User is required.");
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new AppValidationException("Idempotency key is required.");
        }

        if (request.Items.Count == 0)
        {
            throw new AppValidationException("At least one item is required.");
        }

        if (request.DiscountAmount < 0)
        {
            throw new AppValidationException("Discount amount cannot be negative.");
        }

        if (request.DiscountPercent < 0 || request.DiscountPercent > 100)
        {
            throw new AppValidationException("Discount percent must be between 0 and 100.");
        }

        if (request.TaxRatePercent < 0)
        {
            throw new AppValidationException("Tax rate cannot be negative.");
        }
    }

    private async Task<IReadOnlyCollection<ResolvedCheckoutItem>> ResolveAndAggregateItemsAsync(
        IReadOnlyCollection<CheckoutItemRequest> items,
        CancellationToken cancellationToken)
    {
        var normalizedItems = new List<NormalizedCheckoutItem>(items.Count);
        var requestedProductIds = new HashSet<long>();
        var requestedBarcodes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            if (item.Quantity <= 0)
            {
                throw new AppValidationException("Item quantity must be greater than zero.");
            }

            if (item.ProductId.HasValue)
            {
                requestedProductIds.Add(item.ProductId.Value);
                normalizedItems.Add(new NormalizedCheckoutItem(item.ProductId.Value, null, item.Quantity));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(item.Barcode))
            {
                var barcode = item.Barcode.Trim();
                requestedBarcodes.Add(barcode);
                normalizedItems.Add(new NormalizedCheckoutItem(null, barcode, item.Quantity));
                continue;
            }

            throw new AppValidationException("Each item must include productId or barcode.");
        }

        var products = await _dbContext.Products
            .Where(x => requestedProductIds.Contains(x.Id) || requestedBarcodes.Contains(x.Barcode))
            .ToListAsync(cancellationToken);

        var productsById = products.ToDictionary(x => x.Id);
        var productsByBarcode = products.ToDictionary(x => x.Barcode, StringComparer.Ordinal);
        var aggregated = new Dictionary<long, AggregatedItem>();

        foreach (var item in normalizedItems)
        {
            Product? product;

            if (item.ProductId.HasValue)
            {
                productsById.TryGetValue(item.ProductId.Value, out product);
            }
            else
            {
                productsByBarcode.TryGetValue(item.Barcode!, out product);
            }

            if (product is null || !product.IsActive)
            {
                throw new AppValidationException("Product not found or inactive.");
            }

            if (aggregated.TryGetValue(product.Id, out var existing))
            {
                existing.Quantity += item.Quantity;
            }
            else
            {
                aggregated[product.Id] = new AggregatedItem(product, item.Quantity);
            }
        }

        return aggregated
            .Values
            .Select(x => new ResolvedCheckoutItem(x.Product, x.Quantity))
            .ToArray();
    }

    private static Money ComputeDiscount(Money subtotal, decimal discountAmountInput, decimal discountPercent)
    {
        var discountFromAmount = Money.From(discountAmountInput);
        var discountFromPercent = subtotal * (discountPercent / 100m);
        var discount = discountFromAmount + discountFromPercent;

        if (discount.Amount > subtotal.Amount)
        {
            return subtotal;
        }

        return discount;
    }

    private static decimal ResolveAmountTendered(PaymentMethod paymentMethod, decimal amountTenderedInput, decimal finalTotal)
    {
        var amountTendered = Money.Round(amountTenderedInput);

        if (paymentMethod == PaymentMethod.Credit && amountTendered <= 0)
        {
            amountTendered = finalTotal;
        }

        if (amountTendered < finalTotal)
        {
            throw new AppValidationException("Tendered amount cannot be less than total due.");
        }

        return amountTendered;
    }

    private static CheckoutResponse BuildResponse(SalesOrder order, bool isReplay)
    {
        var total = Money.Round(order.TotalAfterTax);
        var tendered = Money.Round(order.AmountTendered);
        var changeDue = Money.Round(Math.Max(0m, tendered - total));

        return new CheckoutResponse
        {
            SalesOrderId = order.Id,
            ReceiptNumber = order.ReceiptNumber,
            Subtotal = Money.Round(order.TotalBeforeDiscount),
            DiscountAmount = Money.Round(order.DiscountAmount),
            TaxAmount = Money.Round(order.TaxAmount),
            Total = total,
            ChangeDue = changeDue,
            IsIdempotentReplay = isReplay,
        };
    }

    private static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private sealed record SelectedItem(Product Product, int Quantity, decimal LineTotal);

    private sealed record NormalizedCheckoutItem(long? ProductId, string? Barcode, int Quantity);

    private sealed record ResolvedCheckoutItem(Product Product, int Quantity);

    private sealed class AggregatedItem
    {
        public AggregatedItem(Product product, int quantity)
        {
            Product = product;
            Quantity = quantity;
        }

        public Product Product { get; }

        public int Quantity { get; set; }
    }
}
