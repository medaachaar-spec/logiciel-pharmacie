using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchasingPersistenceV01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_StockMovements_Direction",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.EnsureSchema(
                name: "purchasing");

            migrationBuilder.CreateTable(
                name: "Suppliers",
                schema: "purchasing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchasing_Suppliers", x => x.Id);
                    table.CheckConstraint("CK_purchasing_Suppliers_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_purchasing_Suppliers_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                schema: "purchasing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SupplierId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchasing_PurchaseOrders", x => x.Id);
                    table.CheckConstraint("CK_purchasing_PurchaseOrders_Number_NotBlank", "LEN(LTRIM(RTRIM([OrderNumber]))) > 0");
                    table.ForeignKey(
                        name: "FK_purchasing_PurchaseOrders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "purchasing",
                        principalTable: "Suppliers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceipts",
                schema: "purchasing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationFingerprint = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    SupplierId = table.Column<long>(type: "bigint", nullable: false),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: true),
                    StockLocationId = table.Column<long>(type: "bigint", nullable: false),
                    ReceiptType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DeliveryNoteNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchasing_GoodsReceipts", x => x.Id);
                    table.CheckConstraint("CK_purchasing_GoodsReceipts_DeliveryNote_NotBlank", "LEN(LTRIM(RTRIM([DeliveryNoteNumber]))) > 0");
                    table.CheckConstraint("CK_purchasing_GoodsReceipts_Fingerprint_NotBlank", "LEN(LTRIM(RTRIM([OperationFingerprint]))) > 0");
                    table.ForeignKey(
                        name: "FK_purchasing_GoodsReceipts_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalSchema: "purchasing",
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_purchasing_GoodsReceipts_StockLocations_StockLocationId",
                        column: x => x.StockLocationId,
                        principalSchema: "inventory",
                        principalTable: "StockLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_purchasing_GoodsReceipts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "purchasing",
                        principalTable: "Suppliers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLines",
                schema: "purchasing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    ProductCodeSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProductNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OrderedQuantity = table.Column<int>(type: "int", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "int", nullable: false),
                    UnitPurchasePrice = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchasing_PurchaseOrderLines", x => x.Id);
                    table.CheckConstraint("CK_purchasing_PurchaseOrderLines_Price", "[UnitPurchasePrice] IS NULL OR [UnitPurchasePrice] >= 0");
                    table.CheckConstraint("CK_purchasing_PurchaseOrderLines_Quantities", "[OrderedQuantity] > 0 AND [ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [OrderedQuantity]");
                    table.ForeignKey(
                        name: "FK_purchasing_PurchaseOrderLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_purchasing_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalSchema: "purchasing",
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptLines",
                schema: "purchasing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoodsReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    PurchaseOrderLineId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPurchasePrice = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    ProductCodeSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProductNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StockBatchId = table.Column<long>(type: "bigint", nullable: true),
                    LotNumberSnapshot = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ExpiryDateSnapshot = table.Column<DateOnly>(type: "date", nullable: true),
                    StockMovementId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchasing_GoodsReceiptLines", x => x.Id);
                    table.CheckConstraint("CK_purchasing_GoodsReceiptLines_Price", "[UnitPurchasePrice] >= 0");
                    table.CheckConstraint("CK_purchasing_GoodsReceiptLines_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_purchasing_GoodsReceiptLines_GoodsReceipts_GoodsReceiptId",
                        column: x => x.GoodsReceiptId,
                        principalSchema: "purchasing",
                        principalTable: "GoodsReceipts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_purchasing_GoodsReceiptLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_purchasing_GoodsReceiptLines_PurchaseOrderLines_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalSchema: "purchasing",
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_purchasing_GoodsReceiptLines_StockBatches_StockBatchId",
                        column: x => x.StockBatchId,
                        principalSchema: "inventory",
                        principalTable: "StockBatches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_purchasing_GoodsReceiptLines_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalSchema: "inventory",
                        principalTable: "StockMovements",
                        principalColumn: "Id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_StockMovements_Direction",
                schema: "inventory",
                table: "StockMovements",
                sql: "(([MovementType] IN (N'Opening', N'ManualIncrease', N'InventoryIncrease', N'PurchaseReceipt') AND [QuantityAfter] = [QuantityBefore] + [Quantity]) OR ([MovementType] IN (N'ManualDecrease', N'InventoryDecrease') AND [QuantityAfter] = [QuantityBefore] - [Quantity]))");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptLines_ProductId",
                schema: "purchasing",
                table: "GoodsReceiptLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_purchasing_GoodsReceiptLines_PurchaseOrderLineId",
                schema: "purchasing",
                table: "GoodsReceiptLines",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_purchasing_GoodsReceiptLines_Receipt_Product",
                schema: "purchasing",
                table: "GoodsReceiptLines",
                columns: new[] { "GoodsReceiptId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchasing_GoodsReceiptLines_StockBatchId",
                schema: "purchasing",
                table: "GoodsReceiptLines",
                column: "StockBatchId");

            migrationBuilder.CreateIndex(
                name: "UX_purchasing_GoodsReceiptLines_StockMovementId",
                schema: "purchasing",
                table: "GoodsReceiptLines",
                column: "StockMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchasing_GoodsReceipts_PurchaseOrderId",
                schema: "purchasing",
                table: "GoodsReceipts",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_purchasing_GoodsReceipts_StockLocationId",
                schema: "purchasing",
                table: "GoodsReceipts",
                column: "StockLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_purchasing_GoodsReceipts_SupplierId_ReceivedAtUtc",
                schema: "purchasing",
                table: "GoodsReceipts",
                columns: new[] { "SupplierId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_purchasing_GoodsReceipts_OperationId",
                schema: "purchasing",
                table: "GoodsReceipts",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_purchasing_GoodsReceipts_ReceiptNumber",
                schema: "purchasing",
                table: "GoodsReceipts",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_purchasing_GoodsReceipts_Supplier_DeliveryNote",
                schema: "purchasing",
                table: "GoodsReceipts",
                columns: new[] { "SupplierId", "DeliveryNoteNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchasing_PurchaseOrderLines_ProductId",
                schema: "purchasing",
                table: "PurchaseOrderLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "UX_purchasing_PurchaseOrderLines_Order_Product",
                schema: "purchasing",
                table: "PurchaseOrderLines",
                columns: new[] { "PurchaseOrderId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchasing_PurchaseOrders_Status_OrderDate",
                schema: "purchasing",
                table: "PurchaseOrders",
                columns: new[] { "Status", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_purchasing_PurchaseOrders_SupplierId_OrderDate",
                schema: "purchasing",
                table: "PurchaseOrders",
                columns: new[] { "SupplierId", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "UX_purchasing_PurchaseOrders_OrderNumber",
                schema: "purchasing",
                table: "PurchaseOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchasing_Suppliers_IsActive_Name",
                schema: "purchasing",
                table: "Suppliers",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "UX_purchasing_Suppliers_Code",
                schema: "purchasing",
                table: "Suppliers",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoodsReceiptLines",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "GoodsReceipts",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "PurchaseOrderLines",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "PurchaseOrders",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "Suppliers",
                schema: "purchasing");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_StockMovements_Direction",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_StockMovements_Direction",
                schema: "inventory",
                table: "StockMovements",
                sql: "(([MovementType] IN (N'Opening', N'ManualIncrease', N'InventoryIncrease') AND [QuantityAfter] = [QuantityBefore] + [Quantity]) OR ([MovementType] IN (N'ManualDecrease', N'InventoryDecrease') AND [QuantityAfter] = [QuantityBefore] - [Quantity]))");
        }
    }
}
