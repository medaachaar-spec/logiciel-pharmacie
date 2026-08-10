SET NOCOUNT ON;

IF DB_NAME() <> N'Pharmacy_Development'
    THROW 51330, 'La vérification Sprint 13 cible uniquement Pharmacy_Development.', 1;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId LIKE N'%_SalesPersistenceV01'
)
    THROW 51331, 'La migration SalesPersistenceV01 n''est pas appliquée.', 1;

IF (SELECT COUNT(*) FROM sys.tables AS tableInfo
    JOIN sys.schemas AS schemaInfo ON schemaInfo.schema_id = tableInfo.schema_id
    WHERE schemaInfo.name = N'sales') <> 5
    THROW 51332, 'Le schéma sales doit contenir exactement cinq tables.', 1;

IF EXISTS
(
    SELECT 1
    FROM sys.columns AS columnInfo
    JOIN sys.tables AS tableInfo ON tableInfo.object_id = columnInfo.object_id
    JOIN sys.schemas AS schemaInfo ON schemaInfo.schema_id = tableInfo.schema_id
    WHERE schemaInfo.name = N'sales'
      AND columnInfo.name = N'Id'
      AND TYPE_NAME(columnInfo.user_type_id) <> N'bigint'
)
    THROW 51333, 'Toutes les clés Id Sales doivent être bigint.', 1;

IF EXISTS
(
    SELECT 1
    FROM sys.foreign_keys AS foreignKey
    JOIN sys.tables AS tableInfo ON tableInfo.object_id = foreignKey.parent_object_id
    JOIN sys.schemas AS schemaInfo ON schemaInfo.schema_id = tableInfo.schema_id
    WHERE schemaInfo.name = N'sales' AND foreignKey.delete_referential_action <> 0
)
    THROW 51334, 'Toutes les suppressions référentielles Sales doivent être NO ACTION.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'sales.Sales') AND name=N'UX_sales_Sales_OperationId' AND is_unique=1)
    OR NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'sales.Sales') AND name=N'UX_sales_Sales_Reference' AND is_unique=1)
    OR NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'sales.Payments') AND name=N'UX_sales_Payments_OperationId' AND is_unique=1)
    THROW 51335, 'Un index unique Sales attendu est absent.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'sales.SaleOperations') AND name=N'AK_sales_SaleOperations_OperationId' AND type=N'UQ')
    THROW 51336, 'L''unicité métier de SaleOperations.OperationId est absente.', 1;

IF EXISTS (SELECT 1 FROM sales.SaleLines WHERE Quantity <= 0 OR UnitPrice < 0 OR LineTotal < 0 OR LineTotal <> UnitPrice * Quantity)
    OR EXISTS (SELECT 1 FROM sales.SaleLineAllocations WHERE Quantity <= 0)
    OR EXISTS (SELECT 1 FROM sales.Payments WHERE Amount < 0 OR ChangeGiven < 0 OR (TenderedAmount IS NOT NULL AND TenderedAmount < 0))
    THROW 51337, 'Une quantité, un montant ou un total Sales viole les contraintes métier.', 1;

IF EXISTS
(
    SELECT sale.Id
    FROM sales.Sales AS sale
    OUTER APPLY (SELECT SUM(LineTotal) AS LinesTotal, SUM(Quantity) AS QuantityTotal, COUNT(*) AS LineCount FROM sales.SaleLines WHERE SaleId=sale.Id) AS lines
    OUTER APPLY (SELECT SUM(Amount) AS PaymentsTotal, COUNT(*) AS PaymentCount FROM sales.Payments WHERE SaleId=sale.Id) AS payments
    WHERE sale.GrandTotal <> COALESCE(lines.LinesTotal, -1)
       OR sale.TotalQuantity <> COALESCE(lines.QuantityTotal, -1)
       OR sale.LineCount <> COALESCE(lines.LineCount, -1)
       OR sale.PaidTotal <> COALESCE(payments.PaymentsTotal, -1)
       OR payments.PaymentCount <> 1
)
    THROW 51338, 'Une vente persistée n''est pas cohérente avec ses lignes ou son paiement.', 1;

IF EXISTS
(
    SELECT 1
    FROM sales.SaleOperations AS operation
    LEFT JOIN sales.Sales AS sale ON sale.Id = operation.ResultSaleId
    WHERE operation.Status <> N'Completed'
       OR operation.ResultSaleId IS NULL
       OR operation.CompletedAtUtc IS NULL
       OR sale.OperationId <> operation.OperationId
)
    THROW 51339, 'Une opération Sales n''est pas complètement finalisée.', 1;

IF EXISTS
(
    SELECT 1
    FROM sales.SaleLineAllocations AS allocation
    JOIN sales.SaleLines AS line ON line.Id = allocation.SaleLineId
    JOIN sales.Sales AS sale ON sale.Id = line.SaleId
    JOIN inventory.StockBalances AS balance ON balance.Id = allocation.StockBalanceId
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM inventory.StockMovements AS movement
        WHERE movement.MovementType = N'Sale'
          AND movement.SourceType = N'Sale'
          AND movement.SourceReference = sale.Reference
          AND movement.ProductId = line.ProductId
          AND movement.StockLocationId = balance.StockLocationId
          AND (movement.StockBatchId = balance.StockBatchId
               OR (movement.StockBatchId IS NULL AND balance.StockBatchId IS NULL))
          AND movement.Quantity = allocation.Quantity
    )
)
    THROW 51340, 'Une allocation Sales ne possède pas son mouvement de sortie correspondant.', 1;

IF EXISTS
(
    SELECT 1
    FROM inventory.StockBalances AS balance
    OUTER APPLY
    (
        SELECT SUM(CASE
            WHEN movement.MovementType IN (N'Opening', N'ManualIncrease', N'InventoryIncrease', N'PurchaseReceipt') THEN movement.Quantity
            ELSE -movement.Quantity END) AS ReconciledQuantity
        FROM inventory.StockMovements AS movement
        WHERE movement.StockLocationId = balance.StockLocationId
          AND movement.ProductId = balance.ProductId
          AND (movement.StockBatchId = balance.StockBatchId
               OR (movement.StockBatchId IS NULL AND balance.StockBatchId IS NULL))
    ) AS ledger
    WHERE balance.AvailableQuantity <> COALESCE(ledger.ReconciledQuantity, 0)
)
    THROW 51341, 'Une balance ne correspond pas au journal incluant les ventes.', 1;

IF EXISTS
(
    SELECT 1
    FROM sys.columns AS columnInfo
    JOIN sys.tables AS tableInfo ON tableInfo.object_id=columnInfo.object_id
    JOIN sys.schemas AS schemaInfo ON schemaInfo.schema_id=tableInfo.schema_id
    WHERE schemaInfo.name=N'sales'
      AND (columnInfo.name LIKE N'%CardNumber%'
        OR columnInfo.name LIKE N'%PAN%'
        OR columnInfo.name LIKE N'%CVV%'
        OR columnInfo.name LIKE N'%CVC%')
)
    THROW 51342, 'Une colonne de donnée bancaire sensible est interdite.', 1;

SELECT
    N'PASS' AS Sprint13Sales,
    (SELECT COUNT(*) FROM sales.Sales) AS Sales,
    (SELECT COUNT(*) FROM sales.SaleLines) AS SaleLines,
    (SELECT COUNT(*) FROM sales.SaleLineAllocations) AS Allocations,
    (SELECT COUNT(*) FROM sales.Payments) AS Payments,
    (SELECT COUNT(*) FROM inventory.StockMovements WHERE MovementType=N'Sale') AS SaleMovements;

SELECT TOP (20)
    sale.Id,
    sale.Reference,
    sale.CompletedAtUtc,
    sale.GrandTotal,
    sale.TotalQuantity,
    sale.PaymentMethod
FROM sales.Sales AS sale
ORDER BY sale.CompletedAtUtc DESC, sale.Id DESC;
