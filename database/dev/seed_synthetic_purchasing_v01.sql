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
    THROW 51210, 'Refus du seed Purchasing : la base cible doit être Pharmacy_Development.', 1;
IF OBJECT_ID(N'purchasing.GoodsReceiptLines', N'U') IS NULL
    THROW 51211, 'La migration PurchasingPersistenceV01 doit être appliquée avant le seed.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Suppliers table (Code nvarchar(32) PRIMARY KEY, Name nvarchar(200), Phone nvarchar(32), Email nvarchar(254));
    INSERT INTO @Suppliers VALUES
        (N'DEMO-S12-ALPHA', N'Distribution Alpha Demo', N'+212 500 000 101', N'alpha.demo@example.invalid'),
        (N'DEMO-S12-BETA', N'Répartition Beta Demo', N'+212 500 000 102', N'beta.demo@example.invalid'),
        (N'DEMO-S12-GAMMA', N'Laboratoire Gamma Demo', NULL, N'gamma.demo@example.invalid'),
        (N'DEMO-S12-DELTA', N'Fournitures Delta Demo', NULL, NULL);

    INSERT INTO purchasing.Suppliers (Code, Name, Phone, Email, Address, IsActive, CreatedAtUtc, UpdatedAtUtc)
    SELECT Code, Name, Phone, Email, N'Adresse entièrement synthétique — Demo', 1, SYSUTCDATETIME(), SYSUTCDATETIME()
    FROM @Suppliers source WHERE NOT EXISTS (SELECT 1 FROM purchasing.Suppliers target WHERE target.Code=source.Code);

    DECLARE @SupplierId bigint=(SELECT Id FROM purchasing.Suppliers WHERE Code=N'DEMO-S12-ALPHA');
    DECLARE @SupplierBetaId bigint=(SELECT Id FROM purchasing.Suppliers WHERE Code=N'DEMO-S12-BETA');
    DECLARE @ProductA bigint=(SELECT ProductId FROM catalog.ProductLocalSettings WHERE InternalCode=N'DEMO-000001');
    DECLARE @ProductB bigint=(SELECT ProductId FROM catalog.ProductLocalSettings WHERE InternalCode=N'DEMO-000002');
    DECLARE @LocationId bigint=(SELECT Id FROM inventory.StockLocations WHERE Code=N'DEMO-LOCATION-MAIN');
    IF @SupplierId IS NULL OR @SupplierBetaId IS NULL OR @ProductA IS NULL OR @ProductB IS NULL OR @LocationId IS NULL
        THROW 51212, 'Les seeds Catalogue et Inventory doivent être appliqués avant Purchasing.', 1;

    IF NOT EXISTS (SELECT 1 FROM purchasing.PurchaseOrders WHERE OrderNumber=N'DEMO-S12-PO-001')
        INSERT purchasing.PurchaseOrders (OrderNumber,SupplierId,Status,OrderDate,ExpectedDate,Notes,CreatedAtUtc,UpdatedAtUtc,ConfirmedAtUtc)
        VALUES (N'DEMO-S12-PO-001',@SupplierId,N'Confirmed','2026-08-01','2026-08-10',N'Commande Demo destinée à une réception partielle.',SYSUTCDATETIME(),SYSUTCDATETIME(),SYSUTCDATETIME());
    IF NOT EXISTS (SELECT 1 FROM purchasing.PurchaseOrders WHERE OrderNumber=N'DEMO-S12-PO-002')
        INSERT purchasing.PurchaseOrders (OrderNumber,SupplierId,Status,OrderDate,ExpectedDate,Notes,CreatedAtUtc,UpdatedAtUtc)
        VALUES (N'DEMO-S12-PO-002',@SupplierBetaId,N'Draft','2026-08-02','2026-08-15',N'Brouillon Demo modifiable.',SYSUTCDATETIME(),SYSUTCDATETIME());

    DECLARE @OrderId bigint=(SELECT Id FROM purchasing.PurchaseOrders WHERE OrderNumber=N'DEMO-S12-PO-001');
    DECLARE @DraftOrderId bigint=(SELECT Id FROM purchasing.PurchaseOrders WHERE OrderNumber=N'DEMO-S12-PO-002');
    IF NOT EXISTS (SELECT 1 FROM purchasing.PurchaseOrderLines WHERE PurchaseOrderId=@OrderId AND ProductId=@ProductA)
        INSERT purchasing.PurchaseOrderLines (PurchaseOrderId,ProductId,ProductCodeSnapshot,ProductNameSnapshot,OrderedQuantity,ReceivedQuantity,UnitPurchasePrice,CreatedAtUtc,UpdatedAtUtc)
        SELECT @OrderId,@ProductA,N'DEMO-000001',p.Name,10,0,7.2500,SYSUTCDATETIME(),SYSUTCDATETIME() FROM catalog.Products p WHERE p.Id=@ProductA;
    IF NOT EXISTS (SELECT 1 FROM purchasing.PurchaseOrderLines WHERE PurchaseOrderId=@OrderId AND ProductId=@ProductB)
        INSERT purchasing.PurchaseOrderLines (PurchaseOrderId,ProductId,ProductCodeSnapshot,ProductNameSnapshot,OrderedQuantity,ReceivedQuantity,UnitPurchasePrice,CreatedAtUtc,UpdatedAtUtc)
        SELECT @OrderId,@ProductB,N'DEMO-000002',p.Name,6,0,11.5000,SYSUTCDATETIME(),SYSUTCDATETIME() FROM catalog.Products p WHERE p.Id=@ProductB;
    IF NOT EXISTS (SELECT 1 FROM purchasing.PurchaseOrderLines WHERE PurchaseOrderId=@DraftOrderId AND ProductId=@ProductB)
        INSERT purchasing.PurchaseOrderLines (PurchaseOrderId,ProductId,ProductCodeSnapshot,ProductNameSnapshot,OrderedQuantity,ReceivedQuantity,UnitPurchasePrice,CreatedAtUtc,UpdatedAtUtc)
        SELECT @DraftOrderId,@ProductB,N'DEMO-000002',p.Name,5,0,10.7500,SYSUTCDATETIME(),SYSUTCDATETIME() FROM catalog.Products p WHERE p.Id=@ProductB;

    DECLARE @ReceiptOperation uniqueidentifier='12000000-0000-0000-0000-000000000001';
    IF NOT EXISTS (SELECT 1 FROM purchasing.GoodsReceipts WHERE OperationId=@ReceiptOperation)
    BEGIN
        DECLARE @BatchOperation uniqueidentifier='12000000-0000-0000-0000-000000000101';
        IF NOT EXISTS (SELECT 1 FROM inventory.StockBatches WHERE ProductId=@ProductA AND BatchNumber=N'DEMO-S12-LOT-A01')
            INSERT inventory.StockBatches (CreationOperationId,CreationFingerprint,ProductId,BatchNumber,ExpiryDate,IsActive,CreatedAtUtc)
            VALUES (@BatchOperation,N'SEED|PURCHASING|DEMO-S12-LOT-A01',@ProductA,N'DEMO-S12-LOT-A01','2030-12-31',1,SYSUTCDATETIME());
        DECLARE @BatchId bigint=(SELECT Id FROM inventory.StockBatches WHERE ProductId=@ProductA AND BatchNumber=N'DEMO-S12-LOT-A01');
        DECLARE @Before int=COALESCE((SELECT AvailableQuantity FROM inventory.StockBalances WITH (UPDLOCK,HOLDLOCK) WHERE StockLocationId=@LocationId AND ProductId=@ProductA AND StockBatchId=@BatchId),0);
        IF NOT EXISTS (SELECT 1 FROM inventory.StockBalances WHERE StockLocationId=@LocationId AND ProductId=@ProductA AND StockBatchId=@BatchId)
            INSERT inventory.StockBalances (StockLocationId,ProductId,StockBatchId,AvailableQuantity,UpdatedAtUtc) VALUES (@LocationId,@ProductA,@BatchId,4,SYSUTCDATETIME());
        ELSE UPDATE inventory.StockBalances SET AvailableQuantity=AvailableQuantity+4,UpdatedAtUtc=SYSUTCDATETIME() WHERE StockLocationId=@LocationId AND ProductId=@ProductA AND StockBatchId=@BatchId;

        DECLARE @MovementOperation uniqueidentifier='12000000-0000-0000-0000-000000001001';
        INSERT inventory.StockMovements (OperationId,OperationFingerprint,ProductId,StockLocationId,StockBatchId,MovementType,Quantity,QuantityBefore,QuantityAfter,Reason,SourceType,SourceReference,OccurredAtUtc,CreatedAtUtc)
        VALUES (@MovementOperation,N'SEED|PURCHASING|RECEIPT|DEMO-S12-REC-001',@ProductA,@LocationId,@BatchId,N'PurchaseReceipt',4,@Before,@Before+4,N'Réception fournisseur Demo Sprint 12',N'GoodsReceipt',N'DEMO-S12-REC-001',SYSUTCDATETIME(),SYSUTCDATETIME());
        DECLARE @MovementId bigint=SCOPE_IDENTITY();

        INSERT purchasing.GoodsReceipts (ReceiptNumber,OperationId,OperationFingerprint,SupplierId,PurchaseOrderId,StockLocationId,ReceiptType,Status,DeliveryNoteNumber,DocumentDate,ReceivedAtUtc,CreatedAtUtc)
        VALUES (N'DEMO-S12-REC-001',@ReceiptOperation,N'SEED-S12-RECEIPT-001',@SupplierId,@OrderId,@LocationId,N'PurchaseOrder',N'Completed',N'DEMO-S12-BL-001','2026-08-04',SYSUTCDATETIME(),SYSUTCDATETIME());
        DECLARE @ReceiptId bigint=SCOPE_IDENTITY();
        DECLARE @OrderLineId bigint=(SELECT Id FROM purchasing.PurchaseOrderLines WHERE PurchaseOrderId=@OrderId AND ProductId=@ProductA);
        INSERT purchasing.GoodsReceiptLines (GoodsReceiptId,ProductId,PurchaseOrderLineId,Quantity,UnitPurchasePrice,ProductCodeSnapshot,ProductNameSnapshot,StockBatchId,LotNumberSnapshot,ExpiryDateSnapshot,StockMovementId,CreatedAtUtc)
        SELECT @ReceiptId,@ProductA,@OrderLineId,4,7.2500,N'DEMO-000001',p.Name,@BatchId,N'DEMO-S12-LOT-A01','2030-12-31',@MovementId,SYSUTCDATETIME() FROM catalog.Products p WHERE p.Id=@ProductA;
        UPDATE purchasing.PurchaseOrderLines SET ReceivedQuantity=4,UpdatedAtUtc=SYSUTCDATETIME() WHERE Id=@OrderLineId;
        UPDATE purchasing.PurchaseOrders SET Status=N'PartiallyReceived',UpdatedAtUtc=SYSUTCDATETIME() WHERE Id=@OrderId;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT N'Seed Purchasing synthétique V0.1 appliqué sans duplication.' AS Result;
