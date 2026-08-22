using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeActivityPersistenceV01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FinalizedByUserAccountId",
                schema: "sales",
                table: "Sales",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PerformedByUserAccountId",
                schema: "sales",
                table: "SaleReturns",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserSessions",
                schema: "security",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EndReason = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_UserSessions", x => x.Id);
                    table.CheckConstraint("CK_security_UserSessions_End_Coherent", "([Status] = N'Closed' AND [EndedAtUtc] IS NOT NULL) OR ([Status] IN (N'Open', N'Interrupted') AND [EndedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_security_UserSessions_Status", "[Status] IN (N'Open', N'Closed', N'Interrupted')");
                    table.ForeignKey(
                        name: "FK_security_UserSessions_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalSchema: "security",
                        principalTable: "UserAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                schema: "security",
                table: "Permissions",
                columns: new[] { "Id", "Code", "Description", "IsSensitive", "Module", "Name" },
                values: new object[,]
                {
                    { 29L, "sales.view_all_history", "Permission explicite sales.view_all_history.", true, "sales", "sales.view_all_history" },
                    { 30L, "security.view_employee_activity", "Permission explicite security.view_employee_activity.", true, "security", "security.view_employee_activity" }
                });

            migrationBuilder.InsertData(
                schema: "security",
                table: "RolePermissions",
                columns: new[] { "Id", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 29L, 29L, 1L },
                    { 30L, 30L, 1L },
                    { 127L, 29L, 2L },
                    { 128L, 30L, 2L }
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_Sales_Actor_Timeline",
                schema: "sales",
                table: "Sales",
                columns: new[] { "FinalizedByUserAccountId", "CompletedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_SaleReturns_Actor_Timeline",
                schema: "sales",
                table: "SaleReturns",
                columns: new[] { "PerformedByUserAccountId", "CompletedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_security_UserSessions_User_Status_Timeline",
                schema: "security",
                table: "UserSessions",
                columns: new[] { "UserAccountId", "Status", "StartedAtUtc", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_sales_SaleReturns_UserAccounts_PerformedByUserAccountId",
                schema: "sales",
                table: "SaleReturns",
                column: "PerformedByUserAccountId",
                principalSchema: "security",
                principalTable: "UserAccounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_sales_Sales_UserAccounts_FinalizedByUserAccountId",
                schema: "sales",
                table: "Sales",
                column: "FinalizedByUserAccountId",
                principalSchema: "security",
                principalTable: "UserAccounts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sales_SaleReturns_UserAccounts_PerformedByUserAccountId",
                schema: "sales",
                table: "SaleReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_Sales_UserAccounts_FinalizedByUserAccountId",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "UserSessions",
                schema: "security");

            migrationBuilder.DropIndex(
                name: "IX_sales_Sales_Actor_Timeline",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_sales_SaleReturns_Actor_Timeline",
                schema: "sales",
                table: "SaleReturns");

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 29L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 30L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 127L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 128L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 29L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 30L);

            migrationBuilder.DropColumn(
                name: "FinalizedByUserAccountId",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "PerformedByUserAccountId",
                schema: "sales",
                table: "SaleReturns");
        }
    }
}
