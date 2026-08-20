using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Pharmacy.Infrastructure.Deployment;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class Sprint18DeploymentTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ReleaseVersionHasOneMsBuildSourceAndIsVisibleAtRuntime()
    {
        var props = XDocument.Load(PathAt("Directory.Build.props"));
        Assert.Equal("1.0.0-rc.1", props.Descendants("PharmacyReleaseVersion").Single().Value);
        Assert.Equal("$(PharmacyReleaseVersion)", props.Descendants("Version").Single().Value);
        Assert.Equal("1.0.0-rc.1", PilotReleaseInfo.Version);
        Assert.Equal("Release Candidate pilote", PilotReleaseInfo.Channel);
    }

    [Fact]
    public void PilotConfigurationUsesIntegratedSecurityAndRejectsSecretFields()
    {
        var root = Path.Combine(Path.GetTempPath(), "PharmacySprint18Config", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var validPath = Path.Combine(root, "pilot.json");
        var invalidPath = Path.Combine(root, "invalid.json");
        try
        {
            File.WriteAllText(validPath, """
                {"ServerInstance":".\\PHARMACY","DatabaseName":"Pharmacy","BackupDirectory":"%ProgramData%\\LogicielPharmacie\\backups"}
                """);
            File.WriteAllText(invalidPath, """
                {"ServerInstance":".\\PHARMACY","DatabaseName":"Pharmacy","BackupDirectory":"C:\\Backup","Password":"interdit"}
                """);
            var configuration = PilotConfiguration.Load(validPath);
            var builder = new SqlConnectionStringBuilder(configuration.BuildDatabaseConnectionString());
            Assert.True(builder.IntegratedSecurity);
            Assert.True(builder.Encrypt);
            Assert.True(builder.TrustServerCertificate);
            Assert.Equal("Pharmacy", builder.InitialCatalog);
            Assert.DoesNotContain("Password", builder.ConnectionString, StringComparison.OrdinalIgnoreCase);
            Assert.Throws<JsonException>(() => PilotConfiguration.Load(invalidPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("localhost;Password=x", "Pharmacy")]
    [InlineData("localhost", "master")]
    [InlineData("localhost", "Pharmacy_Development")]
    [InlineData("localhost", "Pharmacy-Test")]
    public void PilotConfigurationRejectsUnsafeTargets(string server, string database)
    {
        var configuration = new PilotConfiguration(server, database, Path.GetTempPath());
        Assert.Throws<InvalidDataException>(configuration.Validate);
    }

    [Fact]
    public void DesktopStartsOnlyAfterCompatibilityGuardAndNeverMigratesAtStartup()
    {
        var app = File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "App.xaml.cs"));
        Assert.Contains("DatabaseCompatibilityGuard.CheckAsync", app, StringComparison.Ordinal);
        Assert.Contains("Shutdown(2)", app, StringComparison.Ordinal);
        Assert.DoesNotContain("MigrateAsync", app, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureCreated", app, StringComparison.Ordinal);
    }

    [Fact]
    public void PackagingIsSelfContainedUntrimmedX64AndPreservesData()
    {
        var build = File.ReadAllText(PathAt("scripts", "Build-Sprint18Release.ps1"));
        var installer = File.ReadAllText(PathAt("installer", "PharmacyPilot.iss"));
        Assert.Contains("--self-contained true", build, StringComparison.Ordinal);
        Assert.Contains("PublishSingleFile=false", build, StringComparison.Ordinal);
        Assert.Contains("PublishTrimmed=false", build, StringComparison.Ordinal);
        Assert.Contains("ArchitecturesAllowed=x64compatible", installer, StringComparison.Ordinal);
        Assert.Contains("onlyifdoesntexist uninsneveruninstall", installer, StringComparison.Ordinal);
        Assert.Contains("install-or-upgrade", installer, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP DATABASE", installer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pilot.json\"; Type: files", installer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeploymentProjectIsSeparateAndInstallerHasStableIdentity()
    {
        var solution = File.ReadAllText(PathAt("Pharmacy.slnx"));
        var installer = File.ReadAllText(PathAt("installer", "PharmacyPilot.iss"));
        Assert.Contains("src/Pharmacy.Deployment/Pharmacy.Deployment.csproj", solution, StringComparison.Ordinal);
        Assert.Contains("AppId={{5C722BC6-4642-4EAE-AFD1-2A29A7010174}", installer, StringComparison.Ordinal);
        Assert.Contains("CloseApplications=yes", installer, StringComparison.Ordinal);
        Assert.Contains("PrivilegesRequired=admin", installer, StringComparison.Ordinal);
    }

    [Fact]
    public void PromptIsCompleteAndGeneratedArtifactsAreIgnored()
    {
        var prompt = File.ReadAllLines(PathAt("docs", "prompts", "Sprint_18_Packaging_Pilot_UAT_Release.md"));
        Assert.Equal("FIN DU PROMPT SPRINT 18", prompt.Last(line => !string.IsNullOrWhiteSpace(line)));
        var ignore = File.ReadAllText(PathAt(".gitignore"));
        Assert.Contains("artifacts/", ignore, StringComparison.Ordinal);
        Assert.Contains("*.bak", ignore, StringComparison.Ordinal);
        Assert.Contains("*.pfx", ignore, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeveloperValidatesFreshDatabaseMigrationsHealthAndSanitizedSupportBundle()
    {
        var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException("PHARMACY_TEST_SQLSERVER_CONNECTION est requise.");
        var server = new SqlConnectionStringBuilder(raw) { InitialCatalog = "master", Pooling = false };
        var databaseName = "Pharmacy_Sprint18_Test_" + Guid.NewGuid().ToString("N")[..12];
        var output = Path.Combine(Path.GetTempPath(), "PharmacySprint18Support", Guid.NewGuid().ToString("N"));
        var configuration = new PilotConfiguration(server.DataSource, databaseName, Path.GetTempPath());
        var manager = new PilotDeploymentManager(configuration, allowIsolatedTestDatabase: true);
        try
        {
            var outcome = await manager.InstallOrUpgradeAsync(TestContext.Current.CancellationToken);
            Assert.True(outcome.Passed);
            Assert.True(outcome.DatabaseCreated);
            Assert.True(outcome.MigrationApplied);
            Assert.Null(outcome.BackupPath);
            Assert.Equal(0, outcome.Health.PendingMigrations);
            Assert.Equal(5, outcome.Health.CriticalTables);
            var bundle = await manager.CreateSupportBundleAsync(output, TestContext.Current.CancellationToken);
            var bundleText = await File.ReadAllTextAsync(bundle, TestContext.Current.CancellationToken);
            using var bundleJson = JsonDocument.Parse(bundleText);
            Assert.Equal("[MASQUÉ]", bundleJson.RootElement.GetProperty("Server").GetString());
            Assert.DoesNotContain(server.DataSource, bundleText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, await ScalarAsync(server.ConnectionString, $"SELECT COUNT(*) FROM [{databaseName}].security.UserAccounts;"));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(server.ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END;";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task ConfiguredSqlServerPreflightReportsReachableEdition()
    {
        var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException("PHARMACY_TEST_SQLSERVER_CONNECTION est requise.");
        var server = new SqlConnectionStringBuilder(raw);
        var configuration = new PilotConfiguration(server.DataSource, "Pharmacy_Sprint18_Test_Preflight", Path.GetTempPath());
        var manager = new PilotDeploymentManager(configuration, allowIsolatedTestDatabase: true);
        var checks = await manager.PreflightAsync(TestContext.Current.CancellationToken);
        var connection = Assert.Single(checks, check => check.Name == "Connexion SQL");
        Assert.True(connection.Passed);
        Assert.False(string.IsNullOrWhiteSpace(connection.Detail));
        var express = Assert.Single(checks, check => check.Name == "SQL Server Express");
        Assert.Equal(connection.Detail.Contains("Express", StringComparison.OrdinalIgnoreCase), express.Passed);
    }

    [Fact]
    public void ExpressEditionSatisfiesPilotGate()
    {
        var express = PilotDeploymentManager.EvaluateExpressEdition("Express Edition (64-bit)");

        Assert.Equal("SQL Server Express", express.Name);
        Assert.True(express.Passed);
        Assert.Equal("Express Edition (64-bit)", express.Detail);
    }

    [Fact]
    public void DeveloperEditionKeepsPilotGatePending()
    {
        var express = PilotDeploymentManager.EvaluateExpressEdition("Developer Edition (64-bit)");

        Assert.Equal("SQL Server Express", express.Name);
        Assert.False(express.Passed);
        Assert.Contains("Developer", express.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Express est requis", express.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<int> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static string PathAt(params string[] parts) =>
        parts.Aggregate(RepositoryRoot, Path.Combine);

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Pharmacy.slnx"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Racine du dépôt introuvable.");
    }
}
