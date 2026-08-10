using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesPersistenceV01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_StockMovements_Direction",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.EnsureSchema(
                name: "sales");

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleId = table.Column<long>(type: "bigint", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    TenderedAmount = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    ChangeGiven = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    ExternalCardConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_Payments", x => x.Id);
                    table.CheckConstraint("CK_sales_Payments_Amounts_NonNegative", "[Amount] >= 0 AND ([TenderedAmount] IS NULL OR [TenderedAmount] >= 0) AND [ChangeGiven] >= 0");
                    table.CheckConstraint("CK_sales_Payments_Card_Coherent", "([PaymentMethod] <> N'ExternalCard') OR ([ExternalCardConfirmed] = 1 AND [TenderedAmount] IS NULL AND [ChangeGiven] = 0)");
                    table.CheckConstraint("CK_sales_Payments_Cash_Coherent", "([PaymentMethod] <> N'Cash') OR ([TenderedAmount] IS NOT NULL AND [TenderedAmount] >= [Amount] AND [ChangeGiven] = [TenderedAmount] - [Amount] AND [ExternalCardConfirmed] = 0)");
                    table.CheckConstraint("CK_sales_Payments_PaymentMethod", "[PaymentMethod] IN (N'Cash', N'ExternalCard')");
                    table.CheckConstraint("CK_sales_Payments_Status_Completed", "[Status] = N'Completed'");
                });

            migrationBuilder.CreateTable(
                name: "SaleLineAllocations",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleLineId = table.Column<long>(type: "bigint", nullable: false),
                    StockBalanceId = table.Column<long>(type: "bigint", nullable: false),
                    StockBatchId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    BatchNumberSnapshot = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExpiryDateSnapshot = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_SaleLineAllocations", x => x.Id);
                    table.CheckConstraint("CK_sales_SaleLineAllocations_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_sales_SaleLineAllocations_StockBalances_StockBalanceId",
                        column: x => x.StockBalanceId,
                        principalSchema: "inventory",
                        principalTable: "StockBalances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_sales_SaleLineAllocations_StockBatches_StockBatchId",
                        column: x => x.StockBatchId,
                        principalSchema: "inventory",
                        principalTable: "StockBatches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SaleLines",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    ProductReferenceSnapshot = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductNameSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PresentationSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(19,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_SaleLines", x => x.Id);
                    table.CheckConstraint("CK_sales_SaleLines_Amounts_NonNegative", "[UnitPrice] >= 0 AND [LineTotal] >= 0");
                    table.CheckConstraint("CK_sales_SaleLines_Name_NotBlank", "LEN(LTRIM(RTRIM([ProductNameSnapshot]))) > 0");
                    table.CheckConstraint("CK_sales_SaleLines_Quantity_Positive", "[Quantity] > 0");
                    table.CheckConstraint("CK_sales_SaleLines_Reference_NotBlank", "LEN(LTRIM(RTRIM([ProductReferenceSnapshot]))) > 0");
                    table.CheckConstraint("CK_sales_SaleLines_Total_Coherent", "[LineTotal] = [UnitPrice] * [Quantity]");
                    table.ForeignKey(
                        name: "FK_sales_SaleLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SaleOperations",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ResultSaleId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_SaleOperations", x => x.Id);
                    table.UniqueConstraint("AK_sales_SaleOperations_OperationId", x => x.OperationId);
                    table.CheckConstraint("CK_sales_SaleOperations_Completion_Coherent", "([Status] = N'Processing' AND [ResultSaleId] IS NULL AND [CompletedAtUtc] IS NULL) OR ([Status] = N'Completed' AND [ResultSaleId] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_sales_SaleOperations_Fingerprint_NotBlank", "LEN(LTRIM(RTRIM([OperationFingerprint]))) > 0");
                    table.CheckConstraint("CK_sales_SaleOperations_Status", "[Status] IN (N'Processing', N'Completed')");
                });

            migrationBuilder.CreateTable(
                name: "Sales",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Reference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    PaidTotal = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    ChangeGiven = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    TotalQuantity = table.Column<long>(type: "bigint", nullable: false),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_Sales", x => x.Id);
                    table.CheckConstraint("CK_sales_Sales_Amounts_NonNegative", "[GrandTotal] >= 0 AND [PaidTotal] >= 0 AND [ChangeGiven] >= 0");
                    table.CheckConstraint("CK_sales_Sales_Counts_Positive", "[TotalQuantity] > 0 AND [LineCount] > 0");
                    table.CheckConstraint("CK_sales_Sales_PaymentMethod", "[PaymentMethod] IN (N'Cash', N'ExternalCard')");
                    table.CheckConstraint("CK_sales_Sales_Reference_NotBlank", "LEN(LTRIM(RTRIM([Reference]))) > 0");
                    table.CheckConstraint("CK_sales_Sales_Status_Completed", "[Status] = N'Completed'");
                    table.CheckConstraint("CK_sales_Sales_Totals_Coherent", "[PaidTotal] = [GrandTotal]");
                    table.ForeignKey(
                        name: "FK_sales_Sales_SaleOperations_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "sales",
                        principalTable: "SaleOperations",
                        principalColumn: "OperationId");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_StockMovements_Direction",
                schema: "inventory",
                table: "StockMovements",
                sql: "(([MovementType] IN (N'Opening', N'ManualIncrease', N'InventoryIncrease', N'PurchaseReceipt') AND [QuantityAfter] = [QuantityBefore] + [Quantity]) OR ([MovementType] IN (N'ManualDecrease', N'InventoryDecrease', N'Sale') AND [QuantityAfter] = [QuantityBefore] - [Quantity]))");

            migrationBuilder.CreateIndex(
                name: "IX_sales_Payments_SaleId",
                schema: "sales",
                table: "Payments",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "UX_sales_Payments_OperationId",
                schema: "sales",
                table: "Payments",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_SaleLineAllocations_StockBalanceId",
                schema: "sales",
                table: "SaleLineAllocations",
                column: "StockBalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_SaleLineAllocations_StockBatchId",
                schema: "sales",
                table: "SaleLineAllocations",
                column: "StockBatchId");

            migrationBuilder.CreateIndex(
                name: "UX_sales_SaleLineAllocations_Line_Balance",
                schema: "sales",
                table: "SaleLineAllocations",
                columns: new[] { "SaleLineId", "StockBalanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_SaleLines_ProductId",
                schema: "sales",
                table: "SaleLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_SaleLines_SaleId",
                schema: "sales",
                table: "SaleLines",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_SaleOperations_CreatedAtUtc",
                schema: "sales",
                table: "SaleOperations",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "UX_sales_SaleOperations_ResultSaleId",
                schema: "sales",
                table: "SaleOperations",
                column: "ResultSaleId",
                unique: true,
                filter: "[ResultSaleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_sales_Sales_CompletedAtUtc_Id",
                schema: "sales",
                table: "Sales",
                columns: new[] { "CompletedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_sales_Sales_OperationId",
                schema: "sales",
                table: "Sales",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_sales_Sales_Reference",
                schema: "sales",
                table: "Sales",
                column: "Reference",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_Payments_Sales_SaleId",
                schema: "sales",
                table: "Payments",
                column: "SaleId",
                principalSchema: "sales",
                principalTable: "Sales",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_sales_SaleLineAllocations_SaleLines_SaleLineId",
                schema: "sales",
                table: "SaleLineAllocations",
                column: "SaleLineId",
                principalSchema: "sales",
                principalTable: "SaleLines",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_sales_SaleLines_Sales_SaleId",
                schema: "sales",
                table: "SaleLines",
                column: "SaleId",
                principalSchema: "sales",
                principalTable: "Sales",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_sales_SaleOperations_Sales_ResultSaleId",
                schema: "sales",
                table: "SaleOperations",
                column: "ResultSaleId",
                principalSchema: "sales",
                principalTable: "Sales",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE balances
                SET balances.AvailableQuantity = balances.AvailableQuantity + sold.Quantity,
                    balances.UpdatedAtUtc = SYSUTCDATETIME()
                FROM inventory.StockBalances AS balances
                INNER JOIN
                (
                    SELECT StockLocationId, ProductId, StockBatchId, SUM(Quantity) AS Quantity
                    FROM inventory.StockMovements
                    WHERE MovementType = N'Sale'
                    GROUP BY StockLocationId, ProductId, StockBatchId
                ) AS sold
                    ON sold.StockLocationId = balances.StockLocationId
                    AND sold.ProductId = balances.ProductId
                    AND (sold.StockBatchId = balances.StockBatchId
                        OR (sold.StockBatchId IS NULL AND balances.StockBatchId IS NULL));

                DELETE FROM inventory.StockMovements WHERE MovementType = N'Sale';
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_sales_SaleOperations_Sales_ResultSaleId",
                schema: "sales",
                table: "SaleOperations");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SaleLineAllocations",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SaleLines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "Sales",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SaleOperations",
                schema: "sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_StockMovements_Direction",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_StockMovements_Direction",
                schema: "inventory",
                table: "StockMovements",
                sql: "(([MovementType] IN (N'Opening', N'ManualIncrease', N'InventoryIncrease', N'PurchaseReceipt') AND [QuantityAfter] = [QuantityBefore] + [Quantity]) OR ([MovementType] IN (N'ManualDecrease', N'InventoryDecrease') AND [QuantityAfter] = [QuantityBefore] - [Quantity]))");
        }
    }
}
