using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Catalog;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class ProductCategoryConfiguration
    : IEntityTypeConfiguration<ProductCategoryEntity>
{
    public void Configure(EntityTypeBuilder<ProductCategoryEntity> builder)
    {
        builder.ToTable("ProductCategories", CatalogSchema.Name, table =>
        {
            table.HasCheckConstraint(
                "CK_catalog_ProductCategories_Code_NotBlank",
                "LEN(LTRIM(RTRIM([Code]))) > 0");
            table.HasCheckConstraint(
                "CK_catalog_ProductCategories_Name_NotBlank",
                "LEN(LTRIM(RTRIM([Name]))) > 0");
            table.HasCheckConstraint(
                "CK_catalog_ProductCategories_SortOrder_NonNegative",
                "[SortOrder] >= 0");
        });

        builder.HasKey(category => category.Id)
            .HasName("PK_catalog_ProductCategories");
        builder.Property(category => category.Id).UseIdentityColumn();
        builder.Property(category => category.Code)
            .HasColumnType("nvarchar(64)")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(category => category.Name)
            .HasColumnType("nvarchar(160)")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(category => category.IsActive).IsRequired();
        builder.Property(category => category.DefaultTracksLots).IsRequired();
        builder.Property(category => category.DefaultTracksExpiry).IsRequired();
        builder.Property(category => category.DefaultDiscountAllowed).IsRequired();
        builder.Property(category => category.SortOrder).IsRequired();
        builder.Property(category => category.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(category => category.Code)
            .IsUnique()
            .HasDatabaseName("UX_catalog_ProductCategories_Code");
        builder.HasIndex(category => category.Name)
            .HasDatabaseName("IX_catalog_ProductCategories_Name");
    }
}
