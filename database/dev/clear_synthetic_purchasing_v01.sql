SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
IF DB_NAME() <> N'Pharmacy_Development'
    THROW 51220, 'Refus du clear Purchasing : la base cible doit être Pharmacy_Development.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @SeedLines table (MovementId bigint PRIMARY KEY, ProductId bigint, StockBatchId bigint NULL, StockLocationId bigint, Quantity int);
    INSERT @SeedLines SELECT l.StockMovementId,l.ProductId,l.StockBatchId,r.StockLocationId,l.Quantity
    FROM purchasing.GoodsReceiptLines l JOIN purchasing.GoodsReceipts r ON r.Id=l.GoodsReceiptId
    WHERE r.OperationId='12000000-0000-0000-0000-000000000001';

    IF EXISTS (SELECT 1 FROM @SeedLines s LEFT JOIN inventory.StockBalances b ON b.StockLocationId=s.StockLocationId AND b.ProductId=s.ProductId AND b.StockBatchId=s.StockBatchId WHERE b.Id IS NULL OR b.AvailableQuantity<s.Quantity)
        THROW 51221, 'Clear ciblé impossible : la balance Demo Purchasing est absente ou insuffisante.', 1;

    DELETE l FROM purchasing.GoodsReceiptLines l JOIN purchasing.GoodsReceipts r ON r.Id=l.GoodsReceiptId WHERE r.OperationId='12000000-0000-0000-0000-000000000001';
    DELETE FROM purchasing.GoodsReceipts WHERE OperationId='12000000-0000-0000-0000-000000000001';
    DELETE m FROM inventory.StockMovements m JOIN @SeedLines s ON s.MovementId=m.Id;
    UPDATE b SET AvailableQuantity=b.AvailableQuantity-s.Quantity,UpdatedAtUtc=SYSUTCDATETIME()
    FROM inventory.StockBalances b JOIN @SeedLines s ON b.StockLocationId=s.StockLocationId AND b.ProductId=s.ProductId AND b.StockBatchId=s.StockBatchId;
    DELETE b FROM inventory.StockBalances b JOIN @SeedLines s ON b.StockLocationId=s.StockLocationId AND b.ProductId=s.ProductId AND b.StockBatchId=s.StockBatchId WHERE b.AvailableQuantity=0;
    DELETE b FROM inventory.StockBatches b WHERE b.CreationOperationId='12000000-0000-0000-0000-000000000101' AND NOT EXISTS(SELECT 1 FROM inventory.StockBalances sb WHERE sb.StockBatchId=b.Id) AND NOT EXISTS(SELECT 1 FROM inventory.StockMovements sm WHERE sm.StockBatchId=b.Id);

    DELETE l FROM purchasing.PurchaseOrderLines l JOIN purchasing.PurchaseOrders o ON o.Id=l.PurchaseOrderId WHERE o.OrderNumber IN (N'DEMO-S12-PO-001',N'DEMO-S12-PO-002');
    DELETE FROM purchasing.PurchaseOrders WHERE OrderNumber IN (N'DEMO-S12-PO-001',N'DEMO-S12-PO-002');
    DELETE s FROM purchasing.Suppliers s WHERE s.Code LIKE N'DEMO-S12-%' AND NOT EXISTS(SELECT 1 FROM purchasing.PurchaseOrders o WHERE o.SupplierId=s.Id) AND NOT EXISTS(SELECT 1 FROM purchasing.GoodsReceipts r WHERE r.SupplierId=s.Id);
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
SELECT N'Clear Purchasing synthétique V0.1 terminé sans toucher au Catalogue ni aux autres stocks.' AS Result;
