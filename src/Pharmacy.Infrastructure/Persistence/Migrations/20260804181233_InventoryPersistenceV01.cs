using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryPersistenceV01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "StockBatches",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreationOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreationFingerprint = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_StockBatches", x => x.Id);
                    table.CheckConstraint("CK_inventory_StockBatches_BatchNumber_NotBlank", "[BatchNumber] IS NULL OR LEN(LTRIM(RTRIM([BatchNumber]))) > 0");
                    table.CheckConstraint("CK_inventory_StockBatches_CreationFingerprint_NotBlank", "LEN(LTRIM(RTRIM([CreationFingerprint]))) > 0");
                    table.ForeignKey(
                        name: "FK_inventory_StockBatches_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StockLocations",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreationOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreationFingerprint = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    LastOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastOperationFingerprint = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_StockLocations", x => x.Id);
                    table.CheckConstraint("CK_inventory_StockLocations_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_inventory_StockLocations_CreationFingerprint_NotBlank", "LEN(LTRIM(RTRIM([CreationFingerprint]))) > 0");
                    table.CheckConstraint("CK_inventory_StockLocations_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "InventorySessions",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreationOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreationFingerprint = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    StartOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartOperationFingerprint = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    FinalizationOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinalizationFingerprint = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CancellationOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancellationFingerprint = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StockLocationId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_InventorySessions", x => x.Id);
                    table.CheckConstraint("CK_inventory_InventorySessions_CompletedAtUtc", "[CompletedAtUtc] IS NULL OR [CompletedAtUtc] >= [StartedAtUtc]");
                    table.CheckConstraint("CK_inventory_InventorySessions_CreationFingerprint_NotBlank", "LEN(LTRIM(RTRIM([CreationFingerprint]))) > 0");
                    table.CheckConstraint("CK_inventory_InventorySessions_Reference_NotBlank", "LEN(LTRIM(RTRIM([Reference]))) > 0");
                    table.ForeignKey(
                        name: "FK_inventory_InventorySessions_StockLocations_StockLocationId",
                        column: x => x.StockLocationId,
                        principalSchema: "inventory",
                        principalTable: "StockLocations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StockBalances",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockLocationId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    StockBatchId = table.Column<long>(type: "bigint", nullable: true),
                    AvailableQuantity = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_StockBalances", x => x.Id);
                    table.CheckConstraint("CK_inventory_StockBalances_AvailableQuantity_NonNegative", "[AvailableQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_inventory_StockBalances_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_inventory_StockBalances_StockBatches_StockBatchId",
                        column: x => x.StockBatchId,
                        principalSchema: "inventory",
                        principalTable: "StockBatches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_inventory_StockBalances_StockLocations_StockLocationId",
                        column: x => x.StockLocationId,
                        principalSchema: "inventory",
                        principalTable: "StockLocations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationFingerprint = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    StockLocationId = table.Column<long>(type: "bigint", nullable: false),
                    StockBatchId = table.Column<long>(type: "bigint", nullable: true),
                    MovementType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    QuantityBefore = table.Column<int>(type: "int", nullable: false),
                    QuantityAfter = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_StockMovements", x => x.Id);
                    table.CheckConstraint("CK_inventory_StockMovements_Balances_NonNegative", "[QuantityBefore] >= 0 AND [QuantityAfter] >= 0");
                    table.CheckConstraint("CK_inventory_StockMovements_Direction", "(([MovementType] IN (N'Opening', N'ManualIncrease', N'InventoryIncrease') AND [QuantityAfter] = [QuantityBefore] + [Quantity]) OR ([MovementType] IN (N'ManualDecrease', N'InventoryDecrease') AND [QuantityAfter] = [QuantityBefore] - [Quantity]))");
                    table.CheckConstraint("CK_inventory_StockMovements_Fingerprint_NotBlank", "LEN(LTRIM(RTRIM([OperationFingerprint]))) > 0");
                    table.CheckConstraint("CK_inventory_StockMovements_Quantity_Positive", "[Quantity] > 0");
                    table.CheckConstraint("CK_inventory_StockMovements_Reason_NotBlank", "LEN(LTRIM(RTRIM([Reason]))) > 0");
                    table.CheckConstraint("CK_inventory_StockMovements_SourceType_NotBlank", "LEN(LTRIM(RTRIM([SourceType]))) > 0");
                    table.ForeignKey(
                        name: "FK_inventory_StockMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_inventory_StockMovements_StockBatches_StockBatchId",
                        column: x => x.StockBatchId,
                        principalSchema: "inventory",
                        principalTable: "StockBatches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_inventory_StockMovements_StockLocations_StockLocationId",
                        column: x => x.StockLocationId,
                        principalSchema: "inventory",
                        principalTable: "StockLocations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InventoryCountLines",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventorySessionId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    StockBatchId = table.Column<long>(type: "bigint", nullable: true),
                    ExpectedQuantity = table.Column<int>(type: "int", nullable: false),
                    CountedQuantity = table.Column<int>(type: "int", nullable: true),
                    LastOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastOperationFingerprint = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_InventoryCountLines", x => x.Id);
                    table.CheckConstraint("CK_inventory_InventoryCountLines_CountedQuantity_NonNegative", "[CountedQuantity] IS NULL OR [CountedQuantity] >= 0");
                    table.CheckConstraint("CK_inventory_InventoryCountLines_ExpectedQuantity_NonNegative", "[ExpectedQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_inventory_InventoryCountLines_InventorySessions_InventorySessionId",
                        column: x => x.InventorySessionId,
                        principalSchema: "inventory",
                        principalTable: "InventorySessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_inventory_InventoryCountLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_inventory_InventoryCountLines_StockBatches_StockBatchId",
                        column: x => x.StockBatchId,
                        principalSchema: "inventory",
                        principalTable: "StockBatches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountLines_ProductId",
                schema: "inventory",
                table: "InventoryCountLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountLines_StockBatchId",
                schema: "inventory",
                table: "InventoryCountLines",
                column: "StockBatchId");

            migrationBuilder.CreateIndex(
                name: "UX_inventory_InventoryCountLines_LastOperationId",
                schema: "inventory",
                table: "InventoryCountLines",
                column: "LastOperationId",
                unique: true,
                filter: "[LastOperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_inventory_InventoryCountLines_Session_Product_Batch",
                schema: "inventory",
                table: "InventoryCountLines",
                columns: new[] { "InventorySessionId", "ProductId", "StockBatchId" },
                unique: true,
                filter: "[StockBatchId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_inventory_InventoryCountLines_Session_Product_NoBatch",
                schema: "inventory",
                table: "InventoryCountLines",
                columns: new[] { "InventorySessionId", "ProductId" },
                unique: true,
                filter: "[StockBatchId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_InventorySessions_LocationId_Status",
                schema: "inventory",
                table: "InventorySessions",
                columns: new[] { "StockLocationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_inventory_InventorySessions_CancellationOperationId",
                schema: "inventory",
                table: "InventorySessions",
                column: "CancellationOperationId",
                unique: true,
                filter: "[CancellationOperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_inventory_InventorySessions_CreationOperationId",
                schema: "inventory",
                table: "InventorySessions",
                column: "CreationOperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_inventory_InventorySessions_FinalizationOperationId",
                schema: "inventory",
                table: "InventorySessions",
                column: "FinalizationOperationId",
                unique: true,
                filter: "[FinalizationOperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_inventory_InventorySessions_Reference",
                schema: "inventory",
                table: "InventorySessions",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_inventory_InventorySessions_StartOperationId",
                schema: "inventory",
                table: "InventorySessions",
                column: "StartOperationId",
                unique: true,
                filter: "[StartOperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_StockBalances_ProductId_StockLocationId",
                schema: "inventory",
                table: "StockBalances",
                columns: new[] { "ProductId", "StockLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_StockBalances_StockBatchId",
                schema: "inventory",
                table: "StockBalances",
                column: "StockBatchId");

            migrationBuilder.CreateIndex(
                name: "UX_inventory_StockBalances_Location_Product_Batch",
                schema: "inventory",
                table: "StockBalances",
                columns: new[] { "StockLocationId", "ProductId", "StockBatchId" },
                unique: true,
                filter: "[StockBatchId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_inventory_StockBalances_Location_Product_NoBatch",
                schema: "inventory",
                table: "StockBalances",
                columns: new[] { "StockLocationId", "ProductId" },
                unique: true,
                filter: "[StockBatchId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_StockBatches_ExpiryDate",
                schema: "inventory",
                table: "StockBatches",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "UX_inventory_StockBatches_CreationOperationId",
                schema: "inventory",
                table: "StockBatches",
                column: "CreationOperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_inventory_StockBatches_ProductId_BatchNumber",
                schema: "inventory",
                table: "StockBatches",
                columns: new[] { "ProductId", "BatchNumber" },
                unique: true,
                filter: "[BatchNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_inventory_StockLocations_Code",
                schema: "inventory",
                table: "StockLocations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_inventory_StockLocations_CreationOperationId",
                schema: "inventory",
                table: "StockLocations",
                column: "CreationOperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_inventory_StockLocations_LastOperationId",
                schema: "inventory",
                table: "StockLocations",
                column: "LastOperationId",
                unique: true,
                filter: "[LastOperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_inventory_StockLocations_OnePrimary",
                schema: "inventory",
                table: "StockLocations",
                column: "IsPrimary",
                unique: true,
                filter: "[IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_StockMovements_LocationId_OccurredAtUtc",
                schema: "inventory",
                table: "StockMovements",
                columns: new[] { "StockLocationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_StockMovements_ProductId_OccurredAtUtc",
                schema: "inventory",
                table: "StockMovements",
                columns: new[] { "ProductId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_StockMovements_StockBatchId",
                schema: "inventory",
                table: "StockMovements",
                column: "StockBatchId");

            migrationBuilder.CreateIndex(
                name: "UX_inventory_StockMovements_OperationId",
                schema: "inventory",
                table: "StockMovements",
                column: "OperationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryCountLines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "StockBalances",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "StockMovements",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "InventorySessions",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "StockBatches",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "StockLocations",
                schema: "inventory");
        }
    }
}
