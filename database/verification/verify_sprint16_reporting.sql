/* Sprint 16 read-only verification. Run only on a migrated development database. */
SET NOCOUNT ON;

SELECT s.name AS SchemaName, t.name AS ObjectName, N'TABLE' AS ObjectType
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE s.name = N'reporting'
UNION ALL
SELECT s.name, v.name, N'VIEW'
FROM sys.views AS v
JOIN sys.schemas AS s ON s.schema_id = v.schema_id
WHERE s.name = N'reporting'
ORDER BY ObjectType, ObjectName;

SELECT Id, LowStockAlertsEnabled, ExpiryAlertsEnabled, ExpiryHorizonDays, UpdatedAtUtc
FROM reporting.ReportSettings;

SELECT Code, IsSensitive
FROM security.Permissions
WHERE Code LIKE N'reports.%'
ORDER BY Code;

SELECT TOP (100) * FROM reporting.vw_SalesSummary ORDER BY CompletedAtUtc DESC, SaleId DESC;
SELECT TOP (100) * FROM reporting.vw_ProductSales ORDER BY CompletedAtUtc DESC, SaleLineId DESC;
SELECT TOP (100) * FROM reporting.vw_Payments ORDER BY ConfirmedAtUtc DESC, PaymentId DESC;
SELECT TOP (100) * FROM reporting.vw_StockSummary ORDER BY ProductName, ProductId;
SELECT TOP (100) * FROM reporting.vw_ExpiryAlerts ORDER BY ExpiryDate, BatchId;
SELECT TOP (100) * FROM reporting.vw_PurchaseOrders ORDER BY OrderDate DESC, PurchaseOrderId DESC;
SELECT TOP (100) * FROM reporting.vw_Receipts ORDER BY ReceivedAtUtc DESC, ReceiptId DESC;
SELECT TOP (100) * FROM reporting.vw_InventoryAdjustments ORDER BY OccurredAtUtc DESC, MovementId DESC;
SELECT TOP (100) * FROM reporting.vw_CustomerCredit ORDER BY CustomerName, CustomerId;
SELECT TOP (100) * FROM reporting.vw_ReturnsSummary ORDER BY CompletedAtUtc DESC, ReturnId DESC;
SELECT TOP (100) * FROM reporting.vw_CashMovements ORDER BY OccurredAtUtc DESC, CashEntryId DESC;

SELECT
    (SELECT COUNT(*) FROM reporting.vw_SalesSummary) AS SaleCount,
    (SELECT COALESCE(SUM(GrossAmount), 0) FROM reporting.vw_SalesSummary) AS GrossSales,
    (SELECT COALESCE(SUM(RefundAmount), 0) FROM reporting.vw_SalesSummary) AS Refunds,
    (SELECT COALESCE(SUM(NetAmount), 0) FROM reporting.vw_SalesSummary) AS DocumentedNetSales,
    (SELECT COALESCE(SUM(SellableQuantity), 0) FROM reporting.vw_StockSummary) AS CurrentSellableStock,
    (SELECT COUNT(*) FROM reporting.vw_StockSummary WHERE SellableQuantity <= 0) AS CurrentOutOfStockProducts;
