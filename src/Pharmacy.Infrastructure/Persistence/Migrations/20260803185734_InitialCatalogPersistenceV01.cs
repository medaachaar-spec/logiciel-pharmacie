using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalogPersistenceV01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "ProductCategories",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DefaultTracksLots = table.Column<bool>(type: "bit", nullable: false),
                    DefaultTracksExpiry = table.Column<bool>(type: "bit", nullable: false),
                    DefaultDiscountAllowed = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_ProductCategories", x => x.Id);
                    table.CheckConstraint("CK_catalog_ProductCategories_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_catalog_ProductCategories_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_catalog_ProductCategories_SortOrder_NonNegative", "[SortOrder] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    GenericName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Dosage = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Form = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PackageDescription = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    BrandName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_Products", x => x.Id);
                    table.CheckConstraint("CK_catalog_Products_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_catalog_Products_UpdatedAfterCreated", "[UpdatedAtUtc] >= [CreatedAtUtc]");
                });

            migrationBuilder.CreateTable(
                name: "ProductBarcodes",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NormalizedValue = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    BarcodeType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_ProductBarcodes", x => x.Id);
                    table.CheckConstraint("CK_catalog_ProductBarcodes_NormalizedValue_NotBlank", "LEN(LTRIM(RTRIM([NormalizedValue]))) > 0");
                    table.CheckConstraint("CK_catalog_ProductBarcodes_Value_NotBlank", "LEN(LTRIM(RTRIM([Value]))) > 0");
                    table.ForeignKey(
                        name: "FK_catalog_ProductBarcodes_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductLocalSettings",
                schema: "catalog",
                columns: table => new
                {
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    InternalCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CategoryId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSellable = table.Column<bool>(type: "bit", nullable: false),
                    IsPurchasable = table.Column<bool>(type: "bit", nullable: false),
                    TracksLots = table.Column<bool>(type: "bit", nullable: false),
                    TracksExpiry = table.Column<bool>(type: "bit", nullable: false),
                    DiscountAllowed = table.Column<bool>(type: "bit", nullable: false),
                    QuantityScale = table.Column<byte>(type: "tinyint", nullable: false),
                    StorageLocation = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    MinimumStock = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    MaximumStock = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    CurrentSalePrice = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_ProductLocalSettings", x => x.ProductId);
                    table.CheckConstraint("CK_catalog_ProductLocalSettings_CurrencyCode_Length", "LEN([CurrencyCode]) = 3");
                    table.CheckConstraint("CK_catalog_ProductLocalSettings_CurrentSalePrice_NonNegative", "[CurrentSalePrice] >= 0");
                    table.CheckConstraint("CK_catalog_ProductLocalSettings_InternalCode_NotBlank", "[InternalCode] IS NULL OR LEN(LTRIM(RTRIM([InternalCode]))) > 0");
                    table.CheckConstraint("CK_catalog_ProductLocalSettings_QuantityScale_Range", "[QuantityScale] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_catalog_ProductLocalSettings_StockThresholds_NonNegative", "([MinimumStock] IS NULL OR [MinimumStock] >= 0) AND ([MaximumStock] IS NULL OR [MaximumStock] >= 0)");
                    table.CheckConstraint("CK_catalog_ProductLocalSettings_StockThresholds_Order", "[MinimumStock] IS NULL OR [MaximumStock] IS NULL OR [MinimumStock] <= [MaximumStock]");
                    table.ForeignKey(
                        name: "FK_catalog_ProductLocalSettings_ProductCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "catalog",
                        principalTable: "ProductCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_catalog_ProductLocalSettings_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductPriceHistories",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    OldPrice = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    NewPrice = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ChangeOrigin = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EffectiveAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_ProductPriceHistories", x => x.Id);
                    table.CheckConstraint("CK_catalog_ProductPriceHistories_ChangeOrigin_NotBlank", "LEN(LTRIM(RTRIM([ChangeOrigin]))) > 0");
                    table.CheckConstraint("CK_catalog_ProductPriceHistories_CurrencyCode_Length", "LEN([CurrencyCode]) = 3");
                    table.CheckConstraint("CK_catalog_ProductPriceHistories_NewPrice_NonNegative", "[NewPrice] >= 0");
                    table.CheckConstraint("CK_catalog_ProductPriceHistories_OldPrice_NonNegative", "[OldPrice] IS NULL OR [OldPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_catalog_ProductPriceHistories_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_ProductBarcodes_ProductId",
                schema: "catalog",
                table: "ProductBarcodes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "UX_catalog_ProductBarcodes_NormalizedValue",
                schema: "catalog",
                table: "ProductBarcodes",
                column: "NormalizedValue",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_catalog_ProductBarcodes_OneActivePrimaryPerProduct",
                schema: "catalog",
                table: "ProductBarcodes",
                columns: new[] { "ProductId", "IsPrimary", "IsActive" },
                unique: true,
                filter: "[IsPrimary] = 1 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_ProductCategories_Name",
                schema: "catalog",
                table: "ProductCategories",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "UX_catalog_ProductCategories_Code",
                schema: "catalog",
                table: "ProductCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_ProductLocalSettings_CategoryId",
                schema: "catalog",
                table: "ProductLocalSettings",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "UX_catalog_ProductLocalSettings_InternalCode",
                schema: "catalog",
                table: "ProductLocalSettings",
                column: "InternalCode",
                unique: true,
                filter: "[InternalCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_ProductPriceHistories_ProductId_EffectiveAtUtc",
                schema: "catalog",
                table: "ProductPriceHistories",
                columns: new[] { "ProductId", "EffectiveAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_Products_Name",
                schema: "catalog",
                table: "Products",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductBarcodes",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ProductLocalSettings",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ProductPriceHistories",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ProductCategories",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "catalog");
        }
    }
}
