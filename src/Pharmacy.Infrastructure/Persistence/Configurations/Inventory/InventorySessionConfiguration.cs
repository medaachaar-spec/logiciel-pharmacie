using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Inventory;

internal sealed class InventorySessionConfiguration : IEntityTypeConfiguration<InventorySessionEntity>
{
    public void Configure(EntityTypeBuilder<InventorySessionEntity> builder)
    {
        builder.ToTable("InventorySessions", InventorySchema.Name, table =>
        {
            table.HasCheckConstraint("CK_inventory_InventorySessions_Reference_NotBlank", "LEN(LTRIM(RTRIM([Reference]))) > 0");
            table.HasCheckConstraint("CK_inventory_InventorySessions_CreationFingerprint_NotBlank", "LEN(LTRIM(RTRIM([CreationFingerprint]))) > 0");
            table.HasCheckConstraint("CK_inventory_InventorySessions_CompletedAtUtc", "[CompletedAtUtc] IS NULL OR [CompletedAtUtc] >= [StartedAtUtc]");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_inventory_InventorySessions");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.CreationOperationId).IsRequired();
        builder.Property(entity => entity.CreationFingerprint).HasColumnType("nvarchar(512)").HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.StartOperationId);
        builder.Property(entity => entity.StartOperationFingerprint).HasColumnType("nvarchar(512)").HasMaxLength(512);
        builder.Property(entity => entity.FinalizationOperationId);
        builder.Property(entity => entity.FinalizationFingerprint).HasColumnType("nvarchar(512)").HasMaxLength(512);
        builder.Property(entity => entity.CancellationOperationId);
        builder.Property(entity => entity.CancellationFingerprint).HasColumnType("nvarchar(512)").HasMaxLength(512);
        builder.Property(entity => entity.Reference).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.StockLocationId).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.StartedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.CompletedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(entity => entity.Notes).HasColumnType("nvarchar(512)").HasMaxLength(512);
        builder.Property(entity => entity.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(entity => entity.StockLocation).WithMany(location => location.InventorySessions)
            .HasForeignKey(entity => entity.StockLocationId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_inventory_InventorySessions_StockLocations_StockLocationId");
        builder.HasIndex(entity => entity.Reference).IsUnique().HasDatabaseName("UX_inventory_InventorySessions_Reference");
        builder.HasIndex(entity => entity.CreationOperationId).IsUnique().HasDatabaseName("UX_inventory_InventorySessions_CreationOperationId");
        builder.HasIndex(entity => entity.StartOperationId).IsUnique().HasFilter("[StartOperationId] IS NOT NULL").HasDatabaseName("UX_inventory_InventorySessions_StartOperationId");
        builder.HasIndex(entity => entity.FinalizationOperationId).IsUnique().HasFilter("[FinalizationOperationId] IS NOT NULL").HasDatabaseName("UX_inventory_InventorySessions_FinalizationOperationId");
        builder.HasIndex(entity => entity.CancellationOperationId).IsUnique().HasFilter("[CancellationOperationId] IS NOT NULL").HasDatabaseName("UX_inventory_InventorySessions_CancellationOperationId");
        builder.HasIndex(entity => new { entity.StockLocationId, entity.Status }).HasDatabaseName("IX_inventory_InventorySessions_LocationId_Status");
    }
}
