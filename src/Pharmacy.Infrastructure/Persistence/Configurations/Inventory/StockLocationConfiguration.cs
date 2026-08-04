using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Inventory;

internal sealed class StockLocationConfiguration : IEntityTypeConfiguration<StockLocationEntity>
{
    public void Configure(EntityTypeBuilder<StockLocationEntity> builder)
    {
        builder.ToTable("StockLocations", InventorySchema.Name, table =>
        {
            table.HasCheckConstraint("CK_inventory_StockLocations_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
            table.HasCheckConstraint("CK_inventory_StockLocations_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
            table.HasCheckConstraint("CK_inventory_StockLocations_CreationFingerprint_NotBlank", "LEN(LTRIM(RTRIM([CreationFingerprint]))) > 0");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_inventory_StockLocations");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.CreationOperationId).IsRequired();
        builder.Property(entity => entity.CreationFingerprint).HasColumnType("nvarchar(512)").HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.LastOperationId);
        builder.Property(entity => entity.LastOperationFingerprint).HasColumnType("nvarchar(512)").HasMaxLength(512);
        builder.Property(entity => entity.Code).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Name).HasColumnType("nvarchar(160)").HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.IsActive).IsRequired();
        builder.Property(entity => entity.IsPrimary).IsRequired();
        builder.Property(entity => entity.CreatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(entity => entity.Code).IsUnique().HasDatabaseName("UX_inventory_StockLocations_Code");
        builder.HasIndex(entity => entity.CreationOperationId).IsUnique().HasDatabaseName("UX_inventory_StockLocations_CreationOperationId");
        builder.HasIndex(entity => entity.LastOperationId).IsUnique().HasFilter("[LastOperationId] IS NOT NULL").HasDatabaseName("UX_inventory_StockLocations_LastOperationId");
        builder.HasIndex(entity => entity.IsPrimary).IsUnique().HasFilter("[IsPrimary] = 1").HasDatabaseName("UX_inventory_StockLocations_OnePrimary");
    }
}
