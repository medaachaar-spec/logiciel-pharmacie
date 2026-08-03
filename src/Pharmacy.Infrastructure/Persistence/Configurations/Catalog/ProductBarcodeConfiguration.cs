using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Catalog;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class ProductBarcodeConfiguration
    : IEntityTypeConfiguration<ProductBarcodeEntity>
{
    public void Configure(EntityTypeBuilder<ProductBarcodeEntity> builder)
    {
        builder.ToTable("ProductBarcodes", CatalogSchema.Name, table =>
        {
            table.HasCheckConstraint(
                "CK_catalog_ProductBarcodes_Value_NotBlank",
                "LEN(LTRIM(RTRIM([Value]))) > 0");
            table.HasCheckConstraint(
                "CK_catalog_ProductBarcodes_NormalizedValue_NotBlank",
                "LEN(LTRIM(RTRIM([NormalizedValue]))) > 0");
        });

        builder.HasKey(barcode => barcode.Id)
            .HasName("PK_catalog_ProductBarcodes");
        builder.Property(barcode => barcode.Id).UseIdentityColumn();
        builder.Property(barcode => barcode.ProductId).IsRequired();
        builder.Property(barcode => barcode.Value)
            .HasColumnType("nvarchar(128)")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(barcode => barcode.NormalizedValue)
            .HasColumnType("nvarchar(128)")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(barcode => barcode.IsPrimary).IsRequired();
        builder.Property(barcode => barcode.BarcodeType)
            .HasColumnType("nvarchar(32)")
            .HasMaxLength(32);
        builder.Property(barcode => barcode.IsActive).IsRequired();
        builder.Property(barcode => barcode.CreatedAtUtc)
            .HasColumnType("datetime2(7)")
            .IsRequired();
        builder.Property(barcode => barcode.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne(barcode => barcode.Product)
            .WithMany(product => product.Barcodes)
            .HasForeignKey(barcode => barcode.ProductId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_catalog_ProductBarcodes_Products_ProductId");

        builder.HasIndex(barcode => barcode.ProductId)
            .HasDatabaseName("IX_catalog_ProductBarcodes_ProductId");
        builder.HasIndex(barcode => barcode.NormalizedValue)
            .IsUnique()
            .HasDatabaseName("UX_catalog_ProductBarcodes_NormalizedValue");
        builder.HasIndex(barcode => new
            {
                barcode.ProductId,
                barcode.IsPrimary,
                barcode.IsActive
            })
            .IsUnique()
            .HasFilter("[IsPrimary] = 1 AND [IsActive] = 1")
            .HasDatabaseName("UX_catalog_ProductBarcodes_OneActivePrimaryPerProduct");
    }
}
