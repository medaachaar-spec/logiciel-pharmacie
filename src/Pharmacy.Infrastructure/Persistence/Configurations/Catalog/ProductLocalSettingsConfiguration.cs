using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Catalog;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class ProductLocalSettingsConfiguration
    : IEntityTypeConfiguration<ProductLocalSettingsEntity>
{
    public void Configure(EntityTypeBuilder<ProductLocalSettingsEntity> builder)
    {
        builder.ToTable("ProductLocalSettings", CatalogSchema.Name, table =>
        {
            table.HasCheckConstraint(
                "CK_catalog_ProductLocalSettings_InternalCode_NotBlank",
                "[InternalCode] IS NULL OR LEN(LTRIM(RTRIM([InternalCode]))) > 0");
            table.HasCheckConstraint(
                "CK_catalog_ProductLocalSettings_QuantityScale_Range",
                "[QuantityScale] BETWEEN 0 AND 3");
            table.HasCheckConstraint(
                "CK_catalog_ProductLocalSettings_StockThresholds_NonNegative",
                "([MinimumStock] IS NULL OR [MinimumStock] >= 0) AND " +
                "([MaximumStock] IS NULL OR [MaximumStock] >= 0)");
            table.HasCheckConstraint(
                "CK_catalog_ProductLocalSettings_StockThresholds_Order",
                "[MinimumStock] IS NULL OR [MaximumStock] IS NULL OR [MinimumStock] <= [MaximumStock]");
            table.HasCheckConstraint(
                "CK_catalog_ProductLocalSettings_CurrentSalePrice_NonNegative",
                "[CurrentSalePrice] >= 0");
            table.HasCheckConstraint(
                "CK_catalog_ProductLocalSettings_CurrencyCode_Length",
                "LEN([CurrencyCode]) = 3");
        });

        builder.HasKey(settings => settings.ProductId)
            .HasName("PK_catalog_ProductLocalSettings");
        builder.Property(settings => settings.ProductId).ValueGeneratedNever();
        builder.Property(settings => settings.InternalCode)
            .HasColumnType("nvarchar(128)")
            .HasMaxLength(128);
        builder.Property(settings => settings.CategoryId).IsRequired();
        builder.Property(settings => settings.IsActive).IsRequired();
        builder.Property(settings => settings.IsSellable).IsRequired();
        builder.Property(settings => settings.IsPurchasable).IsRequired();
        builder.Property(settings => settings.TracksLots).IsRequired();
        builder.Property(settings => settings.TracksExpiry).IsRequired();
        builder.Property(settings => settings.DiscountAllowed).IsRequired();
        builder.Property(settings => settings.QuantityScale)
            .HasColumnType("tinyint")
            .IsRequired();
        builder.Property(settings => settings.StorageLocation)
            .HasColumnType("nvarchar(128)")
            .HasMaxLength(128);
        builder.Property(settings => settings.MinimumStock)
            .HasColumnType("decimal(18,3)");
        builder.Property(settings => settings.MaximumStock)
            .HasColumnType("decimal(18,3)");
        builder.Property(settings => settings.CurrentSalePrice)
            .HasColumnType("decimal(19,4)")
            .IsRequired();
        builder.Property(settings => settings.CurrencyCode)
            .HasColumnType("nvarchar(3)")
            .HasMaxLength(3)
            .IsRequired();
        builder.Property(settings => settings.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne(settings => settings.Product)
            .WithOne(product => product.LocalSettings)
            .HasForeignKey<ProductLocalSettingsEntity>(settings => settings.ProductId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_catalog_ProductLocalSettings_Products_ProductId");
        builder.HasOne(settings => settings.Category)
            .WithMany(category => category.ProductSettings)
            .HasForeignKey(settings => settings.CategoryId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_catalog_ProductLocalSettings_ProductCategories_CategoryId");

        builder.HasIndex(settings => settings.CategoryId)
            .HasDatabaseName("IX_catalog_ProductLocalSettings_CategoryId");
        builder.HasIndex(settings => settings.InternalCode)
            .IsUnique()
            .HasFilter("[InternalCode] IS NOT NULL")
            .HasDatabaseName("UX_catalog_ProductLocalSettings_InternalCode");
    }
}
