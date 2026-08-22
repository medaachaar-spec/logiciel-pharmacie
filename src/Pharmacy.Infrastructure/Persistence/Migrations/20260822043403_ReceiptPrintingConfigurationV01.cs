using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptPrintingConfigurationV01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationSettings",
                schema: "configuration",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ReceiptPrintingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuration_ApplicationSettings", x => x.Id);
                    table.CheckConstraint("CK_configuration_ApplicationSettings_Singleton", "[Id] = 1");
                });

            migrationBuilder.InsertData(
                schema: "configuration",
                table: "ApplicationSettings",
                columns: new[] { "Id", "ReceiptPrintingEnabled", "UpdatedAtUtc" },
                values: new object[] { 1L, false, new DateTime(2026, 8, 22, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationSettings",
                schema: "configuration");
        }
    }
}
