using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Deployment;

public sealed record DeploymentCheck(string Name, bool Passed, string Detail);
public sealed record DeploymentHealth(bool Passed, string Database, int AppliedMigrations, int PendingMigrations, int CriticalTables);
public sealed record DeploymentOutcome(bool Passed, bool DatabaseCreated, bool MigrationApplied, string? BackupPath, DeploymentHealth Health);

public sealed class PilotDeploymentManager
{
    private const long MinimumFreeBytes = 2L * 1024 * 1024 * 1024;
    private const string WebView2ClientId = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
    private readonly PilotConfiguration configuration;
    private readonly bool allowIsolatedTestDatabase;

    public PilotDeploymentManager(PilotConfiguration configuration)
        : this(configuration, allowIsolatedTestDatabase: false)
    {
    }

    internal PilotDeploymentManager(PilotConfiguration configuration, bool allowIsolatedTestDatabase)
    {
        configuration.Validate(allowIsolatedTestDatabase);
        this.configuration = configuration;
        this.allowIsolatedTestDatabase = allowIsolatedTestDatabase;
    }

    public async Task<IReadOnlyList<DeploymentCheck>> PreflightAsync(CancellationToken cancellationToken = default)
    {
        var windows = OperatingSystem.IsWindows();
        var checks = new List<DeploymentCheck>
        {
            new("Windows 11 x64", windows && Environment.Is64BitOperatingSystem && Environment.OSVersion.Version.Build >= 22000, RuntimeInformation.OSDescription),
            new("Version RC", PilotReleaseInfo.Version.Contains("-rc.", StringComparison.Ordinal), PilotReleaseInfo.Version),
            CheckDisk()
        };
        checks.Add(windows ? CheckAdministrator() : new("Droits administrateur", false, "Windows requis."));
        checks.Add(windows ? CheckWebView2() : new("WebView2 Evergreen", false, "Windows requis."));
        checks.Add(windows ? CheckSqlService() : new("Service SQL", false, "Windows requis."));

        try
        {
            await using var connection = new SqlConnection(configuration.BuildMasterConnectionString(allowIsolatedTestDatabase));
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CONVERT(nvarchar(128), SERVERPROPERTY('Edition'));";
            var edition = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? "Inconnue";
            checks.Add(new("Connexion SQL", true, edition));
            checks.Add(EvaluateExpressEdition(edition));
        }
        catch (SqlException)
        {
            checks.Add(new("Connexion SQL", false, "Instance inaccessible avec l'identité Windows courante."));
            checks.Add(new("SQL Server Express", false, "Édition non vérifiable."));
        }

        return checks.AsReadOnly();
    }

    internal static DeploymentCheck EvaluateExpressEdition(string? edition)
    {
        var detail = string.IsNullOrWhiteSpace(edition) ? "Édition inconnue" : edition.Trim();
        var isExpress = detail.Contains("Express", StringComparison.OrdinalIgnoreCase);
        return new(
            "SQL Server Express",
            isExpress,
            isExpress ? detail : $"{detail} — SQL Server Express est requis pour le pilote.");
    }

    public async Task<DeploymentOutcome> InstallOrUpgradeAsync(CancellationToken cancellationToken = default)
    {
        var databaseExists = await DatabaseExistsAsync(cancellationToken);
        var created = false;
        string? backupPath = null;
        if (!databaseExists)
        {
            await ExecuteMasterAsync($"CREATE DATABASE [{configuration.DatabaseName}];", cancellationToken);
            created = true;
        }

        try
        {
            await using var context = CreateContext();
            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            if (!created && pending.Length > 0)
            {
                backupPath = await CreateVerifiedBackupAsync(cancellationToken);
            }

            if (pending.Length > 0)
            {
                await context.Database.MigrateAsync(cancellationToken);
            }

            var health = await HealthAsync(cancellationToken);
            return new(health.Passed, created, pending.Length > 0, backupPath, health);
        }
        catch
        {
            if (created)
            {
                await DropFreshDatabaseAsync(cancellationToken);
            }
            throw;
        }
    }

    public async Task<DeploymentHealth> HealthAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).Count();
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
        await using var connection = new SqlConnection(configuration.BuildDatabaseConnectionString(allowIsolatedTestDatabase));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE (s.name=N'catalog' AND t.name=N'Products') OR (s.name=N'inventory' AND t.name=N'StockBalances') OR (s.name=N'sales' AND t.name=N'Sales') OR (s.name=N'security' AND t.name=N'UserAccounts') OR (s.name=N'operations' AND t.name=N'BackupRecords');";
        var critical = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return new(pending == 0 && critical == 5, configuration.DatabaseName, applied, pending, critical);
    }

    public async Task<string> CreateSupportBundleAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"support-bundle-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
        DeploymentHealth? health = null;
        string? databaseState = null;
        try { health = await HealthAsync(cancellationToken); }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException) { databaseState = ex.GetType().Name; }
        var payload = new
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Release = PilotReleaseInfo.Version,
            Channel = PilotReleaseInfo.Channel,
            Runtime = RuntimeInformation.FrameworkDescription,
            OS = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Database = configuration.DatabaseName,
            Server = "[MASQUÉ]",
            Health = health,
            DatabaseState = databaseState
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8, cancellationToken);
        return path;
    }

    public async Task RollbackAsync(string backupPath, string confirmation, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(confirmation, configuration.DatabaseName, StringComparison.Ordinal))
            throw new InvalidOperationException("La confirmation de base ne correspond pas.");
        var fullPath = Path.GetFullPath(backupPath);
        if (!File.Exists(fullPath) || !string.Equals(Path.GetExtension(fullPath), ".bak", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException("La sauvegarde de rollback est absente.", fullPath);
        var escaped = fullPath.Replace("'", "''", StringComparison.Ordinal);
        await ExecuteMasterAsync($"RESTORE VERIFYONLY FROM DISK=N'{escaped}' WITH CHECKSUM; ALTER DATABASE [{configuration.DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [{configuration.DatabaseName}] FROM DISK=N'{escaped}' WITH REPLACE, RECOVERY, CHECKSUM; ALTER DATABASE [{configuration.DatabaseName}] SET MULTI_USER;", cancellationToken);
    }

    private PharmacyDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<PharmacyDbContext>();
        builder.UsePharmacySqlServer(configuration.BuildDatabaseConnectionString(allowIsolatedTestDatabase));
        return new PharmacyDbContext(builder.Options);
    }

    private async Task<bool> DatabaseExistsAsync(CancellationToken token)
    {
        await using var connection = new SqlConnection(configuration.BuildMasterConnectionString(allowIsolatedTestDatabase));
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name=@name;";
        command.Parameters.AddWithValue("@name", configuration.DatabaseName);
        return Convert.ToInt32(await command.ExecuteScalarAsync(token)) == 1;
    }

    private async Task<string> CreateVerifiedBackupAsync(CancellationToken token)
    {
        Directory.CreateDirectory(configuration.BackupDirectory);
        var file = Path.Combine(configuration.BackupDirectory, $"Pharmacy_PreUpgrade_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak");
        var escaped = file.Replace("'", "''", StringComparison.Ordinal);
        await ExecuteMasterAsync($"BACKUP DATABASE [{configuration.DatabaseName}] TO DISK=N'{escaped}' WITH COPY_ONLY, INIT, CHECKSUM; RESTORE VERIFYONLY FROM DISK=N'{escaped}' WITH CHECKSUM;", token);
        if (!File.Exists(file) || new FileInfo(file).Length == 0) throw new IOException("La sauvegarde pré-migration est absente ou vide.");
        await using var stream = File.OpenRead(file);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
        await File.WriteAllTextAsync(file + ".sha256", hash + "  " + Path.GetFileName(file), Encoding.ASCII, token);
        return file;
    }

    private async Task ExecuteMasterAsync(string sql, CancellationToken token)
    {
        await using var connection = new SqlConnection(configuration.BuildMasterConnectionString(allowIsolatedTestDatabase));
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 300;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(token);
    }

    private async Task DropFreshDatabaseAsync(CancellationToken token)
    {
        var safePilot = string.Equals(configuration.DatabaseName, "Pharmacy", StringComparison.Ordinal);
        var safeTest = allowIsolatedTestDatabase && configuration.DatabaseName.StartsWith("Pharmacy_Sprint18_Test_", StringComparison.Ordinal);
        if (!safePilot && !safeTest) return;
        SqlConnection.ClearAllPools();
        await ExecuteMasterAsync($"IF DB_ID(N'{configuration.DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{configuration.DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{configuration.DatabaseName}]; END;", token);
    }

    private DeploymentCheck CheckDisk()
    {
        var root = Path.GetPathRoot(configuration.BackupDirectory) ?? configuration.BackupDirectory;
        var drive = new DriveInfo(root);
        return new("Espace disque", drive.AvailableFreeSpace >= MinimumFreeBytes, $"{drive.AvailableFreeSpace / 1024 / 1024} MiB libres");
    }

    [SupportedOSPlatform("windows")]
    private static DeploymentCheck CheckAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        var administrator = principal.IsInRole(WindowsBuiltInRole.Administrator);
        return new("Droits administrateur", administrator, administrator ? "Session élevée." : "Relancer depuis le Setup élevé.");
    }

    [SupportedOSPlatform("windows")]
    private static DeploymentCheck CheckWebView2()
    {
        var paths = new[]
        {
            $@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{WebView2ClientId}",
            $@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{WebView2ClientId}"
        };
        foreach (var path in paths)
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            var version = Convert.ToString(key?.GetValue("pv"));
            if (!string.IsNullOrWhiteSpace(version)) return new("WebView2 Evergreen", true, version);
        }
        return new("WebView2 Evergreen", false, "Runtime absent.");
    }

    [SupportedOSPlatform("windows")]
    private DeploymentCheck CheckSqlService()
    {
        var instance = configuration.ServerInstance.Split('\\').LastOrDefault();
        var expected = string.IsNullOrWhiteSpace(instance) || string.Equals(instance, configuration.ServerInstance, StringComparison.Ordinal)
            ? "MSSQLSERVER" : $"MSSQL${instance}";
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{expected}");
        return new("Service SQL", key is not null, key is null ? $"Service {expected} absent." : expected);
    }
}
