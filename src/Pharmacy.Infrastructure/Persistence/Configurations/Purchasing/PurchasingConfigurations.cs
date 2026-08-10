using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Purchasing;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Purchasing;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<SupplierEntity>
{
    public void Configure(EntityTypeBuilder<SupplierEntity> b)
    {
        b.ToTable("Suppliers", PurchasingSchema.Name, t => { t.HasCheckConstraint("CK_purchasing_Suppliers_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0"); t.HasCheckConstraint("CK_purchasing_Suppliers_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0"); });
        b.HasKey(x => x.Id).HasName("PK_purchasing_Suppliers"); b.Property(x => x.Id).UseIdentityColumn();
        b.Property(x => x.Code).HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired(); b.Property(x => x.Name).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        b.Property(x => x.Phone).HasColumnType("nvarchar(32)").HasMaxLength(32); b.Property(x => x.Email).HasColumnType("nvarchar(254)").HasMaxLength(254); b.Property(x => x.Address).HasColumnType("nvarchar(500)").HasMaxLength(500);
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)").HasDefaultValueSql("SYSUTCDATETIME()"); b.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(7)"); b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_purchasing_Suppliers_Code"); b.HasIndex(x => new { x.IsActive, x.Name }).HasDatabaseName("IX_purchasing_Suppliers_IsActive_Name");
    }
}

internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrderEntity>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderEntity> b)
    {
        b.ToTable("PurchaseOrders", PurchasingSchema.Name, t => t.HasCheckConstraint("CK_purchasing_PurchaseOrders_Number_NotBlank", "LEN(LTRIM(RTRIM([OrderNumber]))) > 0"));
        b.HasKey(x => x.Id).HasName("PK_purchasing_PurchaseOrders"); b.Property(x => x.Id).UseIdentityColumn(); b.Property(x => x.OrderNumber).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired(); b.Property(x => x.OrderDate).HasColumnType("date"); b.Property(x => x.ExpectedDate).HasColumnType("date");
        b.Property(x => x.Notes).HasColumnType("nvarchar(1000)").HasMaxLength(1000); b.Property(x => x.CancellationReason).HasColumnType("nvarchar(500)").HasMaxLength(500);
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)").HasDefaultValueSql("SYSUTCDATETIME()"); b.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(7)"); b.Property(x => x.ConfirmedAtUtc).HasColumnType("datetime2(7)"); b.Property(x => x.CancelledAtUtc).HasColumnType("datetime2(7)"); b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.HasOne(x => x.Supplier).WithMany(x => x.PurchaseOrders).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_purchasing_PurchaseOrders_Suppliers_SupplierId");
        b.HasIndex(x => x.OrderNumber).IsUnique().HasDatabaseName("UX_purchasing_PurchaseOrders_OrderNumber"); b.HasIndex(x => new { x.SupplierId, x.OrderDate }).HasDatabaseName("IX_purchasing_PurchaseOrders_SupplierId_OrderDate"); b.HasIndex(x => new { x.Status, x.OrderDate }).HasDatabaseName("IX_purchasing_PurchaseOrders_Status_OrderDate");
    }
}

internal sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLineEntity>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLineEntity> b)
    {
        b.ToTable("PurchaseOrderLines", PurchasingSchema.Name, t => { t.HasCheckConstraint("CK_purchasing_PurchaseOrderLines_Quantities", "[OrderedQuantity] > 0 AND [ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [OrderedQuantity]"); t.HasCheckConstraint("CK_purchasing_PurchaseOrderLines_Price", "[UnitPurchasePrice] IS NULL OR [UnitPurchasePrice] >= 0"); });
        b.HasKey(x => x.Id).HasName("PK_purchasing_PurchaseOrderLines"); b.Property(x => x.Id).UseIdentityColumn(); b.Property(x => x.ProductCodeSnapshot).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired(); b.Property(x => x.ProductNameSnapshot).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        b.Property(x => x.OrderedQuantity).HasColumnType("int"); b.Property(x => x.ReceivedQuantity).HasColumnType("int"); b.Property(x => x.UnitPurchasePrice).HasColumnType("decimal(19,4)"); b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)").HasDefaultValueSql("SYSUTCDATETIME()"); b.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(7)"); b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.HasOne(x => x.PurchaseOrder).WithMany(x => x.Lines).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_purchasing_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId"); b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_purchasing_PurchaseOrderLines_Products_ProductId");
        b.HasIndex(x => new { x.PurchaseOrderId, x.ProductId }).IsUnique().HasDatabaseName("UX_purchasing_PurchaseOrderLines_Order_Product"); b.HasIndex(x => x.ProductId).HasDatabaseName("IX_purchasing_PurchaseOrderLines_ProductId");
    }
}

internal sealed class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceiptEntity>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptEntity> b)
    {
        b.ToTable("GoodsReceipts", PurchasingSchema.Name, t => { t.HasCheckConstraint("CK_purchasing_GoodsReceipts_DeliveryNote_NotBlank", "LEN(LTRIM(RTRIM([DeliveryNoteNumber]))) > 0"); t.HasCheckConstraint("CK_purchasing_GoodsReceipts_Fingerprint_NotBlank", "LEN(LTRIM(RTRIM([OperationFingerprint]))) > 0"); });
        b.HasKey(x => x.Id).HasName("PK_purchasing_GoodsReceipts"); b.Property(x => x.Id).UseIdentityColumn(); b.Property(x => x.ReceiptNumber).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired(); b.Property(x => x.OperationId).IsRequired(); b.Property(x => x.OperationFingerprint).HasColumnType("nvarchar(1024)").HasMaxLength(1024).IsRequired();
        b.Property(x => x.ReceiptType).HasConversion<string>().HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired(); b.Property(x => x.Status).HasConversion<string>().HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired(); b.Property(x => x.DeliveryNoteNumber).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired(); b.Property(x => x.DocumentDate).HasColumnType("date"); b.Property(x => x.ReceivedAtUtc).HasColumnType("datetime2(7)"); b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)").HasDefaultValueSql("SYSUTCDATETIME()"); b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.HasOne(x => x.Supplier).WithMany(x => x.GoodsReceipts).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_purchasing_GoodsReceipts_Suppliers_SupplierId"); b.HasOne(x => x.PurchaseOrder).WithMany(x => x.GoodsReceipts).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_purchasing_GoodsReceipts_PurchaseOrders_PurchaseOrderId"); b.HasOne(x => x.StockLocation).WithMany().HasForeignKey(x => x.StockLocationId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_purchasing_GoodsReceipts_StockLocations_StockLocationId");
        b.HasIndex(x => x.ReceiptNumber).IsUnique().HasDatabaseName("UX_purchasing_GoodsReceipts_ReceiptNumber"); b.HasIndex(x => x.OperationId).IsUnique().HasDatabaseName("UX_purchasing_GoodsReceipts_OperationId"); b.HasIndex(x => new { x.SupplierId, x.DeliveryNoteNumber }).IsUnique().HasDatabaseName("UX_purchasing_GoodsReceipts_Supplier_DeliveryNote"); b.HasIndex(x => new { x.SupplierId, x.ReceivedAtUtc }).HasDatabaseName("IX_purchasing_GoodsReceipts_SupplierId_ReceivedAtUtc"); b.HasIndex(x => x.PurchaseOrderId).HasDatabaseName("IX_purchasing_GoodsReceipts_PurchaseOrderId"); b.HasIndex(x => x.StockLocationId).HasDatabaseName("IX_purchasing_GoodsReceipts_StockLocationId");
    }
}

internal sealed class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLineEntity>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLineEntity> b)
    {
        b.ToTable("GoodsReceiptLines", PurchasingSchema.Name, t => { t.HasCheckConstraint("CK_purchasing_GoodsReceiptLines_Quantity", "[Quantity] > 0"); t.HasCheckConstraint("CK_purchasing_GoodsReceiptLines_Price", "[UnitPurchasePrice] >= 0"); });
        b.HasKey(x => x.Id).HasName("PK_purchasing_GoodsReceiptLines"); b.Property(x => x.Id).UseIdentityColumn(); b.Property(x => x.Quantity).HasColumnType("int"); b.Property(x => x.UnitPurchasePrice).HasColumnType("decimal(19,4)"); b.Property(x => x.ProductCodeSnapshot).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired(); b.Property(x => x.ProductNameSnapshot).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(); b.Property(x => x.LotNumberSnapshot).HasColumnType("nvarchar(80)").HasMaxLength(80); b.Property(x => x.ExpiryDateSnapshot).HasColumnType("date"); b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasOne(x => x.GoodsReceipt).WithMany(x => x.Lines).HasForeignKey(x => x.GoodsReceiptId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_purchasing_GoodsReceiptLines_GoodsReceipts_GoodsReceiptId"); b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_purchasing_GoodsReceiptLines_Products_ProductId"); b.HasOne(x => x.PurchaseOrderLine).WithMany(x => x.GoodsReceiptLines).HasForeignKey(x => x.PurchaseOrderLineId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_purchasing_GoodsReceiptLines_PurchaseOrderLines_PurchaseOrderLineId"); b.HasOne(x => x.StockBatch).WithMany().HasForeignKey(x => x.StockBatchId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_purchasing_GoodsReceiptLines_StockBatches_StockBatchId"); b.HasOne(x => x.StockMovement).WithOne().HasForeignKey<GoodsReceiptLineEntity>(x => x.StockMovementId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_purchasing_GoodsReceiptLines_StockMovements_StockMovementId");
        b.HasIndex(x => new { x.GoodsReceiptId, x.ProductId }).HasDatabaseName("IX_purchasing_GoodsReceiptLines_Receipt_Product"); b.HasIndex(x => x.PurchaseOrderLineId).HasDatabaseName("IX_purchasing_GoodsReceiptLines_PurchaseOrderLineId"); b.HasIndex(x => x.StockBatchId).HasDatabaseName("IX_purchasing_GoodsReceiptLines_StockBatchId"); b.HasIndex(x => x.StockMovementId).IsUnique().HasDatabaseName("UX_purchasing_GoodsReceiptLines_StockMovementId");
    }
}
