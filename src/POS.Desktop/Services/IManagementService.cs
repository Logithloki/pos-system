using POS.Desktop.Models;

namespace POS.Desktop.Services;

public interface IManagementService
{
    Task<IReadOnlyCollection<InventoryProductItem>> GetInventoryAsync(CancellationToken cancellationToken = default);

    Task<InventoryProductItem> SaveProductAsync(ProductUpsertRequest request, long operatorUserId, CancellationToken cancellationToken = default);

    Task SetProductActiveAsync(long productId, bool isActive, CancellationToken cancellationToken = default);

    Task AdjustStockAsync(long productId, int quantityDelta, string note, long operatorUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SupplierItem>> GetSuppliersAsync(CancellationToken cancellationToken = default);

    Task<SupplierItem> SaveSupplierAsync(SupplierUpsertRequest request, CancellationToken cancellationToken = default);

    Task SetSupplierActiveAsync(long supplierId, bool isActive, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CustomerItem>> GetCustomersAsync(CancellationToken cancellationToken = default);

    Task<CustomerItem> SaveCustomerAsync(CustomerUpsertRequest request, CancellationToken cancellationToken = default);

    Task SetCustomerActiveAsync(long customerId, bool isActive, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CustomerPurchaseItem>> GetCustomerPurchaseHistoryAsync(long customerId, CancellationToken cancellationToken = default);

    Task<ReportSummary> GetReportSummaryAsync(ReportPeriod period, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserAdminItem>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<UserAdminItem> CreateCashierAsync(CreateCashierRequest request, CancellationToken cancellationToken = default);

    Task ResetUserPasswordAsync(long userId, string newPassword, CancellationToken cancellationToken = default);

    Task SetUserActiveAsync(long userId, bool isActive, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<BackupItem>> GetBackupsAsync(CancellationToken cancellationToken = default);

    Task<BackupItem> CreateBackupAsync(CancellationToken cancellationToken = default);

    Task RestoreBackupAsync(string backupFilePath, CancellationToken cancellationToken = default);
}
