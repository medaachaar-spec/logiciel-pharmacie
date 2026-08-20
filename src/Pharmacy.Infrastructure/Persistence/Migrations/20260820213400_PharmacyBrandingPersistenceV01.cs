using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PharmacyBrandingPersistenceV01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "configuration");

            migrationBuilder.CreateTable(
                name: "PharmacyBranding",
                schema: "configuration",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PharmacyName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PrimaryColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    LogoContent = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    LogoMimeType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LogoFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuration_PharmacyBranding", x => x.Id);
                    table.CheckConstraint("CK_configuration_PharmacyBranding_Logo_Metadata", "([LogoContent] IS NULL AND [LogoMimeType] IS NULL AND [LogoFileName] IS NULL) OR ([LogoContent] IS NOT NULL AND [LogoMimeType] IS NOT NULL AND [LogoFileName] IS NOT NULL)");
                    table.CheckConstraint("CK_configuration_PharmacyBranding_Logo_Size", "[LogoContent] IS NULL OR DATALENGTH([LogoContent]) <= 2097152");
                    table.CheckConstraint("CK_configuration_PharmacyBranding_Name_NotBlank", "LEN(LTRIM(RTRIM([PharmacyName]))) > 0");
                    table.CheckConstraint("CK_configuration_PharmacyBranding_PrimaryColor", "[PrimaryColor] LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'");
                    table.CheckConstraint("CK_configuration_PharmacyBranding_Singleton", "[Id] = 1");
                });

            migrationBuilder.InsertData(
                schema: "configuration",
                table: "PharmacyBranding",
                columns: new[] { "Id", "LogoContent", "LogoFileName", "LogoMimeType", "PharmacyName", "PrimaryColor", "UpdatedAtUtc" },
                values: new object[] { 1L, null, null, null, "Pharmacie Diar Douha", "#0D9A48", new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PharmacyBranding",
                schema: "configuration");
        }
    }
}
