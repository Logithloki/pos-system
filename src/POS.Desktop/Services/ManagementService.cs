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

public sealed class ManagementService : IManagementService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOperatorSessionContext _operatorSessionContext;

    public ManagementService(IServiceScopeFactory scopeFactory, IOperatorSessionContext operatorSessionContext)
    {
        _scopeFactory = scopeFactory;
        _operatorSessionContext = operatorSessionContext;
    }

    public async Task<IReadOnlyCollection<InventoryProductItem>> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        return await db.Products
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(
                x => new InventoryProductItem(
                    x.Id,
                    x.Barcode,
                    x.Name,
                    x.Price,
                    x.Cost,
                    x.QuantityOnHand,
                    x.ReorderLevel,
                    x.SupplierId,
                    x.Supplier != null ? x.Supplier.Name : string.Empty,
                    x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<InventoryProductItem> SaveProductAsync(ProductUpsertRequest request, long operatorUserId, CancellationToken cancellationToken = default)
    {
        var currentUserId = EnsureAdminAccess();

        if (string.IsNullOrWhiteSpace(request.Barcode))
        {
            throw new AppValidationException("Barcode is required.");
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
            throw new AppValidationException("Stock cannot be negative.");
        }

        if (request.ReorderLevel < 0)
        {
            throw new AppValidationException("Reorder level cannot be negative.");
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        var barcode = request.Barcode.Trim();
        var name = request.Name.Trim();
        var requestId = request.Id ?? -1;

        var barcodeExists = await db.Products
            .AnyAsync(x => x.Barcode == barcode && x.Id != requestId, cancellationToken);

        if (barcodeExists)
        {
            throw new AppValidationException("A product with this barcode already exists.");
        }

        if (request.SupplierId is long supplierId)
        {
            var supplierExists = await db.Suppliers.AnyAsync(x => x.Id == supplierId, cancellationToken);
            if (!supplierExists)
            {
                throw new AppValidationException("Selected supplier does not exist.");
            }
        }

        Product product;
        var isNew = !request.Id.HasValue;

        if (isNew)
        {
            product = new Product();
            db.Products.Add(product);
        }
        else
        {
            var existingProductId = request.Id ?? 0;
            product = await db.Products.SingleOrDefaultAsync(x => x.Id == existingProductId, cancellationToken)
                ?? throw new AppValidationException("Product not found.");
        }

        var previousStock = product.QuantityOnHand;

        product.Barcode = barcode;
        product.Name = name;
        product.SetPrice(request.Price);
        product.SetCost(request.Cost);
        product.QuantityOnHand = request.QuantityOnHand;
        product.ReorderLevel = request.ReorderLevel;
        product.SupplierId = request.SupplierId;
        product.IsActive = request.IsActive;

        if (currentUserId > 0 && previousStock != product.QuantityOnHand)
        {
            db.InventoryAdjustments.Add(
                new InventoryAdjustment
                {
                    Product = product,
                    UserId = currentUserId,
                    QuantityBefore = previousStock,
                    QuantityAfter = product.QuantityOnHand,
                    QuantityDelta = product.QuantityOnHand - previousStock,
                    Reason = InventoryAdjustmentReason.CountVariance,
                    Note = isNew ? "Initial stock" : "Product update",
                });
        }

        await db.SaveChangesAsync(cancellationToken);

        var supplierName = string.Empty;
        if (product.SupplierId.HasValue)
        {
            supplierName = await db.Suppliers
                .Where(x => x.Id == product.SupplierId.Value)
                .Select(x => x.Name)
                .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        }

        return new InventoryProductItem(
            product.Id,
            product.Barcode,
            product.Name,
            product.Price,
            product.Cost,
            product.QuantityOnHand,
            product.ReorderLevel,
            product.SupplierId,
            supplierName,
            product.IsActive);
    }

    public async Task SetProductActiveAsync(long productId, bool isActive, CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new AppValidationException("Product not found.");

        product.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AdjustStockAsync(long productId, int quantityDelta, string note, long operatorUserId, CancellationToken cancellationToken = default)
    {
        var currentUserId = EnsureAdminAccess();

        if (quantityDelta == 0)
        {
            throw new AppValidationException("Stock adjustment cannot be zero.");
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new AppValidationException("Product not found.");

        var before = product.QuantityOnHand;
        var after = before + quantityDelta;

        if (after < 0)
        {
            throw new AppValidationException("Adjustment would result in negative stock.");
        }

        product.QuantityOnHand = after;

        db.InventoryAdjustments.Add(
            new InventoryAdjustment
            {
                ProductId = product.Id,
                UserId = currentUserId,
                QuantityBefore = before,
                QuantityAfter = after,
                QuantityDelta = quantityDelta,
                Reason = quantityDelta > 0 ? InventoryAdjustmentReason.StockIn : InventoryAdjustmentReason.CountVariance,
                Note = string.IsNullOrWhiteSpace(note) ? "Manual adjustment" : note.Trim(),
            });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<SupplierItem>> GetSuppliersAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        return await db.Suppliers
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(
                x => new SupplierItem(
                    x.Id,
                    x.Name,
                    x.Contact,
                    x.Address,
                    x.Phone,
                    x.Email,
                    x.IsActive,
                    x.Products.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<SupplierItem> SaveSupplierAsync(SupplierUpsertRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AppValidationException("Supplier name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Contact))
        {
            throw new AppValidationException("Supplier contact is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            throw new AppValidationException("Supplier address is required.");
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        Supplier supplier;

        if (!request.Id.HasValue)
        {
            supplier = new Supplier();
            db.Suppliers.Add(supplier);
        }
        else
        {
            supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken)
                ?? throw new AppValidationException("Supplier not found.");
        }

        supplier.Name = request.Name.Trim();
        supplier.Contact = request.Contact.Trim();
        supplier.Address = request.Address.Trim();
        supplier.Phone = request.Phone?.Trim();
        supplier.Email = request.Email?.Trim();
        supplier.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        var productCount = await db.Products.CountAsync(x => x.SupplierId == supplier.Id, cancellationToken);

        return new SupplierItem(
            supplier.Id,
            supplier.Name,
            supplier.Contact,
            supplier.Address,
            supplier.Phone,
            supplier.Email,
            supplier.IsActive,
            productCount);
    }

    public async Task SetSupplierActiveAsync(long supplierId, bool isActive, CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == supplierId, cancellationToken)
            ?? throw new AppValidationException("Supplier not found.");

        supplier.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CustomerItem>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        return await db.Customers
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(
                x => new CustomerItem(
                    x.Id,
                    x.Name,
                    x.Phone,
                    x.Email,
                    x.LoyaltyPoints,
                    x.CumulativeSpend,
                    x.IsActive,
                    x.SalesOrders.Count,
                    x.SalesOrders.OrderByDescending(o => o.CreatedUtc).Select(o => (DateTime?)o.CreatedUtc).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerItem> SaveCustomerAsync(CustomerUpsertRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AppValidationException("Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            throw new AppValidationException("Customer phone is required.");
        }

        if (request.LoyaltyPoints < 0)
        {
            throw new AppValidationException("Loyalty points cannot be negative.");
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        Customer customer;

        if (!request.Id.HasValue)
        {
            customer = new Customer();
            db.Customers.Add(customer);
            customer.CumulativeSpend = 0m;
        }
        else
        {
            customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken)
                ?? throw new AppValidationException("Customer not found.");
        }

        customer.Name = request.Name.Trim();
        customer.Phone = request.Phone.Trim();
        customer.Email = request.Email?.Trim();
        customer.LoyaltyPoints = request.LoyaltyPoints;
        customer.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        var purchaseCount = await db.SalesOrders.CountAsync(x => x.CustomerId == customer.Id, cancellationToken);
        var lastPurchase = await db.SalesOrders
            .Where(x => x.CustomerId == customer.Id)
            .OrderByDescending(x => x.CreatedUtc)
            .Select(x => (DateTime?)x.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return new CustomerItem(
            customer.Id,
            customer.Name,
            customer.Phone,
            customer.Email,
            customer.LoyaltyPoints,
            customer.CumulativeSpend,
            customer.IsActive,
            purchaseCount,
            lastPurchase);
    }

    public async Task SetCustomerActiveAsync(long customerId, bool isActive, CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == customerId, cancellationToken)
            ?? throw new AppValidationException("Customer not found.");

        customer.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CustomerPurchaseItem>> GetCustomerPurchaseHistoryAsync(long customerId, CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        return await db.SalesOrders
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedUtc)
            .Take(200)
            .Select(
                x => new CustomerPurchaseItem(
                    x.Id,
                    x.ReceiptNumber,
                    x.CreatedUtc,
                    x.TotalAfterTax,
                    x.PaymentMethod.ToString(),
                    x.OrderType.ToString()))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReportSummary> GetReportSummaryAsync(ReportPeriod period, CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        var (fromUtc, toUtc) = ResolveRange(period);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        var salesOrders = await db.SalesOrders
            .AsNoTracking()
            .Where(x => x.OrderType == SalesOrderType.Sale && x.CreatedUtc >= fromUtc && x.CreatedUtc <= toUtc)
            .Select(
                x => new
                {
                    x.Id,
                    x.TotalAfterTax,
                    x.CreatedUtc,
                })
            .ToListAsync(cancellationToken);

        var lineData = await db.SalesOrderLines
            .AsNoTracking()
            .Where(x => x.SalesOrder != null && x.SalesOrder.OrderType == SalesOrderType.Sale && x.SalesOrder.CreatedUtc >= fromUtc && x.SalesOrder.CreatedUtc <= toUtc)
            .Select(
                x => new
                {
                    x.ProductId,
                    ProductName = x.Product != null ? x.Product.Name : $"Product-{x.ProductId}",
                    x.Quantity,
                    Revenue = x.LineTotal,
                    Cost = x.Product != null ? x.Product.Cost : 0m,
                })
            .ToListAsync(cancellationToken);

        var totalSales = Money.Round(salesOrders.Sum(x => x.TotalAfterTax));
        var transactionCount = salesOrders.Count;
        var averageSale = transactionCount == 0 ? 0m : Money.Round(totalSales / transactionCount);

        var totalProfit = Money.Round(lineData.Sum(x => x.Revenue - (x.Cost * x.Quantity)));

        var topProducts = lineData
            .GroupBy(x => new { x.ProductId, x.ProductName })
            .Select(
                group => new TopProductItem(
                    group.Key.ProductId,
                    group.Key.ProductName,
                    group.Sum(x => x.Quantity),
                    Money.Round(group.Sum(x => x.Revenue)),
                    Money.Round(group.Sum(x => x.Revenue - (x.Cost * x.Quantity)))))
            .OrderByDescending(x => x.Revenue)
            .Take(10)
            .ToArray();

        return new ReportSummary(fromUtc, toUtc, totalSales, totalProfit, transactionCount, averageSale, topProducts);
    }

    public async Task<IReadOnlyCollection<UserAdminItem>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        return await db.Users
            .AsNoTracking()
            .OrderBy(x => x.Role)
            .ThenBy(x => x.FullName)
            .Select(
                x => new UserAdminItem(
                    x.Id,
                    x.Username,
                    x.FullName,
                    x.Email,
                    x.Role.ToString(),
                    x.IsActive,
                    x.LastLoginUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserAdminItem> CreateCashierAsync(CreateCashierRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new AppValidationException("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new AppValidationException("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new AppValidationException("Password must be at least 8 characters.");
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        var normalizedUsername = request.Username.Trim();
        var exists = await db.Users.AnyAsync(x => x.Username == normalizedUsername, cancellationToken);
        if (exists)
        {
            throw new AppValidationException("Username already exists.");
        }

        var user = new User
        {
            Username = normalizedUsername,
            FullName = request.FullName.Trim(),
            Email = request.Email?.Trim(),
            Role = UserRole.Cashier,
            IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12),
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return new UserAdminItem(user.Id, user.Username, user.FullName, user.Email, user.Role.ToString(), user.IsActive, user.LastLoginUtc);
    }

    public async Task ResetUserPasswordAsync(long userId, string newPassword, CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            throw new AppValidationException("New password must be at least 8 characters.");
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new AppValidationException("User not found.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, 12);
        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetUserActiveAsync(long userId, bool isActive, CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new AppValidationException("User not found.");

        user.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<BackupItem>> GetBackupsAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        using var scope = _scopeFactory.CreateScope();
        var backupService = scope.ServiceProvider.GetRequiredService<IBackupRestoreService>();

        var allBackups = await backupService.ListAllBackupsAsync(cancellationToken);

        return allBackups
            .OrderByDescending(x => x.CreatedUtc)
            .Select(x => new BackupItem(x.FilePath, x.CreatedUtc, x.IsAutomatic, "Available"))
            .ToArray();
    }

    public async Task<BackupItem> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        using var scope = _scopeFactory.CreateScope();
        var backupService = scope.ServiceProvider.GetRequiredService<IBackupRestoreService>();

        var result = await backupService.CreateBackupAsync(isAutomatic: false, cancellationToken);
        return new BackupItem(result.FilePath, result.CreatedUtc, result.IsAutomatic, "Created");
    }

    public async Task RestoreBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();

        if (string.IsNullOrWhiteSpace(backupFilePath))
        {
            throw new AppValidationException("Select a backup to restore.");
        }

        using var scope = _scopeFactory.CreateScope();
        var backupService = scope.ServiceProvider.GetRequiredService<IBackupRestoreService>();

        await backupService.RestoreBackupAsync(
            new RestoreRequest
            {
                BackupFilePath = backupFilePath,
                CreateSafetyBackupBeforeRestore = true,
            },
            cancellationToken);
    }

    private long EnsureAdminAccess()
    {
        if (!_operatorSessionContext.IsInitialized)
        {
            throw new UnauthorizedAccessException("Operator session is not initialized.");
        }

        if (_operatorSessionContext.OperatorRole != UserRole.Admin)
        {
            throw new UnauthorizedAccessException("Only admins can access management features.");
        }

        return _operatorSessionContext.OperatorUserId;
    }

    private static (DateTime fromUtc, DateTime toUtc) ResolveRange(ReportPeriod period)
    {
        var now = DateTime.UtcNow;
        return period switch
        {
            ReportPeriod.Daily => (now.Date, now),
            ReportPeriod.Weekly => (now.Date.AddDays(-6), now),
            ReportPeriod.Monthly => (new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), now),
            _ => (now.Date, now),
        };
    }
}
