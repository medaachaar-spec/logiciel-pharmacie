using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Inventory;

internal sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalanceEntity>
{
    public void Configure(EntityTypeBuilder<StockBalanceEntity> builder)
    {
        builder.ToTable("StockBalances", InventorySchema.Name, table =>
            table.HasCheckConstraint("CK_inventory_StockBalances_AvailableQuantity_NonNegative", "[AvailableQuantity] >= 0"));
        builder.HasKey(entity => entity.Id).HasName("PK_inventory_StockBalances");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.StockLocationId).IsRequired();
        builder.Property(entity => entity.ProductId).IsRequired();
        builder.Property(entity => entity.StockBatchId);
        builder.Property(entity => entity.AvailableQuantity).HasColumnType("int").IsRequired();
        builder.Property(entity => entity.UpdatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(entity => entity.StockLocation).WithMany(location => location.Balances)
            .HasForeignKey(entity => entity.StockLocationId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_inventory_StockBalances_StockLocations_StockLocationId");
        builder.HasOne(entity => entity.Product).WithMany(product => product.StockBalances)
            .HasForeignKey(entity => entity.ProductId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_inventory_StockBalances_Products_ProductId");
        builder.HasOne(entity => entity.StockBatch).WithMany(batch => batch.Balances)
            .HasForeignKey(entity => entity.StockBatchId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_inventory_StockBalances_StockBatches_StockBatchId");
        builder.HasIndex(entity => new { entity.StockLocationId, entity.ProductId, entity.StockBatchId })
            .IsUnique().HasFilter("[StockBatchId] IS NOT NULL")
            .HasDatabaseName("UX_inventory_StockBalances_Location_Product_Batch");
        builder.HasIndex(entity => new { entity.StockLocationId, entity.ProductId })
            .IsUnique().HasFilter("[StockBatchId] IS NULL")
            .HasDatabaseName("UX_inventory_StockBalances_Location_Product_NoBatch");
        builder.HasIndex(entity => new { entity.ProductId, entity.StockLocationId })
            .HasDatabaseName("IX_inventory_StockBalances_ProductId_StockLocationId");
        builder.HasIndex(entity => entity.StockBatchId).HasDatabaseName("IX_inventory_StockBalances_StockBatchId");
    }
}
