using Microsoft.EntityFrameworkCore;
using Pharmacy.Infrastructure.Persistence.Entities.Catalog;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PharmacyDbContext).Assembly);
    }
}
