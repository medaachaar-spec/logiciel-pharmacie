using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Security;
using Pharmacy.Infrastructure.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class SqlSecurityIntegrationTests
{
    private const string Prefix = "Pharmacy_Sprint15_Test_";
    private const string OwnerPassword = "Owner-Strong-Password-2026!";
    private const string NewPassword = "Owner-New-Password-2026!";

    [Fact]
    public async Task RealSqlSecurityValidatesBootstrapHashLockoutRbacRecoveryAndAudit()
    {
        var token = TestContext.Current.CancellationToken;
        var server = BuildServerConnection();
        var databaseName = Prefix + Guid.NewGuid().ToString("N")[..12];
        Assert.Matches(new Regex($"^{Prefix}[0-9a-f]{{12}}$", RegexOptions.CultureInvariant), databaseName);
        var database = new SqlConnectionStringBuilder(server)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Pharmacy.Sprint15.Tests",
            Pooling = false
        };
        await CreateDatabaseAsync(server, databaseName);

        try
        {
            var factory = new Factory(database.ConnectionString);
            await using (var context = await factory.CreateDbContextAsync(token))
            {
                await context.Database.MigrateAsync(token);
                Assert.Empty(await context.Database.GetPendingMigrationsAsync(token));
                Assert.Contains(await context.Database.GetAppliedMigrationsAsync(token), item =>
                    item.EndsWith("_SecurityRbacAuditPersistenceV01", StringComparison.Ordinal));
            }

            Assert.Equal(7, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'security';"));
            Assert.Equal(2, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'audit';"));
            Assert.Equal(20, await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM security.Permissions;"));
            Assert.Equal(0, await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM security.UserAccounts;"));

            var clock = new MutableClock(new DateTime(2026, 8, 10, 16, 0, 0, DateTimeKind.Utc));
            var session = new CurrentSecuritySession();
            var generator = new FixedRecoveryGenerator();
            var hasher = new PasswordHasher<UserAccountEntity>(Options.Create(new PasswordHasherOptions
            {
                CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
                IterationCount = 210_000
            }));
            var service = new SqlAuthenticationService(factory, hasher, generator, session, clock);

            var bootstrap = await service.BootstrapOwnerAsync(
                new BootstrapOwnerRequest(Guid.NewGuid(), "owner.local", "Propriétaire local", OwnerPassword), token);
            Assert.True(bootstrap.IsSuccess, bootstrap.IsFailure ? bootstrap.Error.Code : null);
            Assert.Equal(8, bootstrap.Value.Codes.Count);
            Assert.Equal("security.bootstrap.already_completed", (await service.BootstrapOwnerAsync(
                new BootstrapOwnerRequest(Guid.NewGuid(), "second", "Second", OwnerPassword), token)).Error.Code);

            Assert.Equal(0, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM security.UserAccounts WHERE PasswordHash=@plain OR PasswordHash LIKE N'%' + @plain + N'%';",
                new SqlParameter("@plain", OwnerPassword)));
            Assert.Equal(0, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM security.RecoveryCodes WHERE CodeHash=@plain;",
                new SqlParameter("@plain", bootstrap.Value.Codes[0])));

            for (var attempt = 1; attempt <= 5; attempt++)
            {
                var failed = await service.LoginAsync(new LoginRequest("owner.local", "wrong-password-value", Guid.NewGuid()), token);
                Assert.True(failed.IsFailure);
                Assert.Equal(attempt == 5 ? "security.authentication.locked" : "security.authentication.invalid_credentials", failed.Error.Code);
            }
            Assert.Equal("security.authentication.locked", (await service.LoginAsync(
                new LoginRequest("owner.local", OwnerPassword, Guid.NewGuid()), token)).Error.Code);

            clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));
            var login = await service.LoginAsync(new LoginRequest("owner.local", OwnerPassword, Guid.NewGuid()), token);
            Assert.True(login.IsSuccess, login.IsFailure ? login.Error.Code : null);
            Assert.Contains(SecurityPermissions.SecurityManageUsers, login.Value.Session.Permissions);
            var authorizer = new PermissionAuthorizer(session, clock);
            Assert.True(authorizer.Authorize(SecurityPermissions.InventoryFinalizeCount, true).IsSuccess);
            Assert.Equal("security.authorization.denied", authorizer.Authorize("not.granted").Error.Code);

            await service.LogoutAsync(Guid.NewGuid(), token);
            var recovered = await service.RecoverPasswordAsync(
                "owner.local", bootstrap.Value.Codes[0], NewPassword, Guid.NewGuid(), token);
            Assert.True(recovered.IsSuccess, recovered.IsFailure ? recovered.Error.Code : null);
            Assert.Equal("security.recovery.invalid", (await service.RecoverPasswordAsync(
                "owner.local", bootstrap.Value.Codes[0], OwnerPassword, Guid.NewGuid(), token)).Error.Code);
            Assert.True((await service.LoginAsync(new LoginRequest("owner.local", NewPassword, Guid.NewGuid()), token)).IsSuccess);

            Assert.True(await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM security.AuthenticationEvents;") >= 9);
            Assert.True(await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM audit.AuditEvents;") >= 4);
            Assert.Equal(0, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM audit.AuditEvents WHERE ReasonCode LIKE N'%password%' OR ActorDisplayName LIKE N'%Password%';"));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(server, databaseName);
        }
    }

    private static string BuildServerConnection()
    {
        var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException("PHARMACY_TEST_SQLSERVER_CONNECTION est requise.");
        return new SqlConnectionStringBuilder(raw) { InitialCatalog = "master", Pooling = false }.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string server, string name)
    {
        await using var connection = new SqlConnection(server);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{name}];";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string server, string name)
    {
        Assert.StartsWith(Prefix, name, StringComparison.Ordinal);
        await using var connection = new SqlConnection(server);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END;";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync();
        Assert.NotNull(value);
        return (T)Convert.ChangeType(value, typeof(T));
    }

    private sealed class Factory : IDbContextFactory<PharmacyDbContext>
    {
        private readonly DbContextOptions<PharmacyDbContext> options;
        public Factory(string connectionString)
        {
            var builder = new DbContextOptionsBuilder<PharmacyDbContext>();
            builder.UsePharmacySqlServer(connectionString);
            options = builder.Options;
        }
        public PharmacyDbContext CreateDbContext() => new(options);
        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class MutableClock(DateTime now) : ISecurityClock
    {
        public DateTime UtcNow { get; private set; } = now;
        public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
    }

    private sealed class FixedRecoveryGenerator : IRecoveryCodeGenerator
    {
        private int generation;
        public IReadOnlyList<string> Generate(int count)
        {
            generation++;
            return Enumerable.Range(1, count).Select(index => $"S15{generation:00}-CODE-{index:0000}").ToArray();
        }
    }
}
