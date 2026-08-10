using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Inventory;

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovementEntity>
{
    public void Configure(EntityTypeBuilder<StockMovementEntity> builder)
    {
        builder.ToTable("StockMovements", InventorySchema.Name, table =>
        {
            table.HasCheckConstraint("CK_inventory_StockMovements_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_inventory_StockMovements_Balances_NonNegative", "[QuantityBefore] >= 0 AND [QuantityAfter] >= 0");
            table.HasCheckConstraint("CK_inventory_StockMovements_Reason_NotBlank", "LEN(LTRIM(RTRIM([Reason]))) > 0");
            table.HasCheckConstraint("CK_inventory_StockMovements_SourceType_NotBlank", "LEN(LTRIM(RTRIM([SourceType]))) > 0");
            table.HasCheckConstraint("CK_inventory_StockMovements_Fingerprint_NotBlank", "LEN(LTRIM(RTRIM([OperationFingerprint]))) > 0");
            table.HasCheckConstraint(
                "CK_inventory_StockMovements_Direction",
                "(([MovementType] IN (N'Opening', N'ManualIncrease', N'InventoryIncrease', N'PurchaseReceipt') AND [QuantityAfter] = [QuantityBefore] + [Quantity]) OR " +
                "([MovementType] IN (N'ManualDecrease', N'InventoryDecrease') AND [QuantityAfter] = [QuantityBefore] - [Quantity]))");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_inventory_StockMovements");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.OperationId).IsRequired();
        builder.Property(entity => entity.OperationFingerprint).HasColumnType("nvarchar(512)").HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.ProductId).IsRequired();
        builder.Property(entity => entity.StockLocationId).IsRequired();
        builder.Property(entity => entity.StockBatchId);
        builder.Property(entity => entity.MovementType).HasConversion<string>().HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Quantity).HasColumnType("int").IsRequired();
        builder.Property(entity => entity.QuantityBefore).HasColumnType("int").IsRequired();
        builder.Property(entity => entity.QuantityAfter).HasColumnType("int").IsRequired();
        builder.Property(entity => entity.Reason).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.SourceType).HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.SourceReference).HasColumnType("nvarchar(128)").HasMaxLength(128);
        builder.Property(entity => entity.OccurredAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.CreatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.HasOne(entity => entity.Product).WithMany(product => product.StockMovements)
            .HasForeignKey(entity => entity.ProductId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_inventory_StockMovements_Products_ProductId");
        builder.HasOne(entity => entity.StockLocation).WithMany(location => location.Movements)
            .HasForeignKey(entity => entity.StockLocationId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_inventory_StockMovements_StockLocations_StockLocationId");
        builder.HasOne(entity => entity.StockBatch).WithMany(batch => batch.Movements)
            .HasForeignKey(entity => entity.StockBatchId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_inventory_StockMovements_StockBatches_StockBatchId");
        builder.HasIndex(entity => entity.OperationId).IsUnique().HasDatabaseName("UX_inventory_StockMovements_OperationId");
        builder.HasIndex(entity => new { entity.ProductId, entity.OccurredAtUtc }).HasDatabaseName("IX_inventory_StockMovements_ProductId_OccurredAtUtc");
        builder.HasIndex(entity => new { entity.StockLocationId, entity.OccurredAtUtc }).HasDatabaseName("IX_inventory_StockMovements_LocationId_OccurredAtUtc");
        builder.HasIndex(entity => entity.StockBatchId).HasDatabaseName("IX_inventory_StockMovements_StockBatchId");
    }
}
