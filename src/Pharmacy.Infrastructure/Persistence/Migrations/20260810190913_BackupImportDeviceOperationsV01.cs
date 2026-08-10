using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackupImportDeviceOperationsV01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "operations");

            migrationBuilder.CreateTable(
                name: "BackupRecords",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BackupType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    VerificationStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExternalCopyStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operations_BackupRecords", x => x.Id);
                    table.CheckConstraint("CK_operations_BackupRecords_FileName", "LEN(LTRIM(RTRIM([FileName]))) > 0 AND [FileName] NOT LIKE N'%\\%' AND [FileName] NOT LIKE N'%/%'");
                    table.CheckConstraint("CK_operations_BackupRecords_Status", "[Status] IN (N'Running',N'Completed',N'Failed')");
                    table.CheckConstraint("CK_operations_BackupRecords_Type", "[BackupType] IN (N'Full',N'Differential',N'TransactionLog')");
                });

            migrationBuilder.CreateTable(
                name: "ImportJobs",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ImportType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    SourceFileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReadCount = table.Column<int>(type: "int", nullable: false),
                    CreatedCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedCount = table.Column<int>(type: "int", nullable: false),
                    IgnoredCount = table.Column<int>(type: "int", nullable: false),
                    RejectedCount = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operations_ImportJobs", x => x.Id);
                    table.CheckConstraint("CK_operations_ImportJobs_Counts", "[ReadCount]>=0 AND [CreatedCount]>=0 AND [UpdatedCount]>=0 AND [IgnoredCount]>=0 AND [RejectedCount]>=0");
                    table.CheckConstraint("CK_operations_ImportJobs_FileName", "LEN(LTRIM(RTRIM([SourceFileName]))) > 0 AND [SourceFileName] NOT LIKE N'%\\%' AND [SourceFileName] NOT LIKE N'%/%'");
                    table.CheckConstraint("CK_operations_ImportJobs_Status", "[Status] IN (N'Completed',N'Failed')");
                    table.CheckConstraint("CK_operations_ImportJobs_Type", "[ImportType] IN (N'Catalogue',N'OpeningStock',N'Lots')");
                });

            migrationBuilder.InsertData(
                schema: "security",
                table: "Permissions",
                columns: new[] { "Id", "Code", "Description", "IsSensitive", "Module", "Name" },
                values: new object[,]
                {
                    { 23L, "backup.view", "Permission explicite backup.view.", false, "backup", "backup.view" },
                    { 24L, "backup.create", "Permission explicite backup.create.", false, "backup", "backup.create" },
                    { 25L, "backup.restore", "Permission explicite backup.restore.", true, "backup", "backup.restore" },
                    { 26L, "import.view", "Permission explicite import.view.", false, "import", "import.view" },
                    { 27L, "import.execute", "Permission explicite import.execute.", true, "import", "import.execute" },
                    { 28L, "devices.configure", "Permission explicite devices.configure.", true, "devices", "devices.configure" }
                });

            migrationBuilder.InsertData(
                schema: "security",
                table: "RolePermissions",
                columns: new[] { "Id", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 23L, 23L, 1L },
                    { 24L, 24L, 1L },
                    { 25L, 25L, 1L },
                    { 26L, 26L, 1L },
                    { 27L, 27L, 1L },
                    { 28L, 28L, 1L }
                });

            migrationBuilder.CreateIndex(
                name: "IX_operations_BackupRecords_Timeline",
                schema: "operations",
                table: "BackupRecords",
                columns: new[] { "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_operations_BackupRecords_OperationId",
                schema: "operations",
                table: "BackupRecords",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operations_ImportJobs_Timeline",
                schema: "operations",
                table: "ImportJobs",
                columns: new[] { "StartedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_operations_ImportJobs_Fingerprint",
                schema: "operations",
                table: "ImportJobs",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_operations_ImportJobs_OperationId",
                schema: "operations",
                table: "ImportJobs",
                column: "OperationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupRecords",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "ImportJobs",
                schema: "operations");

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 23L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 24L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 25L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 26L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 27L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 28L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 23L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 24L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 25L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 26L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 27L);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 28L);
        }
    }
}
