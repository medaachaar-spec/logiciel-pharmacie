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
    THROW 51120, 'Refus du nettoyage Inventory : la base cible doit être Pharmacy_Development.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LocationId bigint =
        (SELECT Id FROM inventory.StockLocations WHERE Code = N'DEMO-LOCATION-MAIN');

    DECLARE @PurgeableBalances table
    (
        BalanceId bigint NOT NULL PRIMARY KEY,
        ProductId bigint NOT NULL,
        StockBatchId bigint NULL
    );

    INSERT INTO @PurgeableBalances (BalanceId, ProductId, StockBatchId)
    SELECT balance.Id, balance.ProductId, balance.StockBatchId
    FROM inventory.StockBalances AS balance
    JOIN catalog.ProductLocalSettings AS settings ON settings.ProductId = balance.ProductId
    WHERE balance.StockLocationId = @LocationId
      AND settings.InternalCode IN
      (N'DEMO-000001', N'DEMO-000002', N'DEMO-000003', N'DEMO-000004', N'DEMO-000005',
       N'DEMO-000006', N'DEMO-000007', N'DEMO-000008', N'DEMO-000009', N'DEMO-000010')
      AND NOT EXISTS
      (
          SELECT 1 FROM inventory.StockMovements AS movement
          WHERE movement.StockLocationId = balance.StockLocationId
            AND movement.ProductId = balance.ProductId
            AND ((movement.StockBatchId = balance.StockBatchId)
                 OR (movement.StockBatchId IS NULL AND balance.StockBatchId IS NULL))
            AND NOT (movement.SourceType = N'SyntheticSeed' AND movement.SourceReference = N'DEMO-S11-SEED')
      )
      AND NOT EXISTS
      (
          SELECT 1
          FROM inventory.InventoryCountLines AS line
          JOIN inventory.InventorySessions AS session ON session.Id = line.InventorySessionId
          WHERE session.StockLocationId = balance.StockLocationId
            AND line.ProductId = balance.ProductId
            AND ((line.StockBatchId = balance.StockBatchId)
                 OR (line.StockBatchId IS NULL AND balance.StockBatchId IS NULL))
      );

    DELETE movement
    FROM inventory.StockMovements AS movement
    JOIN @PurgeableBalances AS purgeable ON purgeable.ProductId = movement.ProductId
      AND ((purgeable.StockBatchId = movement.StockBatchId)
           OR (purgeable.StockBatchId IS NULL AND movement.StockBatchId IS NULL))
    WHERE movement.StockLocationId = @LocationId
      AND movement.SourceType = N'SyntheticSeed'
      AND movement.SourceReference = N'DEMO-S11-SEED';

    DELETE balance
    FROM inventory.StockBalances AS balance
    JOIN @PurgeableBalances AS purgeable ON purgeable.BalanceId = balance.Id;

    DELETE batch
    FROM inventory.StockBatches AS batch
    WHERE batch.BatchNumber IN
        (N'DEMO-LOT-A01', N'DEMO-LOT-A02', N'DEMO-LOT-EXPIRED', N'DEMO-LOT-B01', N'DEMO-LOT-C01')
      AND NOT EXISTS (SELECT 1 FROM inventory.StockBalances AS balance WHERE balance.StockBatchId = batch.Id)
      AND NOT EXISTS (SELECT 1 FROM inventory.StockMovements AS movement WHERE movement.StockBatchId = batch.Id)
      AND NOT EXISTS (SELECT 1 FROM inventory.InventoryCountLines AS line WHERE line.StockBatchId = batch.Id);

    DELETE FROM inventory.StockLocations
    WHERE Id = @LocationId
      AND NOT EXISTS (SELECT 1 FROM inventory.StockBalances WHERE StockLocationId = @LocationId)
      AND NOT EXISTS (SELECT 1 FROM inventory.StockMovements WHERE StockLocationId = @LocationId)
      AND NOT EXISTS (SELECT 1 FROM inventory.InventorySessions WHERE StockLocationId = @LocationId);

    UPDATE settings
    SET TracksLots = 0, TracksExpiry = 0
    FROM catalog.ProductLocalSettings AS settings
    WHERE settings.InternalCode IN (N'DEMO-000001', N'DEMO-000002', N'DEMO-000003')
      AND NOT EXISTS (SELECT 1 FROM inventory.StockBalances AS balance WHERE balance.ProductId = settings.ProductId)
      AND NOT EXISTS (SELECT 1 FROM inventory.StockBatches AS batch WHERE batch.ProductId = settings.ProductId);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT N'Nettoyage Inventory Demo terminé ; toute dépendance non Demo a été conservée.' AS Result;
