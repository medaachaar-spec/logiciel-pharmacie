using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CashCustomersCreditReturnsPersistenceV01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_sales_Sales_Amounts_NonNegative",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sales_Sales_Totals_Coherent",
                schema: "sales",
                table: "Sales");

            migrationBuilder.EnsureSchema(
                name: "cash");

            migrationBuilder.EnsureSchema(
                name: "customers");

            migrationBuilder.AddColumn<decimal>(
                name: "CreditTotal",
                schema: "sales",
                table: "Sales",
                type: "decimal(19,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "CustomerId",
                schema: "sales",
                table: "Sales",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashLedgerEntries",
                schema: "cash",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_CashLedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_cash_CashLedgerEntries_Amount_NotZero", "[Amount] <> 0");
                    table.CheckConstraint("CK_cash_CashLedgerEntries_EntryType", "[EntryType] IN (N'SaleCashIn', N'CreditPaymentCashIn', N'RefundCashOut')");
                    table.CheckConstraint("CK_cash_CashLedgerEntries_Source_NotBlank", "LEN(LTRIM(RTRIM([SourceType]))) > 0 AND LEN(LTRIM(RTRIM([SourceReference]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Reference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreditAllowed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers_Customers", x => x.Id);
                    table.CheckConstraint("CK_customers_Customers_FullName_NotBlank", "LEN(LTRIM(RTRIM([FullName]))) > 0");
                    table.CheckConstraint("CK_customers_Customers_Reference_NotBlank", "LEN(LTRIM(RTRIM([Reference]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "SaleReturns",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OriginalSaleId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    RefundMethod = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_SaleReturns", x => x.Id);
                    table.CheckConstraint("CK_sales_SaleReturns_Reference_NotBlank", "LEN(LTRIM(RTRIM([Reference]))) > 0");
                    table.CheckConstraint("CK_sales_SaleReturns_RefundAmount_NonNegative", "[RefundAmount] >= 0");
                    table.CheckConstraint("CK_sales_SaleReturns_RefundMethod", "[RefundMethod] IN (N'Cash', N'ExternalCardPending', N'CreditReduction')");
                    table.CheckConstraint("CK_sales_SaleReturns_Status", "[Status] = N'Completed'");
                });

            migrationBuilder.CreateTable(
                name: "SuspendedSales",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerNameSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    GrandTotal = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SuspendedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_SuspendedSales", x => x.Id);
                    table.CheckConstraint("CK_sales_SuspendedSales_GrandTotal_NonNegative", "[GrandTotal] >= 0");
                    table.CheckConstraint("CK_sales_SuspendedSales_LineCount_Positive", "[LineCount] > 0");
                    table.CheckConstraint("CK_sales_SuspendedSales_Reference_NotBlank", "LEN(LTRIM(RTRIM([Reference]))) > 0");
                    table.CheckConstraint("CK_sales_SuspendedSales_Status", "[Status] IN (N'Open', N'Resumed', N'Abandoned')");
                });

            migrationBuilder.CreateTable(
                name: "CustomerLedgerEntries",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers_CustomerLedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_customers_CustomerLedgerEntries_Amount_NonNegative", "[Amount] >= 0");
                    table.CheckConstraint("CK_customers_CustomerLedgerEntries_EntryType", "[EntryType] IN (N'CreditSale', N'CreditPayment', N'CreditAdjustmentIncrease', N'CreditAdjustmentDecrease', N'RefundImpact')");
                    table.CheckConstraint("CK_customers_CustomerLedgerEntries_Source_NotBlank", "LEN(LTRIM(RTRIM([SourceType]))) > 0 AND LEN(LTRIM(RTRIM([SourceReference]))) > 0");
                    table.ForeignKey(
                        name: "FK_customers_CustomerLedgerEntries_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "customers",
                        principalTable: "Customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SaleReturnLines",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleReturnId = table.Column<long>(type: "bigint", nullable: false),
                    SaleLineId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    StockDestination = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_SaleReturnLines", x => x.Id);
                    table.CheckConstraint("CK_sales_SaleReturnLines_Quantity_Positive", "[Quantity] > 0");
                    table.CheckConstraint("CK_sales_SaleReturnLines_StockDestination", "[StockDestination] IN (N'RestockApproved', N'NotRestockable')");
                    table.CheckConstraint("CK_sales_SaleReturnLines_Total_Coherent", "[LineTotal] = [UnitPrice] * [Quantity]");
                    table.ForeignKey(
                        name: "FK_sales_SaleReturnLines_SaleReturns_SaleReturnId",
                        column: x => x.SaleReturnId,
                        principalSchema: "sales",
                        principalTable: "SaleReturns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SuspendedSaleLines",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SuspendedSaleId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_sales_SuspendedSaleLines", x => x.Id);
                    table.CheckConstraint("CK_sales_SuspendedSaleLines_Quantity_Positive", "[Quantity] > 0");
                    table.CheckConstraint("CK_sales_SuspendedSaleLines_Reference_NotBlank", "LEN(LTRIM(RTRIM([ProductReferenceSnapshot]))) > 0");
                    table.CheckConstraint("CK_sales_SuspendedSaleLines_Total_Coherent", "[LineTotal] = [UnitPrice] * [Quantity]");
                    table.ForeignKey(
                        name: "FK_sales_SuspendedSaleLines_SuspendedSales_SuspendedSaleId",
                        column: x => x.SuspendedSaleId,
                        principalSchema: "sales",
                        principalTable: "SuspendedSales",
                        principalColumn: "Id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_sales_Sales_Amounts_NonNegative",
                schema: "sales",
                table: "Sales",
                sql: "[GrandTotal] >= 0 AND [PaidTotal] >= 0 AND [CreditTotal] >= 0 AND [ChangeGiven] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sales_Sales_Credit_Customer",
                schema: "sales",
                table: "Sales",
                sql: "([CreditTotal] = 0 AND [CustomerId] IS NULL) OR ([CreditTotal] > 0 AND [CustomerId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sales_Sales_Totals_Coherent",
                schema: "sales",
                table: "Sales",
                sql: "[PaidTotal] + [CreditTotal] = [GrandTotal]");

            migrationBuilder.CreateIndex(
                name: "IX_cash_CashLedgerEntries_Timeline",
                schema: "cash",
                table: "CashLedgerEntries",
                columns: new[] { "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_cash_CashLedgerEntries_OperationId",
                schema: "cash",
                table: "CashLedgerEntries",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_CustomerLedgerEntries_Customer_Timeline",
                schema: "customers",
                table: "CustomerLedgerEntries",
                columns: new[] { "CustomerId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_customers_CustomerLedgerEntries_OperationId",
                schema: "customers",
                table: "CustomerLedgerEntries",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_Customers_FullName_Id",
                schema: "customers",
                table: "Customers",
                columns: new[] { "FullName", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_Customers_Phone",
                schema: "customers",
                table: "Customers",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "UX_customers_Customers_Reference",
                schema: "customers",
                table: "Customers",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_SaleReturnId",
                schema: "sales",
                table: "SaleReturnLines",
                column: "SaleReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_SaleReturnLines_SaleLineId",
                schema: "sales",
                table: "SaleReturnLines",
                column: "SaleLineId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_SaleReturns_OriginalSaleId",
                schema: "sales",
                table: "SaleReturns",
                column: "OriginalSaleId");

            migrationBuilder.CreateIndex(
                name: "UX_sales_SaleReturns_OperationId",
                schema: "sales",
                table: "SaleReturns",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_sales_SaleReturns_Reference",
                schema: "sales",
                table: "SaleReturns",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SuspendedSaleLines_SuspendedSaleId",
                schema: "sales",
                table: "SuspendedSaleLines",
                column: "SuspendedSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_SuspendedSales_Status_Timeline",
                schema: "sales",
                table: "SuspendedSales",
                columns: new[] { "Status", "SuspendedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_sales_SuspendedSales_OperationId",
                schema: "sales",
                table: "SuspendedSales",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_sales_SuspendedSales_Reference",
                schema: "sales",
                table: "SuspendedSales",
                column: "Reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashLedgerEntries",
                schema: "cash");

            migrationBuilder.DropTable(
                name: "CustomerLedgerEntries",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "SaleReturnLines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SuspendedSaleLines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "SaleReturns",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SuspendedSales",
                schema: "sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sales_Sales_Amounts_NonNegative",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sales_Sales_Credit_Customer",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sales_Sales_Totals_Coherent",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CreditTotal",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sales",
                table: "Sales");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sales_Sales_Amounts_NonNegative",
                schema: "sales",
                table: "Sales",
                sql: "[GrandTotal] >= 0 AND [PaidTotal] >= 0 AND [ChangeGiven] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sales_Sales_Totals_Coherent",
                schema: "sales",
                table: "Sales",
                sql: "[PaidTotal] = [GrandTotal]");
        }
    }
}
