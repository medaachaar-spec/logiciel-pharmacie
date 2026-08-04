using Microsoft.EntityFrameworkCore;
using Pharmacy.Infrastructure.Persistence.Entities.Catalog;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PharmacyDbContext).Assembly);
    }
}
