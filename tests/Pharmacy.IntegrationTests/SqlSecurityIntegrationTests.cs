using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Application.Reporting.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Security;
using Pharmacy.Infrastructure.Reporting;
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

            Assert.Equal(9, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'security';"));
            Assert.Equal(2, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'audit';"));
            Assert.Equal(30, await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM security.Permissions;"));
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

            var initialState = await service.HasUsersAsync(token);
            Assert.True(initialState.IsSuccess);
            Assert.False(initialState.Value);
            var shortPassword = await service.BootstrapOwnerAsync(
                new BootstrapOwnerRequest(Guid.NewGuid(), "owner.local", "Propriétaire local", "too-short"), token);
            Assert.Equal("security.password.policy_failed", shortPassword.Error.Code);
            Assert.Equal(0, await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM security.UserAccounts;"));

            var concurrent = await Task.WhenAll(
                service.BootstrapOwnerAsync(
                    new BootstrapOwnerRequest(Guid.NewGuid(), "owner.local", "Propriétaire local", OwnerPassword), token),
                service.BootstrapOwnerAsync(
                    new BootstrapOwnerRequest(Guid.NewGuid(), "owner.local", "Propriétaire local", OwnerPassword), token));
            var bootstrap = Assert.Single(concurrent, result => result.IsSuccess);
            var refusedConcurrent = Assert.Single(concurrent, result => result.IsFailure);
            Assert.Equal("security.bootstrap.already_completed", refusedConcurrent.Error.Code);
            Assert.True(bootstrap.IsSuccess, bootstrap.IsFailure ? bootstrap.Error.Code : null);
            Assert.Equal(8, bootstrap.Value.Codes.Count);
            Assert.Equal("security.bootstrap.already_completed", (await service.BootstrapOwnerAsync(
                new BootstrapOwnerRequest(Guid.NewGuid(), "second", "Second", OwnerPassword), token)).Error.Code);
            Assert.Equal(1, await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM security.UserAccounts;"));
            Assert.Equal(1, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM security.UserRoles ur JOIN security.Roles r ON r.Id=ur.RoleId WHERE r.Code=N'OWNER';"));
            Assert.Equal(30, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM security.UserRoles ur JOIN security.RolePermissions rp ON rp.RoleId=ur.RoleId WHERE ur.UserAccountId=1;"));
            var restartedState = await service.HasUsersAsync(token);
            Assert.True(restartedState.IsSuccess);
            Assert.True(restartedState.Value);

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

    [Fact]
    public async Task RealSqlRoleManagementPersistsPermissionsProtectsOwnerAuditsAndDeniesLimitedUser()
    {
        var token = TestContext.Current.CancellationToken;
        var server = BuildServerConnection();
        var databaseName = Prefix + Guid.NewGuid().ToString("N")[..12];
        Assert.Matches(new Regex($"^{Prefix}[0-9a-f]{{12}}$", RegexOptions.CultureInvariant), databaseName);
        var database = new SqlConnectionStringBuilder(server)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Pharmacy.Sec04.Tests",
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
            }

            var clock = new MutableClock(new DateTime(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc));
            var session = new CurrentSecuritySession();
            var generator = new FixedRecoveryGenerator();
            var hasher = new PasswordHasher<UserAccountEntity>(Options.Create(new PasswordHasherOptions
            {
                CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
                IterationCount = 210_000
            }));
            var authentication = new SqlAuthenticationService(factory, hasher, generator, session, clock);
            var bootstrap = await authentication.BootstrapOwnerAsync(
                new BootstrapOwnerRequest(Guid.NewGuid(), "owner.sec04", "Owner SEC-04", OwnerPassword), token);
            Assert.True(bootstrap.IsSuccess, bootstrap.IsFailure ? bootstrap.Error.Code : null);
            var ownerLogin = await authentication.LoginAsync(
                new LoginRequest("owner.sec04", OwnerPassword, Guid.NewGuid()), token);
            Assert.True(ownerLogin.IsSuccess, ownerLogin.IsFailure ? ownerLogin.Error.Code : null);

            var authorizer = new PermissionAuthorizer(session, clock);
            var administration = new SqlSecurityAdministrationService(
                factory, hasher, generator, authorizer, session, clock);
            var permissionResult = await administration.GetPermissionsAsync(token);
            Assert.True(permissionResult.IsSuccess, permissionResult.IsFailure ? permissionResult.Error.Code : null);
            Assert.Equal(30, permissionResult.Value.Count);
            var recipeCodes = new[]
            {
                SecurityPermissions.SalesCreate,
                SecurityPermissions.CatalogView,
                SecurityPermissions.InventoryView
            };
            var recipePermissionIds = permissionResult.Value
                .Where(permission => recipeCodes.Contains(permission.Code, StringComparer.Ordinal))
                .Select(permission => permission.PermissionId)
                .ToArray();
            Assert.Equal(3, recipePermissionIds.Length);

            var created = await administration.CreateRoleAsync(new CreateSecurityRoleRequest(
                Guid.NewGuid(), " role-qa-limite ", " QA limité ", recipePermissionIds), token);
            Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
            Assert.Equal("ROLE-QA-LIMITE", created.Value.Code);
            Assert.Equal("QA limité", created.Value.Name);
            Assert.False(created.Value.IsSystemRole);
            Assert.Equal(recipeCodes.Order(StringComparer.Ordinal), created.Value.Permissions.Select(permission => permission.Code));

            var duplicate = await administration.CreateRoleAsync(new CreateSecurityRoleRequest(
                Guid.NewGuid(), "ROLE-QA-LIMITE", "Doublon", Array.Empty<long>()), token);
            Assert.Equal("security.role.code_exists", duplicate.Error.Code);
            Assert.Equal(1, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM security.Roles WHERE Code=N'ROLE-QA-LIMITE';"));

            var cashViewId = permissionResult.Value.Single(permission => permission.Code == SecurityPermissions.CashView).PermissionId;
            var withoutCatalog = recipePermissionIds
                .Where(id => id != permissionResult.Value.Single(permission => permission.Code == SecurityPermissions.CatalogView).PermissionId)
                .Append(cashViewId)
                .ToArray();
            var firstUpdate = await administration.UpdateRoleAsync(new UpdateSecurityRoleRequest(
                Guid.NewGuid(), created.Value.RoleId, "QA limité modifié", withoutCatalog, created.Value.RowVersion), token);
            Assert.True(firstUpdate.IsSuccess, firstUpdate.IsFailure ? firstUpdate.Error.Code : null);
            Assert.Contains(SecurityPermissions.CashView, firstUpdate.Value.Permissions.Select(permission => permission.Code));
            Assert.DoesNotContain(SecurityPermissions.CatalogView, firstUpdate.Value.Permissions.Select(permission => permission.Code));

            var restored = await administration.UpdateRoleAsync(new UpdateSecurityRoleRequest(
                Guid.NewGuid(), firstUpdate.Value.RoleId, "QA limité", recipePermissionIds, firstUpdate.Value.RowVersion), token);
            Assert.True(restored.IsSuccess, restored.IsFailure ? restored.Error.Code : null);
            Assert.Equal(recipeCodes.Order(StringComparer.Ordinal), restored.Value.Permissions.Select(permission => permission.Code));

            var staleUpdate = await administration.UpdateRoleAsync(new UpdateSecurityRoleRequest(
                Guid.NewGuid(), created.Value.RoleId, "Écriture périmée", recipePermissionIds, created.Value.RowVersion), token);
            Assert.Equal("security.concurrency.conflict", staleUpdate.Error.Code);

            var roles = await administration.GetRolesAsync(token);
            Assert.True(roles.IsSuccess, roles.IsFailure ? roles.Error.Code : null);
            var persistedRole = roles.Value.Single(role => role.Code == "ROLE-QA-LIMITE");
            Assert.Equal("QA limité", persistedRole.Name);
            Assert.Equal(recipeCodes.Order(StringComparer.Ordinal), persistedRole.Permissions.Select(permission => permission.Code));
            var ownerRole = roles.Value.Single(role => role.Code == "OWNER");
            Assert.True(ownerRole.IsSystemRole);
            Assert.Equal(30, ownerRole.Permissions.Count);
            var ownerUpdate = await administration.UpdateRoleAsync(new UpdateSecurityRoleRequest(
                Guid.NewGuid(), ownerRole.RoleId, "Owner modifié", Array.Empty<long>(), ownerRole.RowVersion), token);
            Assert.Equal("security.role.system_protected", ownerUpdate.Error.Code);
            Assert.Equal(30, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM security.RolePermissions rp JOIN security.Roles r ON r.Id=rp.RoleId WHERE r.Code=N'OWNER';"));
            Assert.Equal("Propriétaire", await ScalarAsync<string>(database.ConnectionString,
                "SELECT Name FROM security.Roles WHERE Code=N'OWNER';"));

            var limitedUser = await administration.CreateUserAsync(new CreateSecurityUserRequest(
                Guid.NewGuid(), "UTIL-QA-LIMITE", "Utilisateur QA limité", NewPassword,
                new[] { restored.Value.RoleId }), token);
            Assert.True(limitedUser.IsSuccess, limitedUser.IsFailure ? limitedUser.Error.Code : null);
            Assert.Equal(1, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM security.UserRoles ur JOIN security.Roles r ON r.Id=ur.RoleId JOIN security.UserAccounts u ON u.Id=ur.UserAccountId WHERE r.Code=N'ROLE-QA-LIMITE' AND u.NormalizedLogin=N'UTIL-QA-LIMITE';"));

            Assert.Equal(1, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM audit.AuditEvents WHERE EntityType=N'Role' AND Action=N'role.create' AND Result=N'Success' AND ActorUserId IS NOT NULL;"));
            Assert.Equal(2, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM audit.AuditEvents WHERE EntityType=N'Role' AND Action=N'role.permissions.change' AND Result=N'Success';"));
            Assert.True(await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM audit.AuditEventDetails d JOIN audit.AuditEvents e ON e.Id=d.AuditEventId WHERE e.EntityType=N'Role' AND d.FieldName IN (N'PermissionsAdded',N'PermissionsRemoved');") >= 5);

            await authentication.LogoutAsync(Guid.NewGuid(), token);
            var limitedLogin = await authentication.LoginAsync(
                new LoginRequest("UTIL-QA-LIMITE", NewPassword, Guid.NewGuid()), token);
            Assert.True(limitedLogin.IsSuccess, limitedLogin.IsFailure ? limitedLogin.Error.Code : null);
            Assert.Equal(recipeCodes.Order(StringComparer.Ordinal), limitedLogin.Value.Session.Permissions.Order(StringComparer.Ordinal));
            Assert.Equal("security.authorization.denied", authorizer.Authorize(SecurityPermissions.InventoryAdjust).Error.Code);
            Assert.Equal("security.authorization.denied", authorizer.Authorize(SecurityPermissions.InventoryFinalizeCount, true).Error.Code);
            Assert.Equal("security.authorization.denied", authorizer.Authorize(SecurityPermissions.ReportsView).Error.Code);
            Assert.Equal("security.authorization.denied", authorizer.Authorize(SecurityPermissions.SecurityManageUsers).Error.Code);
            Assert.Equal("security.authorization.denied", authorizer.Authorize(SecurityPermissions.SecurityManageRoles).Error.Code);

            var deniedRoleCreate = await administration.CreateRoleAsync(new CreateSecurityRoleRequest(
                Guid.NewGuid(), "ROLE-INTERDIT", "Interdit", Array.Empty<long>()), token);
            Assert.Equal("security.authorization.denied", deniedRoleCreate.Error.Code);
            var reporting = new SqlReportingService(factory, authorizer, new NoOpAuditRecorder(), clock);
            var deniedReporting = await reporting.GetSnapshotAsync(new ReportingQuery(
                new ReportingPeriod(clock.UtcNow.AddDays(-1), clock.UtcNow), 10), token);
            Assert.Equal("security.authorization.denied", deniedReporting.Error.Code);
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
            var currentGeneration = Interlocked.Increment(ref generation);
            return Enumerable.Range(1, count).Select(index => $"S15{currentGeneration:00}-CODE-{index:0000}").ToArray();
        }
    }

    private sealed class NoOpAuditRecorder : IBusinessAuditRecorder
    {
        public Task RecordSuccessAsync(string module, string entityType, string? entityId, string action, Guid? operationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
