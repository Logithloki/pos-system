using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

public sealed class RefundService : IRefundService
{
    private readonly PosDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<RefundService> _logger;

    public RefundService(PosDbContext dbContext, IAuditLogService auditLogService, ILogger<RefundService> logger)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<RefundResponse> CreateRefundAsync(RefundRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SalesOrderId <= 0)
        {
            throw new AppValidationException("A valid sales order ID is required.");
        }

        var requestedBy = await _dbContext.Users.SingleOrDefaultAsync(
            x => x.Id == request.RequestedByUserId && x.IsActive,
            cancellationToken);

        if (requestedBy is null)
        {
            throw new AppValidationException("Requesting user was not found.");
        }

        if (requestedBy.Role != UserRole.Admin)
        {
            throw new UnauthorizedAccessException("Only admins can create refunds.");
        }

        var existingReversal = await _dbContext.SalesOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(
            x => x.OriginalSalesOrderId == request.SalesOrderId && x.OrderType == SalesOrderType.Reversal,
            cancellationToken);

        if (existingReversal is not null)
        {
            return new RefundResponse
            {
                ReversalSalesOrderId = existingReversal.Id,
                ReversalReceiptNumber = existingReversal.ReceiptNumber,
                ReversalTotal = existingReversal.TotalAfterTax,
            };
        }

        var originalOrder = await _dbContext.SalesOrders
            .Include(x => x.Lines)
            .ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == request.SalesOrderId, cancellationToken);

        if (originalOrder is null)
        {
            throw new AppValidationException("Original sales order not found.");
        }

        if (originalOrder.OrderType != SalesOrderType.Sale)
        {
            throw new AppValidationException("Only completed sale orders can be reversed.");
        }

        if (originalOrder.Status != SalesOrderStatus.Completed)
        {
            throw new AppValidationException("Only completed sale orders can be reversed.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var reversal = new SalesOrder
            {
                ReceiptNumber = $"TMP-{Guid.NewGuid():N}",
                IdempotencyKey = $"refund-{originalOrder.Id}",
                UserId = requestedBy.Id,
                CustomerId = originalOrder.CustomerId,
                OrderType = SalesOrderType.Reversal,
                OriginalSalesOrderId = originalOrder.Id,
                TotalBeforeDiscount = -Money.Round(originalOrder.TotalBeforeDiscount),
                DiscountAmount = -Money.Round(originalOrder.DiscountAmount),
                TaxAmount = -Money.Round(originalOrder.TaxAmount),
                TotalAfterTax = -Money.Round(originalOrder.TotalAfterTax),
                PaymentMethod = originalOrder.PaymentMethod,
                AmountTendered = 0m,
                Status = SalesOrderStatus.Completed,
            };

            foreach (var originalLine in originalOrder.Lines)
            {
                var product = originalLine.Product ?? await _dbContext.Products.SingleAsync(
                    x => x.Id == originalLine.ProductId,
                    cancellationToken);

                var quantityToReturn = Math.Abs(originalLine.Quantity);
                var quantityBefore = product.QuantityOnHand;
                product.AddStock(quantityToReturn);

                reversal.Lines.Add(
                    new SalesOrderLine
                    {
                        ProductId = originalLine.ProductId,
                        Quantity = -quantityToReturn,
                        UnitPrice = Money.Round(originalLine.UnitPrice),
                        LineDiscountAmount = -Math.Abs(Money.Round(originalLine.LineDiscountAmount)),
                        LineTotal = -Math.Abs(Money.Round(originalLine.LineTotal)),
                    });

                _dbContext.InventoryAdjustments.Add(
                    new InventoryAdjustment
                    {
                        ProductId = product.Id,
                        UserId = requestedBy.Id,
                        QuantityDelta = quantityToReturn,
                        QuantityBefore = quantityBefore,
                        QuantityAfter = product.QuantityOnHand,
                        Reason = InventoryAdjustmentReason.Return,
                        Note = string.IsNullOrWhiteSpace(request.Reason) ? "Refund reversal" : request.Reason.Trim(),
                    });
            }

            _dbContext.SalesOrders.Add(reversal);
            await _dbContext.SaveChangesAsync(cancellationToken);

            reversal.ReceiptNumber = $"RR-{reversal.Id:0000000000}";

            var storeName = Environment.GetEnvironmentVariable("POS_STORE_NAME") ?? "POS Store";
            var renderedLines = originalOrder.Lines.Select(
                line =>
                {
                    var productName = line.Product?.Name ?? $"Product-{line.ProductId}";
                    var quantity = -Math.Abs(line.Quantity);
                    var lineTotal = -Math.Abs(line.LineTotal);
                    return new ReceiptRenderer.RenderLine(productName, quantity, line.UnitPrice, lineTotal);
                }).ToArray();

            var thermalPayload = ReceiptRenderer.Render(storeName, reversal, renderedLines);
            var receipt = new Receipt
            {
                SalesOrderId = reversal.Id,
                ReceiptNumber = reversal.ReceiptNumber,
                ThermalPayload = thermalPayload,
                PayloadHash = ComputeSha256(thermalPayload),
            };

            _dbContext.Receipts.Add(receipt);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            try
            {
                await _auditLogService.WriteAsync(
                    new AuditLogEntry
                    {
                        UserId = requestedBy.Id,
                        Action = "RefundReversalCreated",
                        ResourceType = "SalesOrder",
                        ResourceId = reversal.Id.ToString(),
                        Status = "Success",
                        MetadataJson = JsonSerializer.Serialize(
                            new
                            {
                                originalSalesOrderId = originalOrder.Id,
                                reason = request.Reason,
                            }),
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit log write failed for refund SalesOrder {SalesOrderId}. Audit trail may be incomplete.", reversal.Id);
            }

            return new RefundResponse
            {
                ReversalSalesOrderId = reversal.Id,
                ReversalReceiptNumber = reversal.ReceiptNumber,
                ReversalTotal = reversal.TotalAfterTax,
            };
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            _logger.LogWarning(ex, "Refund reversal failed due to concurrency conflict for sales order {SalesOrderId}.", request.SalesOrderId);
            throw new AppValidationException("A concurrent stock update prevented refund completion. Please retry.");
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            var replay = await _dbContext.SalesOrders
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.OriginalSalesOrderId == request.SalesOrderId && x.OrderType == SalesOrderType.Reversal,
                    cancellationToken);

            if (replay is not null)
            {
                return new RefundResponse
                {
                    ReversalSalesOrderId = replay.Id,
                    ReversalReceiptNumber = replay.ReceiptNumber,
                    ReversalTotal = replay.TotalAfterTax,
                };
            }

            _logger.LogError(ex, "Refund persistence failed for original sales order {SalesOrderId}.", request.SalesOrderId);
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

}
