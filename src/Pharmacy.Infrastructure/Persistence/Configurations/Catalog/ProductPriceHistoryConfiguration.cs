using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Catalog;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class ProductPriceHistoryConfiguration
    : IEntityTypeConfiguration<ProductPriceHistoryEntity>
{
    public void Configure(EntityTypeBuilder<ProductPriceHistoryEntity> builder)
    {
        builder.ToTable("ProductPriceHistories", CatalogSchema.Name, table =>
        {
            table.HasCheckConstraint(
                "CK_catalog_ProductPriceHistories_OldPrice_NonNegative",
                "[OldPrice] IS NULL OR [OldPrice] >= 0");
            table.HasCheckConstraint(
                "CK_catalog_ProductPriceHistories_NewPrice_NonNegative",
                "[NewPrice] >= 0");
            table.HasCheckConstraint(
                "CK_catalog_ProductPriceHistories_CurrencyCode_Length",
                "LEN([CurrencyCode]) = 3");
            table.HasCheckConstraint(
                "CK_catalog_ProductPriceHistories_ChangeOrigin_NotBlank",
                "LEN(LTRIM(RTRIM([ChangeOrigin]))) > 0");
        });

        builder.HasKey(history => history.Id)
            .HasName("PK_catalog_ProductPriceHistories");
        builder.Property(history => history.Id).UseIdentityColumn();
        builder.Property(history => history.ProductId).IsRequired();
        builder.Property(history => history.OldPrice)
            .HasColumnType("decimal(19,4)");
        builder.Property(history => history.NewPrice)
            .HasColumnType("decimal(19,4)")
            .IsRequired();
        builder.Property(history => history.CurrencyCode)
            .HasColumnType("nvarchar(3)")
            .HasMaxLength(3)
            .IsRequired();
        builder.Property(history => history.ChangeOrigin)
            .HasColumnType("nvarchar(32)")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(history => history.Reason)
            .HasColumnType("nvarchar(256)")
            .HasMaxLength(256);
        builder.Property(history => history.EffectiveAtUtc)
            .HasColumnType("datetime2(7)")
            .IsRequired();

        builder.HasOne(history => history.Product)
            .WithMany(product => product.PriceHistory)
            .HasForeignKey(history => history.ProductId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_catalog_ProductPriceHistories_Products_ProductId");

        builder.HasIndex(history => new { history.ProductId, history.EffectiveAtUtc })
            .HasDatabaseName("IX_catalog_ProductPriceHistories_ProductId_EffectiveAtUtc");
    }
}
