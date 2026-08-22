using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Branding;
using Pharmacy.Application.Branding.Contracts;
using Pharmacy.Application.Branding.Models;
using Pharmacy.Application.Operations.Contracts;
using Pharmacy.Application.Operations.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Infrastructure.Branding;
using Pharmacy.Infrastructure.Operations;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class SqlPharmacyBrandingIntegrationTests(PharmacyBrandingSqlFixture fixture)
    : IClassFixture<PharmacyBrandingSqlFixture>
{
    [Fact]
    public async Task BRAND007_AdminCanUpdatePharmacyName()
    {
        await fixture.ResetAsync();
        await using var provider = fixture.CreateProvider(authorized: true);
        var service = provider.GetRequiredService<IPharmacyBrandingService>();

        var updated = await UpdateAsync(service, pharmacyName: "  Pharmacie Nouvelle  ");

        Assert.True(updated.IsSuccess, updated.IsFailure ? updated.Error.Message : null);
        Assert.Equal("Pharmacie Nouvelle", updated.Value.PharmacyName);
    }

    [Fact]
    public async Task BRAND008_AdminCanUpdatePrimaryColor()
    {
        await fixture.ResetAsync();
        await using var provider = fixture.CreateProvider(authorized: true);
        var service = provider.GetRequiredService<IPharmacyBrandingService>();

        var updated = await UpdateAsync(service, primaryColor: "#246B45");

        Assert.True(updated.IsSuccess, updated.IsFailure ? updated.Error.Message : null);
        Assert.Equal("#246B45", updated.Value.PrimaryColor);
    }

    [Fact]
    public async Task BRAND009_AdminCanUpdateLogo()
    {
        await fixture.ResetAsync();
        await using var provider = fixture.CreateProvider(authorized: true);
        var service = provider.GetRequiredService<IPharmacyBrandingService>();
        var logo = fixture.ReadBundledLogo();

        var updated = await UpdateAsync(service, logo: logo);

        Assert.True(updated.IsSuccess, updated.IsFailure ? updated.Error.Message : null);
        Assert.Equal(logo, updated.Value.LogoContent);
        Assert.Equal("image/png", updated.Value.LogoMimeType);
    }

    [Fact]
    public async Task BRAND010_UnauthorizedUserCannotUpdateBranding()
    {
        var token = TestContext.Current.CancellationToken;
        await fixture.ResetAsync();
        await using var provider = fixture.CreateProvider(authorized: false);
        var service = provider.GetRequiredService<IPharmacyBrandingService>();

        var result = await UpdateAsync(service, pharmacyName: "Modification interdite");

        Assert.Equal("security.authorization.authentication_required", result.Error.Code);
        Assert.Equal(PharmacyBrandingDefaults.PharmacyName, (await service.GetAsync(token)).Value.PharmacyName);
    }

    [Fact]
    public async Task BRAND014_BrandingPersistsAfterNewDbContext()
    {
        var token = TestContext.Current.CancellationToken;
        await fixture.ResetAsync();
        await using var provider = fixture.CreateProvider(authorized: true);
        var service = provider.GetRequiredService<IPharmacyBrandingService>();
        var updated = await UpdateAsync(service, pharmacyName: "Pharmacie Contexte B", primaryColor: "#176C47");
        Assert.True(updated.IsSuccess);

        await using var context = await fixture.Factory.CreateDbContextAsync(token);
        var persisted = await context.PharmacyBranding.AsNoTracking().SingleAsync(token);

        Assert.Equal("Pharmacie Contexte B", persisted.PharmacyName);
        Assert.Equal("#176C47", persisted.PrimaryColor);
    }

    [Fact]
    public async Task BRAND015_BrandingPersistsAfterFullServiceProviderRebuild()
    {
        var token = TestContext.Current.CancellationToken;
        await fixture.ResetAsync();
        await using (var firstProvider = fixture.CreateProvider(authorized: true))
        {
            var firstService = firstProvider.GetRequiredService<IPharmacyBrandingService>();
            Assert.True((await UpdateAsync(firstService, pharmacyName: "Pharmacie Provider B")).IsSuccess);
        }

        await using var secondProvider = fixture.CreateProvider(authorized: false);
        var reloaded = await secondProvider.GetRequiredService<IPharmacyBrandingService>().GetAsync(token);

        Assert.True(reloaded.IsSuccess);
        Assert.Equal("Pharmacie Provider B", reloaded.Value.PharmacyName);
    }

    [Fact]
    public async Task BRAND016_ConcurrentSettingsUpdateIsHandledSafely()
    {
        var token = TestContext.Current.CancellationToken;
        await fixture.ResetAsync();
        await using var provider = fixture.CreateProvider(authorized: true);
        var service = provider.GetRequiredService<IPharmacyBrandingService>();
        var firstRead = (await service.GetAsync(token)).Value;
        var secondRead = (await service.GetAsync(token)).Value;

        var first = await service.UpdateAsync(Request(firstRead, "Pharmacie Manager A", firstRead.PrimaryColor), token);
        var second = await service.UpdateAsync(Request(secondRead, "Pharmacie Manager B", secondRead.PrimaryColor), token);

        Assert.True(first.IsSuccess);
        Assert.Equal("branding.concurrency.conflict", second.Error.Code);
        Assert.Equal("Pharmacie Manager A", (await service.GetAsync(token)).Value.PharmacyName);
    }

    [Fact]
    public async Task BRAND017_BrandingChangeIsAuditedWithoutBinaryBlobContent()
    {
        var token = TestContext.Current.CancellationToken;
        await fixture.ResetAsync();
        await using var provider = fixture.CreateProvider(authorized: true);
        var service = provider.GetRequiredService<IPharmacyBrandingService>();
        var current = (await service.GetAsync(token)).Value;
        var operationId = Guid.NewGuid();
        var logo = fixture.ReadBundledLogo();

        var result = await service.UpdateAsync(new UpdatePharmacyBrandingRequest(
            operationId, "Pharmacie Audit", "#246B45", logo, "image/png", "audit.png", current.RowVersion), token);

        Assert.True(result.IsSuccess);
        var audit = await fixture.ReadBrandingAuditAsync(operationId);
        Assert.Equal("branding.update", audit.Action);
        Assert.Equal("Success", audit.Result);
        Assert.Equal(1701L, audit.ActorUserId);
        Assert.Equal(new[] { "Logo", "PharmacyName", "PrimaryColor" }, audit.Fields.Order(StringComparer.Ordinal));
        Assert.All(audit.Values, value =>
        {
            Assert.DoesNotContain(Convert.ToBase64String(logo), value, StringComparison.Ordinal);
            Assert.True(value.Length <= 512);
        });
    }

    [Fact]
    public async Task BRAND018_OfficialBackupRestorePreservesBranding()
    {
        var token = TestContext.Current.CancellationToken;
        await fixture.ResetAsync();
        await using var provider = fixture.CreateProvider(authorized: true);
        var branding = provider.GetRequiredService<IPharmacyBrandingService>();
        var logo = fixture.ReadBundledLogo();
        var updated = await UpdateAsync(
            branding, "Pharmacie Sauvegardée", "#246B45", logo);
        Assert.True(updated.IsSuccess);
        var backup = provider.GetRequiredService<IBackupRecoveryService>();
        string? backupPath = null;
        try
        {
            var created = await backup.CreateAsync(new CreateBackupRequest(
                Guid.NewGuid(), SqlBackupType.Full, CopyToExternal: false), token);
            Assert.True(created.IsSuccess, created.IsFailure ? $"{created.Error.Code}: {created.Error.Message}" : null);
            backupPath = created.Value.Backup.FullPath;

            var restored = await backup.RestoreTestAsync(new RestoreTestRequest(Guid.NewGuid(), backupPath), token);

            Assert.True(restored.IsSuccess, restored.IsFailure ? restored.Error.Message : null);
            Assert.Equal(12, restored.Value.AppliedMigrationCount);
            Assert.Equal(5, restored.Value.CriticalTableCount);
            Assert.Equal("Pharmacie Sauvegardée", restored.Value.PharmacyName);
            Assert.Equal("#246B45", restored.Value.PrimaryColor);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(logo)), restored.Value.LogoSha256);
            Assert.True(restored.Value.CleanupPassed);
            Assert.Equal(0, await fixture.RestoreDatabaseCountAsync());
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(backupPath) && File.Exists(backupPath)) File.Delete(backupPath);
        }
    }

    private static async Task<Pharmacy.Domain.Common.Result<PharmacyBrandingView>> UpdateAsync(
        IPharmacyBrandingService service,
        string pharmacyName = "Pharmacie Diar Douha",
        string primaryColor = "#0D9A48",
        byte[]? logo = null)
    {
        var current = (await service.GetAsync()).Value;
        return await service.UpdateAsync(new UpdatePharmacyBrandingRequest(
            Guid.NewGuid(), pharmacyName, primaryColor, logo,
            logo is null ? null : "image/png", logo is null ? null : "logo.png", current.RowVersion));
    }

    private static UpdatePharmacyBrandingRequest Request(
        PharmacyBrandingView current,
        string name,
        string color) => new(
            Guid.NewGuid(), name, color, null, null, null, current.RowVersion);
}

public sealed class PharmacyBrandingSqlFixture : IAsyncLifetime
{
    private const string Prefix = "Pharmacy_BRAND01_Test_";
    private string serverConnection = string.Empty;
    private string databaseConnection = string.Empty;
    private string databaseName = string.Empty;

    public IDbContextFactory<PharmacyDbContext> Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("PHARMACY_TEST_SQLSERVER_CONNECTION est requise.");
        serverConnection = new SqlConnectionStringBuilder(raw)
        {
            InitialCatalog = "master",
            Pooling = false,
            ApplicationName = "Pharmacy.BRAND01.Tests"
        }.ConnectionString;
        databaseName = Prefix + Guid.NewGuid().ToString("N")[..12];
        ValidateDatabaseName(databaseName);
        await ExecuteServerAsync($"CREATE DATABASE [{databaseName}]; ALTER DATABASE [{databaseName}] SET RECOVERY FULL;");
        databaseConnection = new SqlConnectionStringBuilder(serverConnection)
        {
            InitialCatalog = databaseName,
            Pooling = false
        }.ConnectionString;
        var optionsBuilder = new DbContextOptionsBuilder<PharmacyDbContext>();
        optionsBuilder.UsePharmacySqlServer(databaseConnection);
        Factory = new FactoryImpl(optionsBuilder.Options);
        await using var context = await Factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();
        Assert.Equal(12, (await context.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            ValidateDatabaseName(databaseName);
            await ExecuteServerAsync($"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END;");
        }
        SqlConnection.ClearAllPools();
    }

    public ServiceProvider CreateProvider(bool authorized)
    {
        var services = new ServiceCollection();
        services.AddPharmacySqlServerPersistence(databaseConnection);
        services.AddLocalSqlSecurity();
        services.AddSqlPharmacyBranding();
        services.AddOperationalServices();
        var provider = services.BuildServiceProvider();
        if (authorized)
        {
            var now = DateTime.UtcNow;
            provider.GetRequiredService<ICurrentSecuritySession>().Establish(new UserSessionView(
                1701,
                "owner.brand01",
                "Propriétaire BRAND-01",
                ["OWNER"],
                new HashSet<string>([
                    SecurityPermissions.SecurityManageUsers,
                    SecurityPermissions.BackupCreate,
                    SecurityPermissions.BackupRestore
                ], StringComparer.Ordinal),
                now,
                now), now);
        }
        return provider;
    }

    public async Task ResetAsync()
    {
        await ExecuteAsync("""
            UPDATE configuration.PharmacyBranding
            SET PharmacyName=N'Pharmacie Diar Douha', PrimaryColor='#0D9A48',
                LogoContent=NULL, LogoMimeType=NULL, LogoFileName=NULL, UpdatedAtUtc=SYSUTCDATETIME()
            WHERE Id=1;
            DELETE FROM audit.AuditEventDetails WHERE AuditEventId IN (SELECT Id FROM audit.AuditEvents WHERE Module=N'branding');
            DELETE FROM audit.AuditEvents WHERE Module=N'branding';
            """);
    }

    public byte[] ReadBundledLogo()
    {
        var root = FindRepositoryRoot();
        return File.ReadAllBytes(Path.Combine(root, "src", "Pharmacy.Desktop", "wwwroot", "branding", "pharmacie-diar-douha-logo.png"));
    }

    public async Task<(string Action, string Result, long? ActorUserId, string[] Fields, string[] Values)> ReadBrandingAuditAsync(Guid operationId)
    {
        await using var connection = new SqlConnection(databaseConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.Action,e.Result,e.ActorUserId,d.FieldName,COALESCE(d.OldValueRedacted,N''),COALESCE(d.NewValueRedacted,N'')
            FROM audit.AuditEvents e
            LEFT JOIN audit.AuditEventDetails d ON d.AuditEventId=e.Id
            WHERE e.OperationId=@operationId
            ORDER BY d.FieldName;
            """;
        command.Parameters.AddWithValue("@operationId", operationId);
        await using var reader = await command.ExecuteReaderAsync();
        string? action = null, result = null;
        long? actor = null;
        var fields = new List<string>();
        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            action ??= reader.GetString(0);
            result ??= reader.GetString(1);
            actor ??= reader.IsDBNull(2) ? null : reader.GetInt64(2);
            if (!reader.IsDBNull(3)) fields.Add(reader.GetString(3));
            values.Add(reader.GetString(4));
            values.Add(reader.GetString(5));
        }
        return (action ?? string.Empty, result ?? string.Empty, actor, fields.ToArray(), values.ToArray());
    }

    public Task<int> RestoreDatabaseCountAsync() => ScalarAsync<int>(
        serverConnection,
        "SELECT COUNT(*) FROM sys.databases WHERE name LIKE N'Pharmacy_RestoreTest_%';");

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(databaseConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task ExecuteServerAsync(string sql)
    {
        await using var connection = new SqlConnection(serverConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        Assert.NotNull(value);
        return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void ValidateDatabaseName(string value)
    {
        if (!value.StartsWith(Prefix, StringComparison.Ordinal)
            || value.Length != Prefix.Length + 12
            || value[Prefix.Length..].Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidOperationException("Nom de base BRAND-01 non autorisé.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Racine du dépôt introuvable.");
    }

    private sealed class FactoryImpl(DbContextOptions<PharmacyDbContext> options)
        : IDbContextFactory<PharmacyDbContext>
    {
        public PharmacyDbContext CreateDbContext() => new(options);
        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
