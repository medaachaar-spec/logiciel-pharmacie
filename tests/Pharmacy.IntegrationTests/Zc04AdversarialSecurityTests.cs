using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Infrastructure.Security;
using Pharmacy.Infrastructure.Persistence;
using Xunit;

namespace Pharmacy.IntegrationTests;

/// <summary>
/// ZCODE-04A security family: ZC-001 self-permission escalation (ZC04-A01),
/// ZC-002A disabled live session (ZC04-A02) and ZC-013 password behavior
/// (ZC04-A07A / ZC04-A07B) against a real SQL Server database, real
/// authentication/administration services and real persisted-state assertions.
/// </summary>
[Collection("Sprint 09 persistence serial")]
public sealed class Zc04AdversarialSecurityTests
{
    private const string OwnerPassword = "Zc04-Owner-Password-2026!";
    private const string ManagerPassword = "Zc04-Manager-Pass-2026!";
    private const string SellerPassword = "Zc04-Seller-Pass-2026!";

    [Fact]
    public async Task ZC04A01_NonOwnerAdministratorMustNotEscalateOwnPermissionsViaSelfTargetedOverrides()
    {
        var token = TestContext.Current.CancellationToken;
        var server = Zc04.BuildServerConnection();
        var databaseName = Zc04.NewDatabaseName();
        var databaseConnection = Zc04.BuildDatabaseConnection(server.ConnectionString, databaseName);
        await Zc04.CreateDatabaseAsync(server.ConnectionString, databaseName);

        try
        {
            await Zc04.MigrateAsync(databaseConnection);
            var factory = new Zc04.Factory(databaseConnection);
            var clock = new Zc04.Zc04Clock(new DateTime(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc));
            var owner = await Zc04.BootstrapOwnerAsync(factory, clock, "zc04.owner", OwnerPassword);

            // Build a MANAGER-equivalent role: every permission except backup.restore
            // (exactly SecurityRolePresets.ManagerPermissions).
            var permissions = owner.Administration.GetPermissionsAsync(token);
            var permissionViews = (await permissions).Value;
            var managerPermissionIds = permissionViews
                .Where(permission => SecurityRolePresets.ManagerPermissions.Contains(permission.Code, StringComparer.Ordinal))
                .Select(permission => permission.PermissionId)
                .ToArray();
            Assert.Equal(SecurityRolePresets.ManagerPermissions.Count, managerPermissionIds.Length);
            var backupRestoreId = permissionViews
                .Single(permission => permission.Code == SecurityPermissions.BackupRestore).PermissionId;

            var managerRole = await owner.Administration.CreateRoleAsync(new CreateSecurityRoleRequest(
                Guid.NewGuid(), "ZC04-MANAGER", "Manager ZC04", managerPermissionIds), token);
            Assert.True(managerRole.IsSuccess, managerRole.IsFailure ? managerRole.Error.Code : null);
            var createdManager = await owner.Administration.CreateUserAsync(new CreateSecurityUserRequest(
                Guid.NewGuid(), "ZC04MGR", "Manager ZC04", ManagerPassword,
                new[] { managerRole.Value.RoleId }), token);
            Assert.True(createdManager.IsSuccess, createdManager.IsFailure ? createdManager.Error.Code : null);

            // Manager authenticates in their own isolated session.
            var managerSession = new CurrentSecuritySession();
            var hasher = Zc04.CreateHasher();
            var managerAuthentication = new SqlAuthenticationService(
                factory, hasher, new Zc04.SequentialRecoveryGenerator(), managerSession, clock);
            var managerLogin = await managerAuthentication.LoginAsync(
                new LoginRequest("ZC04MGR", ManagerPassword, Guid.NewGuid()), token);
            Assert.True(managerLogin.IsSuccess, managerLogin.IsFailure ? managerLogin.Error.Code : null);
            Assert.DoesNotContain(SecurityPermissions.BackupRestore, managerLogin.Value.Session.Permissions);

            var managerAuthorizer = new PermissionAuthorizer(
                managerSession,
                clock,
                new SqlSecuritySessionValidator(factory));
            Assert.Equal("security.authorization.denied",
                managerAuthorizer.Authorize(SecurityPermissions.BackupRestore).Error.Code);
            Assert.True(managerAuthorizer.Authorize(SecurityPermissions.SecurityManageRoles).IsSuccess);

            // Satisfy the recent-reauthentication requirement legitimately.
            var reauthenticated = await managerAuthentication.ReauthenticateAsync(
                ManagerPassword, Guid.NewGuid(), token);
            Assert.True(reauthenticated.IsSuccess, reauthenticated.IsFailure ? reauthenticated.Error.Code : null);

            // Manager administers security from their own session and targets THEIR OWN account.
            var managerAdministration = new SqlSecurityAdministrationService(
                factory, hasher, new Zc04.SequentialRecoveryGenerator(), managerAuthorizer, managerSession, clock);
            var users = await managerAdministration.GetUsersAsync(token);
            Assert.True(users.IsSuccess, users.IsFailure ? users.Error.Code : null);
            var self = users.Value.Single(user => user.Login == "ZC04MGR");
            Assert.Equal(managerSession.User!.UserId, self.UserId);
            Assert.DoesNotContain(SecurityPermissions.BackupRestore, self.EffectivePermissions);

            var selfGrant = await managerAdministration.SetUserPermissionOverridesAsync(
                new SetUserPermissionOverridesRequest(
                    Guid.NewGuid(),
                    self.UserId,
                    new[] { new SecurityPermissionOverrideSelection(backupRestoreId, SecurityPermissionOverrideEffect.Allow) },
                    self.RowVersion),
                token);

            // INVARIANT under test: a non-OWNER administrator must not be able to increase
            // their own privilege set through self-targeted permission overrides.
            // If the self-grant succeeded and became effective, this test is RED on purpose:
            // the failing test is the requested artifact (ZCODE-01 / ZC-001).
            if (selfGrant.IsSuccess)
            {
                Assert.Fail(
                    "ZC-001 REPRODUCED: self-targeted Allow override for backup.restore was accepted " +
                    $"(result permissions contain backup.restore = {selfGrant.Value.EffectivePermissions.Contains(SecurityPermissions.BackupRestore)}).");
            }

            Assert.Equal("security.authorization.denied", selfGrant.Error.Code);

            // Persisted state must remain unchanged.
            Assert.Equal(0, await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM security.UserPermissionOverrides;"));
            Assert.Equal(0, await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM audit.AuditEvents WHERE Action=N'user.permission_overrides.set';"));

            // Control: the same manager administration legitimately changing ANOTHER user's
            // overrides is still subject to existing rules and succeeds for a third party.
            var createdSeller = await owner.Administration.CreateUserAsync(new CreateSecurityUserRequest(
                Guid.NewGuid(), "ZC04SELL", "Vendeur ZC04", SellerPassword,
                new[] { managerRole.Value.RoleId }), token);
            Assert.True(createdSeller.IsSuccess, createdSeller.IsFailure ? createdSeller.Error.Code : null);
            var otherGrant = await managerAdministration.SetUserPermissionOverridesAsync(
                new SetUserPermissionOverridesRequest(
                    Guid.NewGuid(),
                    createdSeller.Value.UserId,
                    new[] { new SecurityPermissionOverrideSelection(backupRestoreId, SecurityPermissionOverrideEffect.Deny) },
                    createdSeller.Value.RowVersion),
                token);
            Assert.True(otherGrant.IsSuccess, otherGrant.IsFailure ? otherGrant.Error.Code : null);
            Assert.Contains(otherGrant.Value.PermissionOverrides,
                item => item.PermissionCode == SecurityPermissions.BackupRestore && item.Effect == SecurityPermissionOverrideEffect.Deny);
            Assert.Equal(1, await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM security.UserPermissionOverrides upo JOIN security.Permissions p ON p.Id=upo.PermissionId WHERE upo.UserAccountId=@userId AND p.Code=N'backup.restore' AND upo.Effect=N'Deny';",
                new SqlParameter("@userId", SqlDbType.BigInt) { Value = createdSeller.Value.UserId }));
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task ZC04A01B_SelfEscalatedOverrideBecomesEffectiveAtNextLoginWhenSelfGrantIsAccepted()
    {
        // Companion probe for ZC04-A01: the rejected self-grant must leave no persisted
        // override capable of widening the manager's permissions at the next login.
        var token = TestContext.Current.CancellationToken;
        var server = Zc04.BuildServerConnection();
        var databaseName = Zc04.NewDatabaseName();
        var databaseConnection = Zc04.BuildDatabaseConnection(server.ConnectionString, databaseName);
        await Zc04.CreateDatabaseAsync(server.ConnectionString, databaseName);

        try
        {
            await Zc04.MigrateAsync(databaseConnection);
            var factory = new Zc04.Factory(databaseConnection);
            var clock = new Zc04.Zc04Clock(new DateTime(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc));
            var owner = await Zc04.BootstrapOwnerAsync(factory, clock, "zc04.owner.b", OwnerPassword);

            var permissionViews = (await owner.Administration.GetPermissionsAsync(token)).Value;
            var managerPermissionIds = permissionViews
                .Where(permission => SecurityRolePresets.ManagerPermissions.Contains(permission.Code, StringComparer.Ordinal))
                .Select(permission => permission.PermissionId)
                .ToArray();
            var backupRestoreId = permissionViews
                .Single(permission => permission.Code == SecurityPermissions.BackupRestore).PermissionId;
            var managerRole = await owner.Administration.CreateRoleAsync(new CreateSecurityRoleRequest(
                Guid.NewGuid(), "ZC04-MGR-B", "Manager B", managerPermissionIds), token);
            Assert.True(managerRole.IsSuccess, managerRole.IsFailure ? managerRole.Error.Code : null);
            var createdManager = await owner.Administration.CreateUserAsync(new CreateSecurityUserRequest(
                Guid.NewGuid(), "ZC04MGRB", "Manager B", ManagerPassword,
                new[] { managerRole.Value.RoleId }), token);
            Assert.True(createdManager.IsSuccess, createdManager.IsFailure ? createdManager.Error.Code : null);

            var managerSession = new CurrentSecuritySession();
            var hasher = Zc04.CreateHasher();
            var managerAuthentication = new SqlAuthenticationService(
                factory, hasher, new Zc04.SequentialRecoveryGenerator(), managerSession, clock);
            var login = await managerAuthentication.LoginAsync(
                new LoginRequest("ZC04MGRB", ManagerPassword, Guid.NewGuid()), token);
            Assert.True(login.IsSuccess, login.IsFailure ? login.Error.Code : null);
            var reauthenticated = await managerAuthentication.ReauthenticateAsync(
                ManagerPassword, Guid.NewGuid(), token);
            Assert.True(reauthenticated.IsSuccess, reauthenticated.IsFailure ? reauthenticated.Error.Code : null);

            var managerAuthorizer = new PermissionAuthorizer(
                managerSession,
                clock,
                new SqlSecuritySessionValidator(factory));
            var managerAdministration = new SqlSecurityAdministrationService(
                factory, hasher, new Zc04.SequentialRecoveryGenerator(), managerAuthorizer, managerSession, clock);
            var self = (await managerAdministration.GetUsersAsync(token)).Value
                .Single(user => user.UserId == createdManager.Value.UserId);
            var selfGrant = await managerAdministration.SetUserPermissionOverridesAsync(
                new SetUserPermissionOverridesRequest(
                    Guid.NewGuid(),
                    self.UserId,
                    [new SecurityPermissionOverrideSelection(
                        backupRestoreId,
                        SecurityPermissionOverrideEffect.Allow)],
                    self.RowVersion),
                token);
            Assert.True(selfGrant.IsFailure, "The self-targeted grant must be rejected before persistence.");
            Assert.Equal("security.authorization.denied", selfGrant.Error.Code);
            Assert.Equal(0, await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM security.UserPermissionOverrides WHERE UserAccountId=@userId;",
                new SqlParameter("@userId", SqlDbType.BigInt) { Value = createdManager.Value.UserId }));

            managerSession.Clear();
            var relogin = await managerAuthentication.LoginAsync(
                new LoginRequest("ZC04MGRB", ManagerPassword, Guid.NewGuid()), token);
            Assert.True(relogin.IsSuccess, relogin.IsFailure ? relogin.Error.Code : null);

            // No rejected self-grant can become effective after a new authentication.
            Assert.DoesNotContain(SecurityPermissions.BackupRestore, relogin.Value.Session.Permissions);
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task ZC04A02_DisabledLiveSessionMustNotPerformProtectedBusinessOperations()
    {
        var token = TestContext.Current.CancellationToken;
        var server = Zc04.BuildServerConnection();
        var databaseName = Zc04.NewDatabaseName();
        var databaseConnection = Zc04.BuildDatabaseConnection(server.ConnectionString, databaseName);
        await Zc04.CreateDatabaseAsync(server.ConnectionString, databaseName);

        try
        {
            await Zc04.MigrateAsync(databaseConnection);
            var factory = new Zc04.Factory(databaseConnection);
            var clock = new Zc04.Zc04Clock(new DateTime(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc));
            var owner = await Zc04.BootstrapOwnerAsync(factory, clock, "zc04.owner.2", OwnerPassword);

            // Seller with the exact SELLER preset (sales.create included, no admin rights).
            var permissionViews = (await owner.Administration.GetPermissionsAsync(token)).Value;
            var sellerPermissionIds = permissionViews
                .Where(permission => SecurityRolePresets.SellerPermissions.Contains(permission.Code, StringComparer.Ordinal))
                .Select(permission => permission.PermissionId)
                .ToArray();
            Assert.Equal(SecurityRolePresets.SellerPermissions.Count, sellerPermissionIds.Length);
            var sellerRole = await owner.Administration.CreateRoleAsync(new CreateSecurityRoleRequest(
                Guid.NewGuid(), "ZC04-SELLER", "Vendeur ZC04", sellerPermissionIds), token);
            Assert.True(sellerRole.IsSuccess, sellerRole.IsFailure ? sellerRole.Error.Code : null);
            var createdSeller = await owner.Administration.CreateUserAsync(new CreateSecurityUserRequest(
                Guid.NewGuid(), "ZC04SELL2", "Vendeur 2", SellerPassword,
                new[] { sellerRole.Value.RoleId }), token);
            Assert.True(createdSeller.IsSuccess, createdSeller.IsFailure ? createdSeller.Error.Code : null);

            // Seller opens a session (Session A) with sales.create.
            var sellerSession = new CurrentSecuritySession();
            var sellerAuthentication = new SqlAuthenticationService(
                factory, Zc04.CreateHasher(), new Zc04.SequentialRecoveryGenerator(), sellerSession, clock);
            var sellerLogin = await sellerAuthentication.LoginAsync(
                new LoginRequest("ZC04SELL2", SellerPassword, Guid.NewGuid()), token);
            Assert.True(sellerLogin.IsSuccess, sellerLogin.IsFailure ? sellerLogin.Error.Code : null);
            Assert.Contains(SecurityPermissions.SalesCreate, sellerSession.User!.Permissions);

            // Administration (independent owner context) disables the seller account.
            // Refresh the rowversion first: the seller login mutated the user row.
            var sellerBeforeDisable = (await owner.Administration.GetUsersAsync(token)).Value
                .Single(user => user.UserId == createdSeller.Value.UserId);
            var disabled = await owner.Administration.SetUserActiveAsync(new SetUserActiveRequest(
                Guid.NewGuid(), createdSeller.Value.UserId, false, sellerBeforeDisable.RowVersion), token);
            Assert.True(disabled.IsSuccess, disabled.IsFailure ? disabled.Error.Code : null);
            Assert.Equal(0, await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM security.UserAccounts WHERE Id=@userId AND IsActive=1;",
                new SqlParameter("@userId", SqlDbType.BigInt) { Value = createdSeller.Value.UserId }));

            // Disabled account -> NEW login attempt must be rejected.
            var freshSession = new CurrentSecuritySession();
            var freshAuthentication = new SqlAuthenticationService(
                factory, Zc04.CreateHasher(), new Zc04.SequentialRecoveryGenerator(), freshSession, clock);
            var newLogin = await freshAuthentication.LoginAsync(
                new LoginRequest("ZC04SELL2", SellerPassword, Guid.NewGuid()), token);
            Assert.True(newLogin.IsFailure, "New login for a disabled account must be rejected.");

            // WITHOUT re-authenticating Session A, perform a normal protected business
            // operation: a full real sale finalization (sales.create).
            var seeded = await SeededSaleFactory.SeedProductWithStockAsync(factory, clock, "A02", token: token);
            var finalization = await SeededSaleFactory.FinalizeSaleAsSessionAsync(
                factory, clock, sellerSession, seeded, 1, "A02", token);
            var salesCount = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM sales.Sales;");

            // INVARIANT under test: a user explicitly disabled by administration must not be
            // able to perform protected business activity with an existing open session.
            // RED = the disabled seller's session still finalized a sale (ZC-002A REPRODUCED).
            if (finalization.IsSuccess)
            {
                Assert.Fail(
                    "ZC-002A REPRODUCED: disabled seller session finalized a real sale " +
                    $"(sale reference {finalization.Value.Reference}; sales.Sales count {salesCount}).");
            }

            Assert.True(finalization.IsFailure, "Disabled session must not finalize sales.");
            Assert.Equal(0, salesCount);
            Assert.Equal(0, await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM sales.Payments;"));
            Assert.Equal(0, await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM inventory.StockMovements WHERE MovementType=N'Sale';"));
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task ZC04A07A_DisabledAccountSessionAttemptsPasswordChangeWithCorrectCurrentPassword()
    {
        var token = TestContext.Current.CancellationToken;
        var server = Zc04.BuildServerConnection();
        var databaseName = Zc04.NewDatabaseName();
        var databaseConnection = Zc04.BuildDatabaseConnection(server.ConnectionString, databaseName);
        await Zc04.CreateDatabaseAsync(server.ConnectionString, databaseName);

        try
        {
            await Zc04.MigrateAsync(databaseConnection);
            var factory = new Zc04.Factory(databaseConnection);
            var clock = new Zc04.Zc04Clock(new DateTime(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc));
            var owner = await Zc04.BootstrapOwnerAsync(factory, clock, "zc04.owner.7a", OwnerPassword);

            var createdSeller = await CreateUserWithSellerPresetAsync(owner.Administration, token);
            var sellerSession = new CurrentSecuritySession();
            var sellerAuthentication = new SqlAuthenticationService(
                factory, Zc04.CreateHasher(), new Zc04.SequentialRecoveryGenerator(), sellerSession, clock);
            Assert.True((await sellerAuthentication.LoginAsync(
                new LoginRequest(createdSeller.Login, SellerPassword, Guid.NewGuid()), token)).IsSuccess);

            // Refresh the rowversion: the seller login mutated the user row.
            var sellerBeforeDisable = (await owner.Administration.GetUsersAsync(token)).Value
                .Single(user => user.UserId == createdSeller.UserId);
            var disabled = await owner.Administration.SetUserActiveAsync(new SetUserActiveRequest(
                Guid.NewGuid(), createdSeller.UserId, false, sellerBeforeDisable.RowVersion), token);
            Assert.True(disabled.IsSuccess, disabled.IsFailure ? disabled.Error.Code : null);

            // Existing (still-open) session calls ChangePasswordAsync with the CORRECT current password.
            var change = await sellerAuthentication.ChangePasswordAsync(
                SellerPassword, "Zc04-New-Password-2026!", Guid.NewGuid(), token);

            var hashChanged = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM security.AuthenticationEvents WHERE EventType=N'PasswordChange';");

            // Behavior recorded per ZCODE-04A section 11: report exact behavior, no automatic
            // release-blocking classification. The invariant asserted here is the conservative
            // one: a disabled account must not be able to mutate its own credentials.
            // RED = the change was accepted for a disabled account.
            if (change.IsSuccess)
            {
                Assert.Fail(
                    "ZC-013(A) REPRODUCED: disabled account changed its own password through an open session " +
                    $"(PasswordChange events: {hashChanged}).");
            }

            Assert.True(change.IsFailure);
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task ZC04A07B_RepeatedWrongCurrentPasswordAttemptsMustBeMitigated()
    {
        var token = TestContext.Current.CancellationToken;
        var server = Zc04.BuildServerConnection();
        var databaseName = Zc04.NewDatabaseName();
        var databaseConnection = Zc04.BuildDatabaseConnection(server.ConnectionString, databaseName);
        await Zc04.CreateDatabaseAsync(server.ConnectionString, databaseName);

        try
        {
            await Zc04.MigrateAsync(databaseConnection);
            var factory = new Zc04.Factory(databaseConnection);
            var clock = new Zc04.Zc04Clock(new DateTime(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc));
            var owner = await Zc04.BootstrapOwnerAsync(factory, clock, "zc04.owner.7b", OwnerPassword);

            var createdSeller = await CreateUserWithSellerPresetAsync(owner.Administration, token);
            var sellerSession = new CurrentSecuritySession();
            var sellerAuthentication = new SqlAuthenticationService(
                factory, Zc04.CreateHasher(), new Zc04.SequentialRecoveryGenerator(), sellerSession, clock);
            Assert.True((await sellerAuthentication.LoginAsync(
                new LoginRequest(createdSeller.Login, SellerPassword, Guid.NewGuid()), token)).IsSuccess);

            var attempts = 12;
            var failures = 0;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var change = await sellerAuthentication.ChangePasswordAsync(
                    "totally-wrong-current-password", "Zc04-New-Password-2026!", Guid.NewGuid(), token);
                if (change.IsFailure)
                {
                    failures++;
                }
            }

            var failedLoginCount = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT FailedLoginCount FROM security.UserAccounts WHERE Id=@userId;",
                new SqlParameter("@userId", SqlDbType.BigInt) { Value = createdSeller.UserId });
            var lockedUntil = await Zc04.ScalarOrNullAsync<DateTime>(databaseConnection,
                "SELECT LockedUntilUtc FROM security.UserAccounts WHERE Id=@userId;",
                new SqlParameter("@userId", SqlDbType.BigInt) { Value = createdSeller.UserId });
            var failureEvents = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM security.AuthenticationEvents WHERE EventType=N'PasswordChange' AND WasSuccessful=0;");
            var auditFailures = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM audit.AuditEvents WHERE Action=N'password.change' AND Result=N'Failure';");

            Assert.Equal(attempts, failures);

            // INVARIANT: repeated wrong-current-password attempts must be mitigated by at
            // least ONE of: lockout, failed-attempt counting, event auditing, throttling.
            // RED = unlimited unlogged unthrottled password guessing through the
            // ChangePassword surface (ZC-013(B) hardening gap).
            var mitigated = failedLoginCount > 0
                || (lockedUntil is not null && lockedUntil.Value > clock.UtcNow)
                || failureEvents > 0
                || auditFailures > 0;
            if (!mitigated)
            {
                Assert.Fail(
                    "ZC-013(B) REPRODUCED: 12 wrong-current-password attempts produced no lockout, " +
                    $"no FailedLoginCount increment ({failedLoginCount}), no failure events ({failureEvents}) " +
                    $"and no audit failures ({auditFailures}).");
            }
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }

    private async Task<(long UserId, string Login, byte[] RowVersion)> CreateUserWithSellerPresetAsync(
        SqlSecurityAdministrationService administration,
        CancellationToken token)
    {
        var permissionViews = (await administration.GetPermissionsAsync(token)).Value;
        var sellerPermissionIds = permissionViews
            .Where(permission => SecurityRolePresets.SellerPermissions.Contains(permission.Code, StringComparer.Ordinal))
            .Select(permission => permission.PermissionId)
            .ToArray();
        var role = await administration.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "ZC04-SELLER-X", "Vendeur ZC04", sellerPermissionIds), token);
        Assert.True(role.IsSuccess, role.IsFailure ? role.Error.Code : null);
        var created = await administration.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "ZC04SELLX", "Vendeur X", SellerPassword,
            new[] { role.Value.RoleId }), token);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
        return (created.Value.UserId, created.Value.Login, created.Value.RowVersion);
    }
}
