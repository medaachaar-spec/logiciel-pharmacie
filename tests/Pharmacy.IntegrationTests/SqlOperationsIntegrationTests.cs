using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Operations.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Infrastructure.Operations;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class SqlOperationsIntegrationTests
{
    private const string Prefix = "Pharmacy_Sprint17_Test_";
    private static readonly DateTime Now = new(2026, 8, 10, 19, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RealSqlValidatesControlledImportBackupRestoreCleanupAndDeviceDiagnostics()
    {
        var token = TestContext.Current.CancellationToken;
        var server = BuildServerConnection();
        var databaseName = Prefix + Guid.NewGuid().ToString("N")[..12];
        Assert.Matches(new Regex($"^{Prefix}[0-9a-f]{{12}}$", RegexOptions.CultureInvariant), databaseName);
        var database = new SqlConnectionStringBuilder(server)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Pharmacy.Sprint17.Tests",
            Pooling = false
        };
        string? backupPath = null;
        string? externalDirectory = null;
        string? externalBackupPath = null;
        var previousExternalDirectory = Environment.GetEnvironmentVariable("PHARMACY_BACKUP_EXTERNAL_DIRECTORY");
        var previousExternalEncryption = Environment.GetEnvironmentVariable("PHARMACY_BACKUP_EXTERNAL_ENCRYPTED");
        var restoreOperationId = Guid.NewGuid();
        var restoreDatabaseName = "Pharmacy_RestoreTest_" + restoreOperationId.ToString("N")[..12];
        await CreateDatabaseAsync(server, databaseName);

        try
        {
            var factory = new Factory(database.ConnectionString);
            await using (var context = await factory.CreateDbContextAsync(token))
            {
                await context.Database.MigrateAsync(token);
                Assert.Empty(await context.Database.GetPendingMigrationsAsync(token));
                Assert.Contains(await context.Database.GetAppliedMigrationsAsync(token), migration =>
                    migration.EndsWith("_BackupImportDeviceOperationsV01", StringComparison.Ordinal));
            }

            Assert.Equal(2, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'operations';"));
            Assert.Equal(28, await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM security.Permissions;"));
            await ExecuteAsync(database.ConnectionString,
                "INSERT INTO catalog.ProductCategories(Code,Name,IsActive,DefaultTracksLots,DefaultTracksExpiry,DefaultDiscountAllowed,SortOrder) VALUES(N'GEN',N'Générique import',1,0,0,0,0);");

            var clock = new FixedClock(Now);
            var session = new CurrentSecuritySession();
            var authorizer = new PermissionAuthorizer(session, clock);
            var import = new SqlControlledImportService(factory, authorizer, new NoOpAuditRecorder(), clock);
            var mapping = new CsvImportMapping("Reference", "Designation", "PrixVente", null, "CodeBarres", "Categorie");
            var validContent = Encoding.UTF8.GetBytes("Reference;Designation;PrixVente;CodeBarres;Categorie\r\nS17-001;Produit synthétique;19.95;6111111111111;GEN\r\nS17-002;منتج اصطناعي;8.50;6111111111112;GEN\r\n");

            var denied = await import.PreviewAsync(new PreviewImportRequest("catalogue.csv", validContent, ControlledImportType.Catalogue, mapping), token);
            Assert.Equal("security.authorization.authentication_required", denied.Error.Code);
            session.Establish(new UserSessionView(
                1700, "owner.s17", "Propriétaire Sprint 17", ["OWNER"],
                new HashSet<string>([
                    SecurityPermissions.BackupView, SecurityPermissions.BackupCreate, SecurityPermissions.BackupRestore,
                    SecurityPermissions.ImportView, SecurityPermissions.ImportExecute, SecurityPermissions.DevicesConfigure
                ], StringComparer.Ordinal), Now, Now), Now);

            var unsafeName = await import.PreviewAsync(new PreviewImportRequest("../catalogue.csv", validContent, ControlledImportType.Catalogue, mapping), token);
            Assert.Equal("import.file.invalid", unsafeName.Error.Code);
            var formulaContent = Encoding.UTF8.GetBytes("Reference;Designation;PrixVente;CodeBarres;Categorie\r\nS17-X;=CMD;10;6222222222222;GEN\r\n");
            var formulaPreview = await import.PreviewAsync(new PreviewImportRequest("formula.csv", formulaContent, ControlledImportType.Catalogue, mapping), token);
            Assert.True(formulaPreview.IsSuccess);
            Assert.False(formulaPreview.Value.CanConfirm);
            Assert.Contains(formulaPreview.Value.Issues, issue => issue.Code == "import.formula.detected");

            var preview = await import.PreviewAsync(new PreviewImportRequest("catalogue.csv", validContent, ControlledImportType.Catalogue, mapping), token);
            Assert.True(preview.IsSuccess, preview.IsFailure ? preview.Error.Message : null);
            Assert.True(preview.Value.CanConfirm);
            Assert.Equal(2, preview.Value.TotalRows);
            var largeCsv = new StringBuilder("Reference;Designation;PrixVente;Categorie\r\n");
            for (var index = 1; index <= 8_100; index++) largeCsv.Append($"BULK-{index:00000};Référence synthétique {index};1.25;GEN\r\n");
            var largePreview = await import.PreviewAsync(new PreviewImportRequest(
                "catalogue-8100.csv", Encoding.UTF8.GetBytes(largeCsv.ToString()), ControlledImportType.Catalogue,
                new CsvImportMapping("Reference", "Designation", "PrixVente", CategoryColumn: "Categorie")), token);
            Assert.True(largePreview.IsSuccess, largePreview.IsFailure ? largePreview.Error.Message : null);
            Assert.True(largePreview.Value.CanConfirm);
            Assert.Equal(8_100, largePreview.Value.TotalRows);
            Assert.Equal(100, largePreview.Value.Rows.Count);
            var operationId = Guid.NewGuid();
            var imported = await import.ConfirmAsync(new ConfirmImportRequest(operationId, "catalogue.csv", validContent, ControlledImportType.Catalogue, mapping), token);
            Assert.True(imported.IsSuccess, imported.IsFailure ? imported.Error.Message : null);
            Assert.Equal(2, imported.Value.CreatedCount);
            var replay = await import.ConfirmAsync(new ConfirmImportRequest(operationId, "catalogue.csv", validContent, ControlledImportType.Catalogue, mapping), token);
            Assert.True(replay.IsSuccess);
            Assert.True(replay.Value.WasReplay);
            Assert.Equal(2, await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM catalog.ProductLocalSettings WHERE InternalCode LIKE N'S17-00%';"));
            Assert.Equal(1, await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM operations.ImportJobs;"));
            var collisionContent = Encoding.UTF8.GetBytes("Reference;Designation;PrixVente;CodeBarres;Categorie\r\nS17-COLLIDE;Collision;3.00;6111111111111;GEN\r\n");
            var collision = await import.ConfirmAsync(new ConfirmImportRequest(Guid.NewGuid(), "collision.csv", collisionContent, ControlledImportType.Catalogue, mapping), token);
            Assert.Equal("import.validation.failed", collision.Error.Code);
            Assert.Equal(0, await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM catalog.ProductLocalSettings WHERE InternalCode=N'S17-COLLIDE';"));

            await ExecuteAsync(database.ConnectionString,
                "INSERT INTO inventory.StockLocations(CreationOperationId,CreationFingerprint,Code,Name,IsActive,IsPrimary,CreatedAtUtc) VALUES(NEWID(),N'S17|LOCATION',N'MAGASIN',N'Magasin synthétique',1,1,SYSUTCDATETIME());");
            var openingContent = Encoding.UTF8.GetBytes("Reference;Designation;Quantite\r\nS17-001;Produit synthétique;15\r\n");
            var openingMapping = new CsvImportMapping("Reference", "Designation", QuantityColumn: "Quantite", StockLocationCode: "MAGASIN");
            var opening = await import.ConfirmAsync(new ConfirmImportRequest(Guid.NewGuid(), "opening.csv", openingContent, ControlledImportType.OpeningStock, openingMapping), token);
            Assert.True(opening.IsSuccess, opening.IsFailure ? opening.Error.Message : null);
            Assert.Equal(1, opening.Value.CreatedCount);
            Assert.Equal(15m, await ScalarAsync<decimal>(database.ConnectionString, "SELECT SUM(b.AvailableQuantity) FROM inventory.StockBalances b JOIN catalog.ProductLocalSettings p ON p.ProductId=b.ProductId WHERE p.InternalCode=N'S17-001';"));
            await ExecuteAsync(database.ConnectionString, "UPDATE catalog.ProductLocalSettings SET TracksLots=1, TracksExpiry=1 WHERE InternalCode=N'S17-002';");
            var lotContent = Encoding.UTF8.GetBytes("Reference;Designation;Quantite;Lot;Peremption\r\nS17-002;منتج اصطناعي;9;LOT-S17;2028-12-31\r\n");
            var lotMapping = new CsvImportMapping("Reference", "Designation", QuantityColumn: "Quantite", LotColumn: "Lot", ExpiryColumn: "Peremption", StockLocationCode: "MAGASIN");
            var lots = await import.ConfirmAsync(new ConfirmImportRequest(Guid.NewGuid(), "lots.csv", lotContent, ControlledImportType.Lots, lotMapping), token);
            Assert.True(lots.IsSuccess, lots.IsFailure ? lots.Error.Message : null);
            Assert.Equal(1, lots.Value.CreatedCount);
            Assert.Equal(1, await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM inventory.StockBatches WHERE BatchNumber=N'LOT-S17' AND ExpiryDate='2028-12-31';"));
            Assert.Equal(3, await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM operations.ImportJobs;"));

            var backups = new SqlBackupRecoveryService(factory, authorizer, new NoOpAuditRecorder(), clock);
            var configuration = await backups.GetConfigurationAsync(token);
            Assert.True(configuration.IsSuccess, configuration.IsFailure ? configuration.Error.Message : null);
            Assert.Equal(databaseName, configuration.Value.DatabaseName);
            externalDirectory = Path.Combine(configuration.Value.LocalDirectory, "External_" + databaseName);
            Environment.SetEnvironmentVariable("PHARMACY_BACKUP_EXTERNAL_DIRECTORY", externalDirectory);
            Environment.SetEnvironmentVariable("PHARMACY_BACKUP_EXTERNAL_ENCRYPTED", "true");
            var created = await backups.CreateAsync(new CreateBackupRequest(Guid.NewGuid(), SqlBackupType.Full, true), token);
            Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
            backupPath = created.Value.Backup.FullPath;
            Assert.True(File.Exists(backupPath));
            Assert.NotNull(created.Value.Backup.Sha256);
            Assert.Equal(64, created.Value.Backup.Sha256!.Length);
            Assert.Equal("VerifyOnlyPassed", created.Value.Backup.VerificationStatus);
            Assert.Equal("CopiedEncryptedVolume", created.Value.Backup.ExternalCopyStatus);
            externalBackupPath = Path.Combine(externalDirectory, created.Value.Backup.FileName);
            Assert.True(File.Exists(externalBackupPath));
            var verified = await backups.VerifyAsync(new VerifyBackupRequest(Guid.NewGuid(), backupPath), token);
            Assert.True(verified.IsSuccess, verified.IsFailure ? verified.Error.Code : null);
            Assert.Equal(created.Value.Backup.Sha256, verified.Value.Sha256);
            Assert.Equal("backup.path.invalid", (await backups.VerifyAsync(new VerifyBackupRequest(Guid.NewGuid(), Path.Combine(Path.GetTempPath(), "outside.bak")), token)).Error.Code);
            session.Establish(new UserSessionView(
                1700, "owner.s17", "Propriétaire Sprint 17", ["OWNER"],
                new HashSet<string>([
                    SecurityPermissions.BackupView, SecurityPermissions.BackupCreate, SecurityPermissions.BackupRestore,
                    SecurityPermissions.ImportView, SecurityPermissions.ImportExecute, SecurityPermissions.DevicesConfigure
                ], StringComparer.Ordinal), Now.AddMinutes(-6), Now.AddMinutes(-6)), Now.AddMinutes(-6));
            Assert.Equal("security.authorization.reauthentication_required", (await backups.RestoreTestAsync(new RestoreTestRequest(restoreOperationId, backupPath), token)).Error.Code);
            session.MarkReauthenticated(Now);
            var restored = await backups.RestoreTestAsync(new RestoreTestRequest(restoreOperationId, backupPath), token);
            Assert.True(restored.IsSuccess, restored.IsFailure ? restored.Error.Code : null);
            Assert.True(restored.Value.HealthCheckPassed);
            Assert.True(restored.Value.CleanupPassed);
            Assert.Equal(0, await ScalarAsync<int>(server, "SELECT COUNT(*) FROM sys.databases WHERE name LIKE N'Pharmacy_RestoreTest_%';"));

            var diagnostic = await new LocalDeviceDiagnostics(factory, authorizer, new NoOpAuditRecorder()).CheckAsync(token);
            Assert.True(diagnostic.IsSuccess);
            Assert.Contains(diagnostic.Value.Items, item => item.Device == "Imprimante ticket" && item.Status == "Simulateur");
            Assert.Contains(diagnostic.Value.Items, item => item.Device == "TPE" && item.Status == "Externe manuel");
            Assert.Contains(diagnostic.Value.Items, item => item.Device == "UPS" && !item.IsReady);
            var printer = new SimulatedReceiptPrinter(new NoOpAuditRecorder());
            Assert.Equal("printing.sale.not_persisted", (await printer.PrintAsync(new ReceiptPrintRequest(
                Guid.NewGuid(), 0, "", Now, [], 0, "Cash", false), token)).Error.Code);
            var printed = await printer.PrintAsync(new ReceiptPrintRequest(
                Guid.NewGuid(), 99, "S17-SALE-001", Now, [new ReceiptLineData("Produit", 1, 10m, 10m)], 10m, "Cash", false), token);
            Assert.True(printed.IsSuccess);
            Assert.Equal("Simulated80mm", printed.Value.Status);
            Assert.True((await printer.PrintAsync(new ReceiptPrintRequest(
                Guid.NewGuid(), 99, "S17-SALE-001", Now, [new ReceiptLineData("Produit", 1, 10m, 10m)], 10m, "Cash", true), token)).IsSuccess);
            var terminal = new ManualPaymentTerminal();
            Assert.Equal(ManualTerminalOutcome.Accepted, (await terminal.ConfirmAsync(ManualTerminalOutcome.Accepted, token)).Value.Outcome);
            Assert.Equal(ManualTerminalOutcome.Declined, (await terminal.ConfirmAsync(ManualTerminalOutcome.Declined, token)).Value.Outcome);
            Assert.Equal(ManualTerminalOutcome.Cancelled, (await terminal.ConfirmAsync(ManualTerminalOutcome.Cancelled, token)).Value.Outcome);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PHARMACY_BACKUP_EXTERNAL_DIRECTORY", previousExternalDirectory);
            Environment.SetEnvironmentVariable("PHARMACY_BACKUP_EXTERNAL_ENCRYPTED", previousExternalEncryption);
            SqlConnection.ClearAllPools();
            await DropDatabasesAsync(server, databaseName, restoreDatabaseName);
            if (!string.IsNullOrWhiteSpace(externalBackupPath) && File.Exists(externalBackupPath)) File.Delete(externalBackupPath);
            if (!string.IsNullOrWhiteSpace(externalDirectory) && Directory.Exists(externalDirectory)) Directory.Delete(externalDirectory, false);
            if (!string.IsNullOrWhiteSpace(backupPath) && File.Exists(backupPath)) File.Delete(backupPath);
        }

        Assert.True(string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath));
        Assert.Equal(0, await ScalarAsync<int>(server, $"SELECT COUNT(*) FROM sys.databases WHERE name=N'{databaseName}' OR name LIKE N'Pharmacy_RestoreTest_%';"));
    }

    private static string BuildServerConnection()
    {
        var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException("PHARMACY_TEST_SQLSERVER_CONNECTION est requise.");
        return new SqlConnectionStringBuilder(raw) { InitialCatalog = "master", Pooling = false }.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string server, string name)
    {
        await using var connection = new SqlConnection(server); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = $"CREATE DATABASE [{name}]; ALTER DATABASE [{name}] SET RECOVERY FULL;"; await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabasesAsync(string server, string name, string restoreName)
    {
        Assert.StartsWith(Prefix, name, StringComparison.Ordinal);
        Assert.StartsWith("Pharmacy_RestoreTest_", restoreName, StringComparison.Ordinal);
        await using var connection = new SqlConnection(server); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{restoreName}') IS NOT NULL BEGIN ALTER DATABASE [{restoreName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{restoreName}]; END; IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END;";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = sql; return await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        var value = await command.ExecuteScalarAsync(); Assert.NotNull(value); return (T)Convert.ChangeType(value, typeof(T));
    }

    private sealed class Factory : IDbContextFactory<PharmacyDbContext>
    {
        private readonly DbContextOptions<PharmacyDbContext> options;
        public Factory(string connectionString) { var builder = new DbContextOptionsBuilder<PharmacyDbContext>(); builder.UsePharmacySqlServer(connectionString); options = builder.Options; }
        public PharmacyDbContext CreateDbContext() => new(options);
        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class FixedClock(DateTime now) : ISecurityClock { public DateTime UtcNow { get; } = now; }
    private sealed class NoOpAuditRecorder : IBusinessAuditRecorder
    {
        public Task RecordSuccessAsync(string module, string entityType, string? entityId, string action, Guid? operationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
