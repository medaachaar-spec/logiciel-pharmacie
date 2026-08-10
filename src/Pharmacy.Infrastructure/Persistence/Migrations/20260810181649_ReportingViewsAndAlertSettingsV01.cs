using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportingViewsAndAlertSettingsV01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reporting");

            migrationBuilder.CreateTable(
                name: "ReportSettings",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    LowStockAlertsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ExpiryAlertsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ExpiryHorizonDays = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reporting_ReportSettings", x => x.Id);
                    table.CheckConstraint("CK_reporting_ReportSettings_ExpiryHorizon", "[ExpiryHorizonDays] IS NULL OR [ExpiryHorizonDays] >= 0");
                    table.CheckConstraint("CK_reporting_ReportSettings_Singleton", "[Id] = 1");
                });

            migrationBuilder.InsertData(
                schema: "security",
                table: "Permissions",
                columns: new[] { "Id", "Code", "Description", "IsSensitive", "Module", "Name" },
                values: new object[,]
                {
                    { 21L, "reports.export", "Permission explicite reports.export.", false, "reports", "reports.export" },
                    { 22L, "reports.manage_settings", "Permission explicite reports.manage_settings.", true, "reports", "reports.manage_settings" }
                });

            migrationBuilder.InsertData(
                schema: "reporting",
                table: "ReportSettings",
                columns: new[] { "Id", "ExpiryAlertsEnabled", "ExpiryHorizonDays", "LowStockAlertsEnabled", "UpdatedAtUtc" },
                values: new object[] { 1L, false, null, false, new DateTime(2026, 8, 10, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                schema: "security",
                table: "RolePermissions",
                columns: new[] { "Id", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 21L, 21L, 1L },
                    { 22L, 22L, 1L }
                });

            migrationBuilder.Sql("""
                CREATE VIEW reporting.vw_SalesSummary AS
                SELECT s.Id AS SaleId, s.Reference, s.CompletedAtUtc, s.Status, s.PaymentMethod,
                       s.LineCount, s.TotalQuantity, s.GrandTotal AS GrossAmount,
                       s.PaidTotal AS PaidAmount, s.CreditTotal AS CreditAmount,
                       COALESCE(r.RefundAmount, 0) AS RefundAmount,
                       s.GrandTotal - COALESCE(r.RefundAmount, 0) AS NetAmount
                FROM sales.Sales AS s
                LEFT JOIN (
                    SELECT OriginalSaleId, SUM(RefundAmount) AS RefundAmount
                    FROM sales.SaleReturns WHERE Status = N'Completed' GROUP BY OriginalSaleId
                ) AS r ON r.OriginalSaleId = s.Id;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW reporting.vw_ProductSales AS
                SELECT sl.Id AS SaleLineId, sl.SaleId, s.Reference AS SaleReference,
                       s.CompletedAtUtc, sl.ProductId, sl.ProductReferenceSnapshot,
                       sl.ProductNameSnapshot, sl.Quantity AS SoldQuantity,
                       COALESCE(r.ReturnedQuantity, 0) AS ReturnedQuantity,
                       sl.LineTotal AS GrossAmount, COALESCE(r.RefundAmount, 0) AS RefundAmount,
                       sl.LineTotal - COALESCE(r.RefundAmount, 0) AS NetAmount
                FROM sales.SaleLines AS sl
                INNER JOIN sales.Sales AS s ON s.Id = sl.SaleId
                LEFT JOIN (
                    SELECT rl.SaleLineId, SUM(rl.Quantity) AS ReturnedQuantity, SUM(rl.LineTotal) AS RefundAmount
                    FROM sales.SaleReturnLines AS rl
                    INNER JOIN sales.SaleReturns AS sr ON sr.Id = rl.SaleReturnId AND sr.Status = N'Completed'
                    GROUP BY rl.SaleLineId
                ) AS r ON r.SaleLineId = sl.Id;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW reporting.vw_Payments AS
                SELECT p.Id AS PaymentId, p.SaleId, s.Reference AS SaleReference,
                       p.ConfirmedAtUtc, p.PaymentMethod, p.Status, p.Amount,
                       p.TenderedAmount, p.ChangeGiven, p.ExternalCardConfirmed
                FROM sales.Payments AS p
                INNER JOIN sales.Sales AS s ON s.Id = p.SaleId;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW reporting.vw_StockSummary AS
                SELECT p.Id AS ProductId, COALESCE(pls.InternalCode, N'') AS ProductReference,
                       p.Name AS ProductName, pls.MinimumStock,
                       COALESCE(SUM(CASE WHEN sb.AvailableQuantity > 0
                            AND (sb.StockBatchId IS NULL OR (b.IsActive = 1 AND (b.ExpiryDate IS NULL OR b.ExpiryDate >= CONVERT(date, SYSUTCDATETIME()))))
                            THEN CONVERT(bigint, sb.AvailableQuantity) ELSE CONVERT(bigint, 0) END), 0) AS SellableQuantity
                FROM catalog.Products AS p
                INNER JOIN catalog.ProductLocalSettings AS pls ON pls.ProductId = p.Id
                LEFT JOIN inventory.StockBalances AS sb ON sb.ProductId = p.Id
                LEFT JOIN inventory.StockBatches AS b ON b.Id = sb.StockBatchId
                WHERE p.IsActive = 1 AND pls.IsActive = 1 AND pls.IsSellable = 1
                GROUP BY p.Id, pls.InternalCode, p.Name, pls.MinimumStock;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW reporting.vw_ExpiryAlerts AS
                SELECT b.Id AS BatchId, b.ProductId, COALESCE(pls.InternalCode, N'') AS ProductReference,
                       p.Name AS ProductName, b.BatchNumber, b.ExpiryDate,
                       COALESCE(SUM(CONVERT(bigint, sb.AvailableQuantity)), 0) AS Quantity,
                       DATEDIFF(day, CONVERT(date, SYSUTCDATETIME()), b.ExpiryDate) AS DaysRemaining
                FROM inventory.StockBatches AS b
                INNER JOIN catalog.Products AS p ON p.Id = b.ProductId
                LEFT JOIN catalog.ProductLocalSettings AS pls ON pls.ProductId = p.Id
                LEFT JOIN inventory.StockBalances AS sb ON sb.StockBatchId = b.Id
                WHERE b.IsActive = 1 AND b.ExpiryDate IS NOT NULL
                GROUP BY b.Id, b.ProductId, pls.InternalCode, p.Name, b.BatchNumber, b.ExpiryDate
                HAVING COALESCE(SUM(CONVERT(bigint, sb.AvailableQuantity)), 0) > 0;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW reporting.vw_PurchaseOrders AS
                SELECT po.Id AS PurchaseOrderId, po.OrderNumber, po.OrderDate, po.ExpectedDate,
                       po.Status, sup.Code AS SupplierCode, sup.Name AS SupplierName,
                       COALESCE(SUM(CONVERT(bigint, pol.OrderedQuantity)), 0) AS OrderedQuantity,
                       COALESCE(SUM(CONVERT(bigint, pol.ReceivedQuantity)), 0) AS ReceivedQuantity,
                       COALESCE(SUM(CONVERT(decimal(19,4), pol.OrderedQuantity) * pol.UnitPurchasePrice), 0) AS HistoricalOrderedAmount
                FROM purchasing.PurchaseOrders AS po
                INNER JOIN purchasing.Suppliers AS sup ON sup.Id = po.SupplierId
                LEFT JOIN purchasing.PurchaseOrderLines AS pol ON pol.PurchaseOrderId = po.Id
                GROUP BY po.Id, po.OrderNumber, po.OrderDate, po.ExpectedDate, po.Status, sup.Code, sup.Name;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW reporting.vw_Receipts AS
                SELECT gr.Id AS ReceiptId, gr.ReceiptNumber, gr.ReceivedAtUtc, gr.ReceiptType,
                       gr.Status, sup.Code AS SupplierCode, sup.Name AS SupplierName,
                       COALESCE(SUM(CONVERT(bigint, grl.Quantity)), 0) AS ReceivedQuantity,
                       COALESCE(SUM(CONVERT(decimal(19,4), grl.Quantity) * grl.UnitPurchasePrice), 0) AS HistoricalReceivedAmount
                FROM purchasing.GoodsReceipts AS gr
                INNER JOIN purchasing.Suppliers AS sup ON sup.Id = gr.SupplierId
                LEFT JOIN purchasing.GoodsReceiptLines AS grl ON grl.GoodsReceiptId = gr.Id
                GROUP BY gr.Id, gr.ReceiptNumber, gr.ReceivedAtUtc, gr.ReceiptType, gr.Status, sup.Code, sup.Name;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW reporting.vw_InventoryAdjustments AS
                SELECT sm.Id AS MovementId, sm.OccurredAtUtc, sm.ProductId,
                       COALESCE(pls.InternalCode, N'') AS ProductReference, p.Name AS ProductName,
                       sm.MovementType, sm.Quantity, sm.QuantityBefore, sm.QuantityAfter,
                       sm.Reason, sm.SourceType, sm.SourceReference
                FROM inventory.StockMovements AS sm
                INNER JOIN catalog.Products AS p ON p.Id = sm.ProductId
                LEFT JOIN catalog.ProductLocalSettings AS pls ON pls.ProductId = p.Id
                WHERE sm.MovementType IN (N'ManualIncrease', N'ManualDecrease', N'InventoryIncrease', N'InventoryDecrease');
                """);

            migrationBuilder.Sql("""
                CREATE VIEW reporting.vw_CustomerCredit AS
                SELECT c.Id AS CustomerId, c.Reference AS CustomerReference, c.FullName AS CustomerName,
                       c.CreditAllowed,
                       COALESCE(SUM(CASE WHEN cle.EntryType IN (N'CreditSale', N'CreditAdjustmentIncrease') THEN cle.Amount ELSE 0 END), 0) AS CreditGenerated,
                       COALESCE(SUM(CASE WHEN cle.EntryType IN (N'CreditPayment', N'CreditAdjustmentDecrease', N'RefundImpact') THEN cle.Amount ELSE 0 END), 0) AS CreditPaid,
                       COALESCE(SUM(CASE WHEN cle.EntryType IN (N'CreditSale', N'CreditAdjustmentIncrease') THEN cle.Amount ELSE -cle.Amount END), 0) AS OpenBalance
                FROM customers.Customers AS c
                LEFT JOIN customers.CustomerLedgerEntries AS cle ON cle.CustomerId = c.Id
                GROUP BY c.Id, c.Reference, c.FullName, c.CreditAllowed;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW reporting.vw_ReturnsSummary AS
                SELECT sr.Id AS ReturnId, sr.Reference AS ReturnReference, sr.CompletedAtUtc,
                       s.Reference AS SaleReference, sr.Status, sr.RefundMethod, sr.RefundAmount,
                       COALESCE(SUM(srl.Quantity), 0) AS ReturnedQuantity,
                       COALESCE(SUM(CASE WHEN srl.StockDestination = N'RestockApproved' THEN srl.Quantity ELSE 0 END), 0) AS RestockedQuantity
                FROM sales.SaleReturns AS sr
                INNER JOIN sales.Sales AS s ON s.Id = sr.OriginalSaleId
                LEFT JOIN sales.SaleReturnLines AS srl ON srl.SaleReturnId = sr.Id
                GROUP BY sr.Id, sr.Reference, sr.CompletedAtUtc, s.Reference, sr.Status, sr.RefundMethod, sr.RefundAmount;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW reporting.vw_CashMovements AS
                SELECT Id AS CashEntryId, OccurredAtUtc, EntryType, Amount, SourceType, SourceReference
                FROM cash.CashLedgerEntries;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS reporting.vw_CashMovements;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS reporting.vw_ReturnsSummary;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS reporting.vw_CustomerCredit;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS reporting.vw_InventoryAdjustments;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS reporting.vw_Receipts;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS reporting.vw_PurchaseOrders;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS reporting.vw_ExpiryAlerts;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS reporting.vw_StockSummary;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS reporting.vw_Payments;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS reporting.vw_ProductSales;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS reporting.vw_SalesSummary;");

            migrationBuilder.DropTable(
                name: "ReportSettings",
                schema: "reporting");

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 21L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 22L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 21L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 22L);
        }
    }
}
