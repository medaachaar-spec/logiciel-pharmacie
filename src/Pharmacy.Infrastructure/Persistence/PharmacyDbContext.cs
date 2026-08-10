using Microsoft.EntityFrameworkCore;
using Pharmacy.Infrastructure.Persistence.Entities.Audit;
using Pharmacy.Infrastructure.Persistence.Entities.Cash;
using Pharmacy.Infrastructure.Persistence.Entities.Catalog;
using Pharmacy.Infrastructure.Persistence.Entities.Customers;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;
using Pharmacy.Infrastructure.Persistence.Entities.Purchasing;
using Pharmacy.Infrastructure.Persistence.Entities.Reporting;
using Pharmacy.Infrastructure.Persistence.Entities.Sales;
using Pharmacy.Infrastructure.Persistence.Entities.Security;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class PharmacyDbContext : DbContext
{
    public PharmacyDbContext(DbContextOptions<PharmacyDbContext> options)
        : base(options)
    {
    }

    internal DbSet<ProductCategoryEntity> ProductCategories => Set<ProductCategoryEntity>();

    internal DbSet<ProductEntity> Products => Set<ProductEntity>();

    internal DbSet<ProductLocalSettingsEntity> ProductLocalSettings => Set<ProductLocalSettingsEntity>();

    internal DbSet<ProductBarcodeEntity> ProductBarcodes => Set<ProductBarcodeEntity>();

    internal DbSet<ProductPriceHistoryEntity> ProductPriceHistories => Set<ProductPriceHistoryEntity>();

    internal DbSet<StockLocationEntity> StockLocations => Set<StockLocationEntity>();

    internal DbSet<StockBatchEntity> StockBatches => Set<StockBatchEntity>();

    internal DbSet<StockBalanceEntity> StockBalances => Set<StockBalanceEntity>();

    internal DbSet<StockMovementEntity> StockMovements => Set<StockMovementEntity>();

    internal DbSet<InventorySessionEntity> InventorySessions => Set<InventorySessionEntity>();

    internal DbSet<InventoryCountLineEntity> InventoryCountLines => Set<InventoryCountLineEntity>();

    internal DbSet<SupplierEntity> Suppliers => Set<SupplierEntity>();
    internal DbSet<PurchaseOrderEntity> PurchaseOrders => Set<PurchaseOrderEntity>();
    internal DbSet<PurchaseOrderLineEntity> PurchaseOrderLines => Set<PurchaseOrderLineEntity>();
    internal DbSet<GoodsReceiptEntity> GoodsReceipts => Set<GoodsReceiptEntity>();
    internal DbSet<GoodsReceiptLineEntity> GoodsReceiptLines => Set<GoodsReceiptLineEntity>();

    internal DbSet<SaleEntity> Sales => Set<SaleEntity>();
    internal DbSet<SaleLineEntity> SaleLines => Set<SaleLineEntity>();
    internal DbSet<SaleLineAllocationEntity> SaleLineAllocations => Set<SaleLineAllocationEntity>();
    internal DbSet<PaymentEntity> Payments => Set<PaymentEntity>();
    internal DbSet<SaleOperationEntity> SaleOperations => Set<SaleOperationEntity>();
    internal DbSet<CustomerEntity> Customers => Set<CustomerEntity>();
    internal DbSet<CustomerLedgerEntryEntity> CustomerLedgerEntries => Set<CustomerLedgerEntryEntity>();
    internal DbSet<CashLedgerEntryEntity> CashLedgerEntries => Set<CashLedgerEntryEntity>();
    internal DbSet<SuspendedSaleEntity> SuspendedSales => Set<SuspendedSaleEntity>();
    internal DbSet<SuspendedSaleLineEntity> SuspendedSaleLines => Set<SuspendedSaleLineEntity>();
    internal DbSet<SaleReturnEntity> SaleReturns => Set<SaleReturnEntity>();
    internal DbSet<SaleReturnLineEntity> SaleReturnLines => Set<SaleReturnLineEntity>();
    internal DbSet<UserAccountEntity> UserAccounts => Set<UserAccountEntity>();
    internal DbSet<RoleEntity> Roles => Set<RoleEntity>();
    internal DbSet<PermissionEntity> Permissions => Set<PermissionEntity>();
    internal DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    internal DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();
    internal DbSet<RecoveryCodeEntity> RecoveryCodes => Set<RecoveryCodeEntity>();
    internal DbSet<AuthenticationEventEntity> AuthenticationEvents => Set<AuthenticationEventEntity>();
    internal DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();
    internal DbSet<AuditEventDetailEntity> AuditEventDetails => Set<AuditEventDetailEntity>();
    internal DbSet<ReportSettingsEntity> ReportSettings => Set<ReportSettingsEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PharmacyDbContext).Assembly);
    }
}
