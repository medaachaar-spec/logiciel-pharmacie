using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Sales;
using Pharmacy.Infrastructure.Persistence.Entities.Security;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Sales;

internal sealed class SuspendedSaleConfiguration : IEntityTypeConfiguration<SuspendedSaleEntity>
{
    public void Configure(EntityTypeBuilder<SuspendedSaleEntity> builder)
    {
        builder.ToTable("SuspendedSales", SalesSchema.Name, table =>
        {
            table.HasCheckConstraint("CK_sales_SuspendedSales_Status", "[Status] IN (N'Open', N'Resumed', N'Abandoned')");
            table.HasCheckConstraint("CK_sales_SuspendedSales_Reference_NotBlank", "LEN(LTRIM(RTRIM([Reference]))) > 0");
            table.HasCheckConstraint("CK_sales_SuspendedSales_GrandTotal_NonNegative", "[GrandTotal] >= 0");
            table.HasCheckConstraint("CK_sales_SuspendedSales_LineCount_Positive", "[LineCount] > 0");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_sales_SuspendedSales");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.OperationId).IsRequired();
        builder.Property(entity => entity.Reference).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.CustomerId);
        builder.Property(entity => entity.CustomerNameSnapshot).HasColumnType("nvarchar(256)").HasMaxLength(256);
        builder.Property(entity => entity.GrandTotal).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(entity => entity.LineCount).HasColumnType("int").IsRequired();
        builder.Property(entity => entity.Status).HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.SuspendedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.UpdatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => entity.OperationId).IsUnique().HasDatabaseName("UX_sales_SuspendedSales_OperationId");
        builder.HasIndex(entity => entity.Reference).IsUnique().HasDatabaseName("UX_sales_SuspendedSales_Reference");
        builder.HasIndex(entity => new { entity.Status, entity.SuspendedAtUtc, entity.Id }).HasDatabaseName("IX_sales_SuspendedSales_Status_Timeline");
    }
}

internal sealed class SuspendedSaleLineConfiguration : IEntityTypeConfiguration<SuspendedSaleLineEntity>
{
    public void Configure(EntityTypeBuilder<SuspendedSaleLineEntity> builder)
    {
        builder.ToTable("SuspendedSaleLines", SalesSchema.Name, table =>
        {
            table.HasCheckConstraint("CK_sales_SuspendedSaleLines_Reference_NotBlank", "LEN(LTRIM(RTRIM([ProductReferenceSnapshot]))) > 0");
            table.HasCheckConstraint("CK_sales_SuspendedSaleLines_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_sales_SuspendedSaleLines_Total_Coherent", "[LineTotal] = [UnitPrice] * [Quantity]");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_sales_SuspendedSaleLines");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.SuspendedSaleId).IsRequired();
        builder.Property(entity => entity.ProductId).IsRequired();
        builder.Property(entity => entity.ProductReferenceSnapshot).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.ProductNameSnapshot).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.PresentationSnapshot).HasColumnType("nvarchar(256)").HasMaxLength(256);
        builder.Property(entity => entity.UnitPrice).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(entity => entity.Quantity).HasColumnType("int").IsRequired();
        builder.Property(entity => entity.LineTotal).HasColumnType("decimal(19,4)").IsRequired();
        builder.HasOne(entity => entity.SuspendedSale).WithMany(sale => sale.Lines)
            .HasForeignKey(entity => entity.SuspendedSaleId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_sales_SuspendedSaleLines_SuspendedSales_SuspendedSaleId");
    }
}

internal sealed class SaleReturnConfiguration : IEntityTypeConfiguration<SaleReturnEntity>
{
    public void Configure(EntityTypeBuilder<SaleReturnEntity> builder)
    {
        builder.ToTable("SaleReturns", SalesSchema.Name, table =>
        {
            table.HasCheckConstraint("CK_sales_SaleReturns_Status", "[Status] = N'Completed'");
            table.HasCheckConstraint("CK_sales_SaleReturns_Reference_NotBlank", "LEN(LTRIM(RTRIM([Reference]))) > 0");
            table.HasCheckConstraint("CK_sales_SaleReturns_RefundAmount_NonNegative", "[RefundAmount] >= 0");
            table.HasCheckConstraint("CK_sales_SaleReturns_RefundMethod", "[RefundMethod] IN (N'Cash', N'ExternalCardPending', N'CreditReduction')");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_sales_SaleReturns");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.OperationId).IsRequired();
        builder.Property(entity => entity.Reference).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.OriginalSaleId).IsRequired();
        builder.Property(entity => entity.Status).HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.RefundAmount).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(entity => entity.RefundMethod).HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.CompletedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.PerformedByUserAccountId);
        builder.HasIndex(entity => entity.OperationId).IsUnique().HasDatabaseName("UX_sales_SaleReturns_OperationId");
        builder.HasIndex(entity => entity.Reference).IsUnique().HasDatabaseName("UX_sales_SaleReturns_Reference");
        builder.HasIndex(entity => entity.OriginalSaleId).HasDatabaseName("IX_sales_SaleReturns_OriginalSaleId");
        builder.HasIndex(entity => new { entity.PerformedByUserAccountId, entity.CompletedAtUtc, entity.Id })
            .HasDatabaseName("IX_sales_SaleReturns_Actor_Timeline");
        builder.HasOne<UserAccountEntity>().WithMany()
            .HasForeignKey(entity => entity.PerformedByUserAccountId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_sales_SaleReturns_UserAccounts_PerformedByUserAccountId");
    }
}

internal sealed class SaleReturnLineConfiguration : IEntityTypeConfiguration<SaleReturnLineEntity>
{
    public void Configure(EntityTypeBuilder<SaleReturnLineEntity> builder)
    {
        builder.ToTable("SaleReturnLines", SalesSchema.Name, table =>
        {
            table.HasCheckConstraint("CK_sales_SaleReturnLines_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_sales_SaleReturnLines_Total_Coherent", "[LineTotal] = [UnitPrice] * [Quantity]");
            table.HasCheckConstraint("CK_sales_SaleReturnLines_StockDestination", "[StockDestination] IN (N'RestockApproved', N'NotRestockable')");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_sales_SaleReturnLines");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.SaleReturnId).IsRequired();
        builder.Property(entity => entity.SaleLineId).IsRequired();
        builder.Property(entity => entity.ProductId).IsRequired();
        builder.Property(entity => entity.Quantity).HasColumnType("int").IsRequired();
        builder.Property(entity => entity.UnitPrice).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(entity => entity.LineTotal).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(entity => entity.StockDestination).HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        builder.HasOne(entity => entity.SaleReturn).WithMany(saleReturn => saleReturn.Lines)
            .HasForeignKey(entity => entity.SaleReturnId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_sales_SaleReturnLines_SaleReturns_SaleReturnId");
        builder.HasIndex(entity => entity.SaleLineId).HasDatabaseName("IX_sales_SaleReturnLines_SaleLineId");
    }
}
