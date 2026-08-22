using Microsoft.Data.SqlClient;
using Pharmacy.Infrastructure.Deployment;
using Pharmacy.Infrastructure.Persistence;
using Xunit;

namespace Pharmacy.IntegrationTests;

/// <summary>
/// ZCODE-04A / ZC-007: DesktopConnectionResolver safety with synthetic inputs.
/// These tests are DB-INDEPENDENT: they never connect to any database and only
/// inspect the resolved connection STRING. The pilot configuration is a
/// synthetic temp file pointing at a non-existent server (ZC04-SYNTHETIC-SRV),
/// so even a bug could not reach a real pilot database. The real pilot
/// instance (.\PHARMACY) is never referenced.
///
/// Safety invariant: an explicitly supplied but rejected/malformed database
/// target must not silently resolve to a completely different writable
/// pilot/production database without the caller knowing.
/// </summary>
[Collection("Sprint 09 persistence serial")]
public sealed class Zc04ConnectionResolverTests
{
    private const string DevelopmentConnection = "Server=localhost;Database=Pharmacy_Development;Integrated Security=true;TrustServerCertificate=true";
    private const string StagingConnection = "Server=localhost;Database=Pharmacy_Staging;Integrated Security=true;TrustServerCertificate=true";
    private const string MalformedConnection = "this is not a connection string at all !!!";
    private const string DisallowedConnection = "Server=prod-sql-01;Database=Pharmacy_Prod;Integrated Security=true";
    private const string SyntheticPilotServer = "ZC04-SYNTHETIC-SRV";

    [Fact]
    public void ZC04A05A_ValidDevelopmentEnvironmentConnectionIsReturnedAsIs()
    {
        using var environment = ResolverEnvironment.WithPilotConfig(DevelopmentConnection);
        var resolved = DesktopConnectionResolver.Resolve();
        Assert.NotNull(resolved);
        var builder = new SqlConnectionStringBuilder(resolved);
        Assert.Equal("localhost", builder.DataSource, ignoreCase: true);
        Assert.Equal("Pharmacy_Development", builder.InitialCatalog, ignoreCase: true);
    }

    [Fact]
    public void ZC04A05B_ExplicitStagingTargetMustNotSilentlyFallThroughToThePilotDatabase()
    {
        using var environment = ResolverEnvironment.WithPilotConfig(StagingConnection);
        var exception = Assert.Throws<InvalidOperationException>(DesktopConnectionResolver.Resolve);
        Assert.Contains(PharmacyDbContextFactory.ConnectionEnvironmentVariable, exception.Message);
        Assert.DoesNotContain(SyntheticPilotServer, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ZC04A05C_MalformedEnvironmentConnectionMustNotSilentlyFallThroughToThePilotDatabase()
    {
        using var environment = ResolverEnvironment.WithPilotConfig(MalformedConnection);
        var exception = Assert.Throws<InvalidOperationException>(DesktopConnectionResolver.Resolve);
        Assert.Contains(PharmacyDbContextFactory.ConnectionEnvironmentVariable, exception.Message);
        Assert.DoesNotContain(MalformedConnection, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ZC04A05D_ExplicitDisallowedTargetWhilePilotConfigExistsMustNotResolveToThePilotDatabase()
    {
        using var environment = ResolverEnvironment.WithPilotConfig(DisallowedConnection);
        var exception = Assert.Throws<InvalidOperationException>(DesktopConnectionResolver.Resolve);
        Assert.Contains(PharmacyDbContextFactory.ConnectionEnvironmentVariable, exception.Message);
        Assert.DoesNotContain("prod-sql-01", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ZC04A05E_NoEnvironmentConnectionFallsBackOnlyToTheConfiguredPilotOrNull()
    {
        // With NO explicit env connection, falling back to the configured pilot
        // (or null when no pilot config exists at all) is the documented intended
        // behavior: no explicit target was rejected, so nothing is silent here.
        // The machine may legitimately carry a default pilot.json (.\PHARMACY).
        using var environment = ResolverEnvironment.WithoutPilotConfig(connection: null);
        var resolved = DesktopConnectionResolver.Resolve();
        if (resolved is null)
        {
            return;
        }

        var builder = new SqlConnectionStringBuilder(resolved);
        Assert.Equal("Pharmacy", builder.InitialCatalog, ignoreCase: true);
    }

    /// <summary>
    /// Manipulates the process environment variables and a synthetic pilot
    /// configuration file, restoring every previous value on dispose.
    /// Never connects anywhere: the synthetic pilot server does not exist.
    /// </summary>
    private sealed class ResolverEnvironment : IDisposable
    {
        private readonly string? previousConnection;
        private readonly string? previousPilotConfig;
        private readonly string? pilotConfigPath;

        private ResolverEnvironment(string? previousConnection, string? previousPilotConfig, string? pilotConfigPath)
        {
            this.previousConnection = previousConnection;
            this.previousPilotConfig = previousPilotConfig;
            this.pilotConfigPath = pilotConfigPath;
        }

        public static ResolverEnvironment WithPilotConfig(string connection) =>
            Create(connection, pilotConfigExists: true);

        public static ResolverEnvironment WithoutPilotConfig(string? connection) =>
            Create(connection, pilotConfigExists: false);

        private static ResolverEnvironment Create(string? connection, bool pilotConfigExists)
        {
            var previousConnection = Environment.GetEnvironmentVariable(PharmacyDbContextFactory.ConnectionEnvironmentVariable);
            var previousPilotConfig = Environment.GetEnvironmentVariable(PilotConfiguration.ConfigurationEnvironmentVariable);

            string? pilotConfigPath = null;
            if (pilotConfigExists)
            {
                var directory = Path.Combine(Path.GetTempPath(), "zc04-resolver-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                pilotConfigPath = Path.Combine(directory, "pilot.json");
                File.WriteAllText(pilotConfigPath, $$"""
                    {
                      "ServerInstance": "{{SyntheticPilotServer}}",
                      "DatabaseName": "Pharmacy",
                      "BackupDirectory": "{{directory.Replace("\\", "\\\\")}}"
                    }
                    """);
                Environment.SetEnvironmentVariable(PilotConfiguration.ConfigurationEnvironmentVariable, pilotConfigPath);
            }
            else
            {
                Environment.SetEnvironmentVariable(PilotConfiguration.ConfigurationEnvironmentVariable, null);
            }

            Environment.SetEnvironmentVariable(PharmacyDbContextFactory.ConnectionEnvironmentVariable, connection);
            return new ResolverEnvironment(previousConnection, previousPilotConfig, pilotConfigPath);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(PharmacyDbContextFactory.ConnectionEnvironmentVariable, previousConnection);
            Environment.SetEnvironmentVariable(PilotConfiguration.ConfigurationEnvironmentVariable, previousPilotConfig);
            if (pilotConfigPath is not null)
            {
                try
                {
                    var directory = Path.GetDirectoryName(pilotConfigPath);
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, recursive: true);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
