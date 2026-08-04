using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Catalog;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<ProductEntity>
{
    public void Configure(EntityTypeBuilder<ProductEntity> builder)
    {
        builder.ToTable("Products", CatalogSchema.Name, table =>
        {
            table.HasCheckConstraint(
                "CK_catalog_Products_Name_NotBlank",
                "LEN(LTRIM(RTRIM([Name]))) > 0");
            table.HasCheckConstraint(
                "CK_catalog_Products_UpdatedAfterCreated",
                "[UpdatedAtUtc] >= [CreatedAtUtc]");
        });

        builder.HasKey(product => product.Id)
            .HasName("PK_catalog_Products");
        builder.Property(product => product.Id).UseIdentityColumn();
        builder.Property(product => product.Name)
            .HasColumnType("nvarchar(256)")
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(product => product.GenericName)
            .HasColumnType("nvarchar(256)")
            .HasMaxLength(256);
        builder.Property(product => product.Dosage)
            .HasColumnType("nvarchar(128)")
            .HasMaxLength(128);
        builder.Property(product => product.Form)
            .HasColumnType("nvarchar(128)")
            .HasMaxLength(128);
        builder.Property(product => product.PackageDescription)
            .HasColumnType("nvarchar(256)")
            .HasMaxLength(256);
        builder.Property(product => product.BrandName)
            .HasColumnType("nvarchar(160)")
            .HasMaxLength(160);
        builder.Property(product => product.IsActive)
            .HasDefaultValue(true)
            .IsRequired();
        builder.Property(product => product.CreatedAtUtc)
            .HasColumnType("datetime2(7)")
            .IsRequired();
        builder.Property(product => product.UpdatedAtUtc)
            .HasColumnType("datetime2(7)")
            .IsRequired();
        builder.Property(product => product.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(product => product.Name)
            .HasDatabaseName("IX_catalog_Products_Name");
        builder.HasIndex(product => product.IsActive)
            .HasDatabaseName("IX_catalog_Products_IsActive");
    }
}
