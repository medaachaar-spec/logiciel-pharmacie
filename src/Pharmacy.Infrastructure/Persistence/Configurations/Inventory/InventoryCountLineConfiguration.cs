using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Inventory;

internal sealed class InventoryCountLineConfiguration : IEntityTypeConfiguration<InventoryCountLineEntity>
{
    public void Configure(EntityTypeBuilder<InventoryCountLineEntity> builder)
    {
        builder.ToTable("InventoryCountLines", InventorySchema.Name, table =>
        {
            table.HasCheckConstraint("CK_inventory_InventoryCountLines_ExpectedQuantity_NonNegative", "[ExpectedQuantity] >= 0");
            table.HasCheckConstraint("CK_inventory_InventoryCountLines_CountedQuantity_NonNegative", "[CountedQuantity] IS NULL OR [CountedQuantity] >= 0");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_inventory_InventoryCountLines");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.InventorySessionId).IsRequired();
        builder.Property(entity => entity.ProductId).IsRequired();
        builder.Property(entity => entity.StockBatchId);
        builder.Property(entity => entity.ExpectedQuantity).HasColumnType("int").IsRequired();
        builder.Property(entity => entity.CountedQuantity).HasColumnType("int");
        builder.Property(entity => entity.LastOperationId);
        builder.Property(entity => entity.LastOperationFingerprint).HasColumnType("nvarchar(512)").HasMaxLength(512);
        builder.Property(entity => entity.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(entity => entity.InventorySession).WithMany(session => session.Lines)
            .HasForeignKey(entity => entity.InventorySessionId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_inventory_InventoryCountLines_InventorySessions_InventorySessionId");
        builder.HasOne(entity => entity.Product).WithMany(product => product.InventoryCountLines)
            .HasForeignKey(entity => entity.ProductId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_inventory_InventoryCountLines_Products_ProductId");
        builder.HasOne(entity => entity.StockBatch).WithMany(batch => batch.InventoryCountLines)
            .HasForeignKey(entity => entity.StockBatchId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_inventory_InventoryCountLines_StockBatches_StockBatchId");
        builder.HasIndex(entity => new { entity.InventorySessionId, entity.ProductId, entity.StockBatchId })
            .IsUnique().HasFilter("[StockBatchId] IS NOT NULL")
            .HasDatabaseName("UX_inventory_InventoryCountLines_Session_Product_Batch");
        builder.HasIndex(entity => new { entity.InventorySessionId, entity.ProductId })
            .IsUnique().HasFilter("[StockBatchId] IS NULL")
            .HasDatabaseName("UX_inventory_InventoryCountLines_Session_Product_NoBatch");
        builder.HasIndex(entity => entity.LastOperationId).IsUnique().HasFilter("[LastOperationId] IS NOT NULL")
            .HasDatabaseName("UX_inventory_InventoryCountLines_LastOperationId");
    }
}
