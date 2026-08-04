SET NOCOUNT ON;

IF DB_NAME() <> N'Pharmacy_Development'
    THROW 51130, 'La vérification Sprint 11 doit cibler Pharmacy_Development.', 1;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId LIKE N'%_InventoryPersistenceV01'
)
    THROW 51131, 'La migration InventoryPersistenceV01 n''est pas appliquée.', 1;

IF (SELECT COUNT(*) FROM sys.tables AS tableInfo
    JOIN sys.schemas AS schemaInfo ON schemaInfo.schema_id = tableInfo.schema_id
    WHERE schemaInfo.name = N'inventory') <> 6
    THROW 51132, 'Le schéma inventory doit contenir exactement six tables.', 1;

IF NOT EXISTS (SELECT 1 FROM inventory.StockLocations WHERE Code = N'DEMO-LOCATION-MAIN')
    THROW 51133, 'L''emplacement Demo est absent.', 1;

IF (SELECT COUNT(*) FROM inventory.StockBatches
    WHERE BatchNumber IN (N'DEMO-LOT-A01', N'DEMO-LOT-A02', N'DEMO-LOT-EXPIRED', N'DEMO-LOT-B01', N'DEMO-LOT-C01')) <> 5
    THROW 51134, 'Les cinq lots Demo sont absents ou dupliqués.', 1;

IF (SELECT COUNT(*) FROM inventory.StockMovements
    WHERE SourceType = N'SyntheticSeed' AND SourceReference = N'DEMO-S11-SEED') <> 12
    THROW 51135, 'Les douze mouvements Opening Demo sont absents ou dupliqués.', 1;

IF (SELECT COUNT(*) FROM inventory.StockBalances AS balance
    JOIN inventory.StockLocations AS location ON location.Id = balance.StockLocationId
    JOIN catalog.ProductLocalSettings AS settings ON settings.ProductId = balance.ProductId
    WHERE location.Code = N'DEMO-LOCATION-MAIN'
      AND settings.InternalCode IN
      (N'DEMO-000001', N'DEMO-000002', N'DEMO-000003', N'DEMO-000004', N'DEMO-000005',
       N'DEMO-000006', N'DEMO-000007', N'DEMO-000008', N'DEMO-000009', N'DEMO-000010')) <> 12
    THROW 51136, 'Les douze balances Demo sont absentes ou dupliquées.', 1;

IF EXISTS (SELECT 1 FROM inventory.StockBalances WHERE AvailableQuantity < 0)
    THROW 51137, 'Une balance négative existe.', 1;

IF EXISTS (SELECT 1 FROM inventory.StockMovements WHERE Quantity <= 0 OR QuantityBefore < 0 OR QuantityAfter < 0)
    THROW 51138, 'Un mouvement viole les contraintes de quantité.', 1;

IF EXISTS
(
    SELECT balance.StockLocationId, balance.ProductId, balance.StockBatchId
    FROM inventory.StockBalances AS balance
    OUTER APPLY
    (
        SELECT SUM(CASE
            WHEN movement.MovementType IN (N'Opening', N'ManualIncrease', N'InventoryIncrease') THEN movement.Quantity
            ELSE -movement.Quantity END) AS ReconciledQuantity
        FROM inventory.StockMovements AS movement
        WHERE movement.StockLocationId = balance.StockLocationId
          AND movement.ProductId = balance.ProductId
          AND ((movement.StockBatchId = balance.StockBatchId)
               OR (movement.StockBatchId IS NULL AND balance.StockBatchId IS NULL))
    ) AS ledger
    WHERE balance.AvailableQuantity <> COALESCE(ledger.ReconciledQuantity, 0)
)
    THROW 51139, 'Une balance ne correspond pas à son journal de mouvements.', 1;

IF NOT EXISTS
(
    SELECT 1 FROM inventory.StockBatches
    WHERE BatchNumber = N'DEMO-LOT-EXPIRED' AND ExpiryDate < CONVERT(date, GETDATE())
)
    THROW 51140, 'Le lot expiré Demo attendu est absent.', 1;

IF EXISTS
(
    SELECT 1
    FROM inventory.StockBalances AS balance
    JOIN inventory.StockBatches AS batch ON batch.Id = balance.StockBatchId
    WHERE batch.ExpiryDate < CONVERT(date, GETDATE())
      AND batch.BatchNumber = N'DEMO-LOT-EXPIRED'
      AND balance.AvailableQuantity <= 0
)
    THROW 51141, 'Le lot expiré Demo doit rester visible avec sa quantité.', 1;

SELECT
    DB_NAME() AS DatabaseName,
    databaseInfo.compatibility_level AS CompatibilityLevel,
    CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')) AS ProductVersion,
    CONVERT(nvarchar(128), SERVERPROPERTY('Edition')) AS Edition
FROM sys.databases AS databaseInfo
WHERE databaseInfo.name = DB_NAME();

SELECT MigrationId, ProductVersion
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;

SELECT
    (SELECT COUNT(*) FROM inventory.StockLocations) AS Locations,
    (SELECT COUNT(*) FROM inventory.StockBatches) AS Batches,
    (SELECT COUNT(*) FROM inventory.StockBalances WHERE AvailableQuantity > 0) AS PositiveBalances,
    (SELECT COUNT(*) FROM inventory.StockMovements) AS Movements,
    (SELECT COUNT(*) FROM inventory.InventorySessions) AS InventorySessions,
    (SELECT COUNT(*) FROM inventory.StockBatches WHERE ExpiryDate < CONVERT(date, GETDATE())) AS ExpiredBatches;

SELECT
    settings.InternalCode,
    product.Name,
    location.Code AS LocationCode,
    batch.BatchNumber,
    batch.ExpiryDate,
    balance.AvailableQuantity,
    CASE WHEN batch.ExpiryDate < CONVERT(date, GETDATE()) THEN N'Expiré — exclu de la disponibilité POS' ELSE N'Vendable selon états' END AS AvailabilityState,
    sys.fn_varbintohexstr(balance.RowVersion) AS BalanceRowVersion
FROM inventory.StockBalances AS balance
JOIN catalog.Products AS product ON product.Id = balance.ProductId
JOIN catalog.ProductLocalSettings AS settings ON settings.ProductId = product.Id
JOIN inventory.StockLocations AS location ON location.Id = balance.StockLocationId
LEFT JOIN inventory.StockBatches AS batch ON batch.Id = balance.StockBatchId
WHERE location.Code = N'DEMO-LOCATION-MAIN'
ORDER BY settings.InternalCode, batch.ExpiryDate, batch.BatchNumber;
