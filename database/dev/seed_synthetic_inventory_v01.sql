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
    THROW 51110, 'Refus du seed Inventory : la base cible doit être Pharmacy_Development.', 1;

IF OBJECT_ID(N'inventory.StockLocations', N'U') IS NULL
    OR OBJECT_ID(N'inventory.StockBatches', N'U') IS NULL
    OR OBJECT_ID(N'inventory.StockBalances', N'U') IS NULL
    OR OBJECT_ID(N'inventory.StockMovements', N'U') IS NULL
    THROW 51111, 'La migration InventoryPersistenceV01 doit être appliquée avant le seed.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LocationOperationId uniqueidentifier = '11000000-0000-0000-0000-000000000001';

    IF NOT EXISTS (SELECT 1 FROM inventory.StockLocations WHERE Code = N'DEMO-LOCATION-MAIN')
    BEGIN
        INSERT INTO inventory.StockLocations
            (CreationOperationId, CreationFingerprint, Code, Name, IsActive, IsPrimary, CreatedAtUtc)
        VALUES
            (@LocationOperationId, N'SEED|LOCATION|DEMO-LOCATION-MAIN', N'DEMO-LOCATION-MAIN',
             N'Emplacement principal Demo', 1, 1, SYSUTCDATETIME());
    END;

    DECLARE @LocationId bigint =
        (SELECT Id FROM inventory.StockLocations WHERE Code = N'DEMO-LOCATION-MAIN');

    IF @LocationId IS NULL
        THROW 51112, 'L''emplacement Demo n''a pas pu être résolu.', 1;

    DECLARE @Products table
    (
        InternalCode nvarchar(128) NOT NULL PRIMARY KEY,
        ProductId bigint NOT NULL,
        TracksLots bit NOT NULL,
        TracksExpiry bit NOT NULL
    );

    INSERT INTO @Products (InternalCode, ProductId, TracksLots, TracksExpiry)
    SELECT settings.InternalCode, settings.ProductId,
           CASE WHEN settings.InternalCode IN (N'DEMO-000001', N'DEMO-000002', N'DEMO-000003') THEN 1 ELSE 0 END,
           CASE WHEN settings.InternalCode IN (N'DEMO-000001', N'DEMO-000002') THEN 1 ELSE 0 END
    FROM catalog.ProductLocalSettings AS settings
    WHERE settings.InternalCode IN
    (
        N'DEMO-000001', N'DEMO-000002', N'DEMO-000003', N'DEMO-000004', N'DEMO-000005',
        N'DEMO-000006', N'DEMO-000007', N'DEMO-000008', N'DEMO-000009', N'DEMO-000010'
    );

    IF (SELECT COUNT(*) FROM @Products) <> 10
        THROW 51113, 'Les dix produits canoniques DEMO-000001 à DEMO-000010 sont requis.', 1;

    UPDATE settings
    SET TracksLots = source.TracksLots,
        TracksExpiry = source.TracksExpiry
    FROM catalog.ProductLocalSettings AS settings
    JOIN @Products AS source ON source.ProductId = settings.ProductId
    WHERE settings.TracksLots <> source.TracksLots
       OR settings.TracksExpiry <> source.TracksExpiry;

    DECLARE @Batches table
    (
        InternalCode nvarchar(128) NOT NULL,
        BatchNumber nvarchar(128) NOT NULL PRIMARY KEY,
        ExpiryDate date NULL,
        CreationOperationId uniqueidentifier NOT NULL
    );

    INSERT INTO @Batches (InternalCode, BatchNumber, ExpiryDate, CreationOperationId)
    VALUES
        (N'DEMO-000001', N'DEMO-LOT-A01', '2028-12-31', '11000000-0000-0000-0000-000000000101'),
        (N'DEMO-000001', N'DEMO-LOT-A02', '2029-06-30', '11000000-0000-0000-0000-000000000102'),
        (N'DEMO-000001', N'DEMO-LOT-EXPIRED', '2025-12-31', '11000000-0000-0000-0000-000000000103'),
        (N'DEMO-000002', N'DEMO-LOT-B01', '2027-11-30', '11000000-0000-0000-0000-000000000104'),
        (N'DEMO-000003', N'DEMO-LOT-C01', NULL, '11000000-0000-0000-0000-000000000105');

    INSERT INTO inventory.StockBatches
        (CreationOperationId, CreationFingerprint, ProductId, BatchNumber, ExpiryDate, IsActive, CreatedAtUtc)
    SELECT source.CreationOperationId,
           CONCAT(N'SEED|BATCH|', source.InternalCode, N'|', source.BatchNumber),
           product.ProductId, source.BatchNumber, source.ExpiryDate, 1, SYSUTCDATETIME()
    FROM @Batches AS source
    JOIN @Products AS product ON product.InternalCode = source.InternalCode
    WHERE NOT EXISTS
    (
        SELECT 1 FROM inventory.StockBatches AS target
        WHERE target.ProductId = product.ProductId AND target.BatchNumber = source.BatchNumber
    );

    DECLARE @SeedRows table
    (
        InternalCode nvarchar(128) NOT NULL,
        BatchNumber nvarchar(128) NULL,
        Quantity int NOT NULL,
        OperationId uniqueidentifier NOT NULL PRIMARY KEY
    );

    INSERT INTO @SeedRows (InternalCode, BatchNumber, Quantity, OperationId)
    VALUES
        (N'DEMO-000001', N'DEMO-LOT-A01', 5, '11000000-0000-0000-0000-000000001001'),
        (N'DEMO-000001', N'DEMO-LOT-A02', 7, '11000000-0000-0000-0000-000000001002'),
        (N'DEMO-000001', N'DEMO-LOT-EXPIRED', 2, '11000000-0000-0000-0000-000000001003'),
        (N'DEMO-000002', N'DEMO-LOT-B01', 9, '11000000-0000-0000-0000-000000001004'),
        (N'DEMO-000003', N'DEMO-LOT-C01', 6, '11000000-0000-0000-0000-000000001005'),
        (N'DEMO-000004', NULL, 8, '11000000-0000-0000-0000-000000001006'),
        (N'DEMO-000005', NULL, 10, '11000000-0000-0000-0000-000000001007'),
        (N'DEMO-000006', NULL, 4, '11000000-0000-0000-0000-000000001008'),
        (N'DEMO-000007', NULL, 3, '11000000-0000-0000-0000-000000001009'),
        (N'DEMO-000008', NULL, 11, '11000000-0000-0000-0000-000000001010'),
        (N'DEMO-000009', NULL, 2, '11000000-0000-0000-0000-000000001011'),
        (N'DEMO-000010', NULL, 12, '11000000-0000-0000-0000-000000001012');

    DECLARE @Resolved table
    (
        InternalCode nvarchar(128) NOT NULL,
        ProductId bigint NOT NULL,
        StockBatchId bigint NULL,
        Quantity int NOT NULL,
        OperationId uniqueidentifier NOT NULL PRIMARY KEY
    );

    INSERT INTO @Resolved (InternalCode, ProductId, StockBatchId, Quantity, OperationId)
    SELECT seed.InternalCode, product.ProductId, batch.Id, seed.Quantity, seed.OperationId
    FROM @SeedRows AS seed
    JOIN @Products AS product ON product.InternalCode = seed.InternalCode
    LEFT JOIN inventory.StockBatches AS batch
        ON batch.ProductId = product.ProductId AND batch.BatchNumber = seed.BatchNumber;

    IF (SELECT COUNT(*) FROM @Resolved) <> 12
        THROW 51114, 'Les douze lignes de stock Demo n''ont pas été résolues.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM @Resolved AS source
        WHERE (EXISTS (SELECT 1 FROM inventory.StockMovements AS movement WHERE movement.OperationId = source.OperationId)
               AND NOT EXISTS
                   (SELECT 1 FROM inventory.StockBalances AS balance
                    WHERE balance.StockLocationId = @LocationId AND balance.ProductId = source.ProductId
                      AND ((balance.StockBatchId = source.StockBatchId)
                           OR (balance.StockBatchId IS NULL AND source.StockBatchId IS NULL))))
           OR (NOT EXISTS (SELECT 1 FROM inventory.StockMovements AS movement WHERE movement.OperationId = source.OperationId)
               AND EXISTS
                   (SELECT 1 FROM inventory.StockBalances AS balance
                    WHERE balance.StockLocationId = @LocationId AND balance.ProductId = source.ProductId
                      AND ((balance.StockBatchId = source.StockBatchId)
                           OR (balance.StockBatchId IS NULL AND source.StockBatchId IS NULL))))
    )
        THROW 51115, 'État partiel détecté entre mouvements Opening et balances Demo.', 1;

    DECLARE @ToInsert table
    (
        InternalCode nvarchar(128) NOT NULL,
        ProductId bigint NOT NULL,
        StockBatchId bigint NULL,
        Quantity int NOT NULL,
        OperationId uniqueidentifier NOT NULL PRIMARY KEY
    );

    INSERT INTO @ToInsert
    SELECT source.*
    FROM @Resolved AS source
    WHERE NOT EXISTS (SELECT 1 FROM inventory.StockMovements AS movement WHERE movement.OperationId = source.OperationId);

    INSERT INTO inventory.StockMovements
        (OperationId, OperationFingerprint, ProductId, StockLocationId, StockBatchId, MovementType,
         Quantity, QuantityBefore, QuantityAfter, Reason, SourceType, SourceReference, OccurredAtUtc, CreatedAtUtc)
    SELECT OperationId, CONCAT(N'SEED|OPENING|', InternalCode, N'|', COALESCE(CONVERT(nvarchar(30), StockBatchId), N'-'), N'|', Quantity),
           ProductId, @LocationId, StockBatchId, N'Opening', Quantity, 0, Quantity,
           N'Stock d''ouverture entièrement synthétique', N'SyntheticSeed', N'DEMO-S11-SEED',
           SYSUTCDATETIME(), SYSUTCDATETIME()
    FROM @ToInsert;

    INSERT INTO inventory.StockBalances
        (StockLocationId, ProductId, StockBatchId, AvailableQuantity, UpdatedAtUtc)
    SELECT @LocationId, ProductId, StockBatchId, Quantity, SYSUTCDATETIME()
    FROM @ToInsert;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT N'Seed Inventory synthétique V0.1 appliqué sans duplication.' AS Result;
