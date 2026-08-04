using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Inventory;

internal sealed class StockBatchConfiguration : IEntityTypeConfiguration<StockBatchEntity>
{
    public void Configure(EntityTypeBuilder<StockBatchEntity> builder)
    {
        builder.ToTable("StockBatches", InventorySchema.Name, table =>
        {
            table.HasCheckConstraint("CK_inventory_StockBatches_BatchNumber_NotBlank", "[BatchNumber] IS NULL OR LEN(LTRIM(RTRIM([BatchNumber]))) > 0");
            table.HasCheckConstraint("CK_inventory_StockBatches_CreationFingerprint_NotBlank", "LEN(LTRIM(RTRIM([CreationFingerprint]))) > 0");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_inventory_StockBatches");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.CreationOperationId).IsRequired();
        builder.Property(entity => entity.CreationFingerprint).HasColumnType("nvarchar(512)").HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.ProductId).IsRequired();
        builder.Property(entity => entity.BatchNumber).HasColumnType("nvarchar(128)").HasMaxLength(128);
        builder.Property(entity => entity.ExpiryDate).HasColumnType("date");
        builder.Property(entity => entity.IsActive).IsRequired();
        builder.Property(entity => entity.CreatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(entity => entity.Product).WithMany(product => product.StockBatches)
            .HasForeignKey(entity => entity.ProductId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_inventory_StockBatches_Products_ProductId");
        builder.HasIndex(entity => entity.CreationOperationId).IsUnique().HasDatabaseName("UX_inventory_StockBatches_CreationOperationId");
        builder.HasIndex(entity => new { entity.ProductId, entity.BatchNumber }).IsUnique()
            .HasFilter("[BatchNumber] IS NOT NULL").HasDatabaseName("UX_inventory_StockBatches_ProductId_BatchNumber");
        builder.HasIndex(entity => entity.ExpiryDate).HasDatabaseName("IX_inventory_StockBatches_ExpiryDate");
    }
}
