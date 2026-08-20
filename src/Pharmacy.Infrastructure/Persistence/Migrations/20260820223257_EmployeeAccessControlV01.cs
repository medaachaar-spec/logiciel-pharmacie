using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeAccessControlV01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPermissionOverrides",
                schema: "security",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    PermissionId = table.Column<long>(type: "bigint", nullable: false),
                    Effect = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_UserPermissionOverrides", x => x.Id);
                    table.CheckConstraint("CK_security_UserPermissionOverrides_Effect", "[Effect] IN (N'Allow', N'Deny')");
                    table.ForeignKey(
                        name: "FK_security_UserPermissionOverrides_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "security",
                        principalTable: "Permissions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_security_UserPermissionOverrides_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalSchema: "security",
                        principalTable: "UserAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                schema: "security",
                table: "Roles",
                columns: new[] { "Id", "Code", "Description", "IsActive", "IsSystemRole", "Name" },
                values: new object[,]
                {
                    { 2L, "MANAGER", "Responsable des opérations courantes et de la gestion des employés.", true, true, "Manager" },
                    { 3L, "SELLER", "Vente et consultations nécessaires au point de vente.", true, true, "Vendeur" }
                });

            migrationBuilder.InsertData(
                schema: "security",
                table: "RolePermissions",
                columns: new[] { "Id", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 100L, 1L, 2L },
                    { 101L, 2L, 2L },
                    { 102L, 3L, 2L },
                    { 103L, 4L, 2L },
                    { 104L, 5L, 2L },
                    { 105L, 6L, 2L },
                    { 106L, 7L, 2L },
                    { 107L, 8L, 2L },
                    { 108L, 9L, 2L },
                    { 109L, 10L, 2L },
                    { 110L, 11L, 2L },
                    { 111L, 12L, 2L },
                    { 112L, 13L, 2L },
                    { 113L, 14L, 2L },
                    { 114L, 15L, 2L },
                    { 115L, 16L, 2L },
                    { 116L, 17L, 2L },
                    { 117L, 18L, 2L },
                    { 118L, 19L, 2L },
                    { 119L, 20L, 2L },
                    { 120L, 21L, 2L },
                    { 121L, 22L, 2L },
                    { 122L, 23L, 2L },
                    { 123L, 24L, 2L },
                    { 124L, 26L, 2L },
                    { 125L, 27L, 2L },
                    { 126L, 28L, 2L },
                    { 200L, 1L, 3L },
                    { 201L, 2L, 3L },
                    { 202L, 10L, 3L },
                    { 203L, 5L, 3L },
                    { 204L, 13L, 3L }
                });

            migrationBuilder.CreateIndex(
                name: "IX_security_UserPermissionOverrides_UpdatedByUserId",
                schema: "security",
                table: "UserPermissionOverrides",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissionOverrides_PermissionId",
                schema: "security",
                table: "UserPermissionOverrides",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "UX_security_UserPermissionOverrides_User_Permission",
                schema: "security",
                table: "UserPermissionOverrides",
                columns: new[] { "UserAccountId", "PermissionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPermissionOverrides",
                schema: "security");

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 100L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 101L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 102L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 103L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 104L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 105L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 106L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 107L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 108L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 109L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 110L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 111L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 112L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 113L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 114L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 115L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 116L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 117L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 118L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 119L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 120L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 121L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 122L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 123L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 124L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 125L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 126L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 200L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 201L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 202L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 203L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 204L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3L);
        }
    }
}
