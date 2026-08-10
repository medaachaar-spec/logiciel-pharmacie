using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Sales;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Sales;

internal sealed class SaleConfiguration : IEntityTypeConfiguration<SaleEntity>
{
    public void Configure(EntityTypeBuilder<SaleEntity> builder)
    {
        builder.ToTable("Sales", SalesSchema.Name, table =>
        {
            table.HasCheckConstraint("CK_sales_Sales_Reference_NotBlank", "LEN(LTRIM(RTRIM([Reference]))) > 0");
            table.HasCheckConstraint("CK_sales_Sales_Status_Completed", "[Status] = N'Completed'");
            table.HasCheckConstraint("CK_sales_Sales_PaymentMethod", "[PaymentMethod] IN (N'Cash', N'ExternalCard')");
            table.HasCheckConstraint("CK_sales_Sales_Amounts_NonNegative", "[GrandTotal] >= 0 AND [PaidTotal] >= 0 AND [ChangeGiven] >= 0");
            table.HasCheckConstraint("CK_sales_Sales_Totals_Coherent", "[PaidTotal] = [GrandTotal]");
            table.HasCheckConstraint("CK_sales_Sales_Counts_Positive", "[TotalQuantity] > 0 AND [LineCount] > 0");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_sales_Sales");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.Reference).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.OperationId).IsRequired();
        builder.Property(entity => entity.Status).HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.CompletedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.GrandTotal).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(entity => entity.PaidTotal).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(entity => entity.ChangeGiven).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(entity => entity.TotalQuantity).HasColumnType("bigint").IsRequired();
        builder.Property(entity => entity.LineCount).HasColumnType("int").IsRequired();
        builder.Property(entity => entity.PaymentMethod).HasConversion<string>().HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        builder.HasIndex(entity => entity.Reference).IsUnique().HasDatabaseName("UX_sales_Sales_Reference");
        builder.HasIndex(entity => entity.OperationId).IsUnique().HasDatabaseName("UX_sales_Sales_OperationId");
        builder.HasIndex(entity => new { entity.CompletedAtUtc, entity.Id }).HasDatabaseName("IX_sales_Sales_CompletedAtUtc_Id");
        builder.HasOne<SaleOperationEntity>().WithOne()
            .HasPrincipalKey<SaleOperationEntity>(operation => operation.OperationId)
            .HasForeignKey<SaleEntity>(sale => sale.OperationId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_sales_Sales_SaleOperations_OperationId");
    }
}

internal sealed class SaleLineConfiguration : IEntityTypeConfiguration<SaleLineEntity>
{
    public void Configure(EntityTypeBuilder<SaleLineEntity> builder)
    {
        builder.ToTable("SaleLines", SalesSchema.Name, table =>
        {
            table.HasCheckConstraint("CK_sales_SaleLines_Reference_NotBlank", "LEN(LTRIM(RTRIM([ProductReferenceSnapshot]))) > 0");
            table.HasCheckConstraint("CK_sales_SaleLines_Name_NotBlank", "LEN(LTRIM(RTRIM([ProductNameSnapshot]))) > 0");
            table.HasCheckConstraint("CK_sales_SaleLines_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_sales_SaleLines_Amounts_NonNegative", "[UnitPrice] >= 0 AND [LineTotal] >= 0");
            table.HasCheckConstraint("CK_sales_SaleLines_Total_Coherent", "[LineTotal] = [UnitPrice] * [Quantity]");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_sales_SaleLines");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.SaleId).IsRequired();
        builder.Property(entity => entity.ProductId).IsRequired();
        builder.Property(entity => entity.ProductReferenceSnapshot).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.ProductNameSnapshot).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.PresentationSnapshot).HasColumnType("nvarchar(256)").HasMaxLength(256);
        builder.Property(entity => entity.UnitPrice).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(entity => entity.Quantity).HasColumnType("int").IsRequired();
        builder.Property(entity => entity.LineTotal).HasColumnType("decimal(19,4)").IsRequired();
        builder.HasOne(entity => entity.Sale).WithMany(sale => sale.Lines)
            .HasForeignKey(entity => entity.SaleId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_sales_SaleLines_Sales_SaleId");
        builder.HasOne(entity => entity.Product).WithMany()
            .HasForeignKey(entity => entity.ProductId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_sales_SaleLines_Products_ProductId");
        builder.HasIndex(entity => entity.SaleId).HasDatabaseName("IX_sales_SaleLines_SaleId");
        builder.HasIndex(entity => entity.ProductId).HasDatabaseName("IX_sales_SaleLines_ProductId");
    }
}

internal sealed class SaleLineAllocationConfiguration : IEntityTypeConfiguration<SaleLineAllocationEntity>
{
    public void Configure(EntityTypeBuilder<SaleLineAllocationEntity> builder)
    {
        builder.ToTable("SaleLineAllocations", SalesSchema.Name, table =>
            table.HasCheckConstraint("CK_sales_SaleLineAllocations_Quantity_Positive", "[Quantity] > 0"));
        builder.HasKey(entity => entity.Id).HasName("PK_sales_SaleLineAllocations");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.SaleLineId).IsRequired();
        builder.Property(entity => entity.StockBalanceId).IsRequired();
        builder.Property(entity => entity.StockBatchId);
        builder.Property(entity => entity.Quantity).HasColumnType("int").IsRequired();
        builder.Property(entity => entity.BatchNumberSnapshot).HasColumnType("nvarchar(128)").HasMaxLength(128);
        builder.Property(entity => entity.ExpiryDateSnapshot).HasColumnType("date");
        builder.HasOne(entity => entity.SaleLine).WithMany(line => line.Allocations)
            .HasForeignKey(entity => entity.SaleLineId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_sales_SaleLineAllocations_SaleLines_SaleLineId");
        builder.HasOne(entity => entity.StockBalance).WithMany()
            .HasForeignKey(entity => entity.StockBalanceId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_sales_SaleLineAllocations_StockBalances_StockBalanceId");
        builder.HasOne(entity => entity.StockBatch).WithMany()
            .HasForeignKey(entity => entity.StockBatchId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_sales_SaleLineAllocations_StockBatches_StockBatchId");
        builder.HasIndex(entity => new { entity.SaleLineId, entity.StockBalanceId })
            .IsUnique().HasDatabaseName("UX_sales_SaleLineAllocations_Line_Balance");
        builder.HasIndex(entity => entity.StockBalanceId).HasDatabaseName("IX_sales_SaleLineAllocations_StockBalanceId");
        builder.HasIndex(entity => entity.StockBatchId).HasDatabaseName("IX_sales_SaleLineAllocations_StockBatchId");
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<PaymentEntity>
{
    public void Configure(EntityTypeBuilder<PaymentEntity> builder)
    {
        builder.ToTable("Payments", SalesSchema.Name, table =>
        {
            table.HasCheckConstraint("CK_sales_Payments_Status_Completed", "[Status] = N'Completed'");
            table.HasCheckConstraint("CK_sales_Payments_PaymentMethod", "[PaymentMethod] IN (N'Cash', N'ExternalCard')");
            table.HasCheckConstraint("CK_sales_Payments_Amounts_NonNegative", "[Amount] >= 0 AND ([TenderedAmount] IS NULL OR [TenderedAmount] >= 0) AND [ChangeGiven] >= 0");
            table.HasCheckConstraint("CK_sales_Payments_Cash_Coherent", "([PaymentMethod] <> N'Cash') OR ([TenderedAmount] IS NOT NULL AND [TenderedAmount] >= [Amount] AND [ChangeGiven] = [TenderedAmount] - [Amount] AND [ExternalCardConfirmed] = 0)");
            table.HasCheckConstraint("CK_sales_Payments_Card_Coherent", "([PaymentMethod] <> N'ExternalCard') OR ([ExternalCardConfirmed] = 1 AND [TenderedAmount] IS NULL AND [ChangeGiven] = 0)");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_sales_Payments");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.SaleId).IsRequired();
        builder.Property(entity => entity.OperationId).IsRequired();
        builder.Property(entity => entity.PaymentMethod).HasConversion<string>().HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Status).HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Amount).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(entity => entity.TenderedAmount).HasColumnType("decimal(19,4)");
        builder.Property(entity => entity.ChangeGiven).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(entity => entity.ExternalCardConfirmed).IsRequired();
        builder.Property(entity => entity.ConfirmedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.HasOne(entity => entity.Sale).WithMany(sale => sale.Payments)
            .HasForeignKey(entity => entity.SaleId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_sales_Payments_Sales_SaleId");
        builder.HasIndex(entity => entity.OperationId).IsUnique().HasDatabaseName("UX_sales_Payments_OperationId");
        builder.HasIndex(entity => entity.SaleId).HasDatabaseName("IX_sales_Payments_SaleId");
    }
}

internal sealed class SaleOperationConfiguration : IEntityTypeConfiguration<SaleOperationEntity>
{
    public void Configure(EntityTypeBuilder<SaleOperationEntity> builder)
    {
        builder.ToTable("SaleOperations", SalesSchema.Name, table =>
        {
            table.HasCheckConstraint("CK_sales_SaleOperations_Fingerprint_NotBlank", "LEN(LTRIM(RTRIM([OperationFingerprint]))) > 0");
            table.HasCheckConstraint("CK_sales_SaleOperations_Status", "[Status] IN (N'Processing', N'Completed')");
            table.HasCheckConstraint("CK_sales_SaleOperations_Completion_Coherent", "([Status] = N'Processing' AND [ResultSaleId] IS NULL AND [CompletedAtUtc] IS NULL) OR ([Status] = N'Completed' AND [ResultSaleId] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL)");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_sales_SaleOperations");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.OperationId).IsRequired();
        builder.Property(entity => entity.OperationFingerprint).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Status).HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.ResultSaleId);
        builder.Property(entity => entity.CreatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.CompletedAtUtc).HasColumnType("datetime2(7)");
        builder.HasAlternateKey(entity => entity.OperationId).HasName("AK_sales_SaleOperations_OperationId");
        builder.HasOne(entity => entity.ResultSale).WithOne()
            .HasForeignKey<SaleOperationEntity>(entity => entity.ResultSaleId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_sales_SaleOperations_Sales_ResultSaleId");
        builder.HasIndex(entity => entity.ResultSaleId).IsUnique()
            .HasFilter("[ResultSaleId] IS NOT NULL")
            .HasDatabaseName("UX_sales_SaleOperations_ResultSaleId");
        builder.HasIndex(entity => entity.CreatedAtUtc).HasDatabaseName("IX_sales_SaleOperations_CreatedAtUtc");
    }
}
