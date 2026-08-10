/*
Sprint 14 verification script.
Run on a migrated development database. It performs read-only checks.
*/

SET NOCOUNT ON;

SELECT s.name AS SchemaName, t.name AS TableName
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE s.name IN (N'customers', N'cash', N'sales')
  AND t.name IN (
      N'Customers',
      N'CustomerLedgerEntries',
      N'CashLedgerEntries',
      N'SuspendedSales',
      N'SuspendedSaleLines',
      N'SaleReturns',
      N'SaleReturnLines')
ORDER BY s.name, t.name;

SELECT
    c.Reference,
    c.FullName,
    c.IsActive,
    c.CreditAllowed,
    COALESCE(SUM(CASE
        WHEN e.EntryType IN (N'CreditSale', N'CreditAdjustmentIncrease') THEN e.Amount
        ELSE -e.Amount
    END), 0) AS CreditBalance
FROM customers.Customers AS c
LEFT JOIN customers.CustomerLedgerEntries AS e ON e.CustomerId = c.Id
GROUP BY c.Reference, c.FullName, c.IsActive, c.CreditAllowed
ORDER BY c.FullName;

SELECT TOP (50)
    EntryType,
    Amount,
    SourceType,
    SourceReference,
    OccurredAtUtc
FROM cash.CashLedgerEntries
ORDER BY OccurredAtUtc DESC, Id DESC;

SELECT TOP (50)
    Reference,
    Status,
    GrandTotal,
    LineCount,
    SuspendedAtUtc
FROM sales.SuspendedSales
ORDER BY SuspendedAtUtc DESC, Id DESC;

SELECT TOP (50)
    Reference,
    OriginalSaleId,
    RefundAmount,
    RefundMethod,
    CompletedAtUtc
FROM sales.SaleReturns
ORDER BY CompletedAtUtc DESC, Id DESC;
