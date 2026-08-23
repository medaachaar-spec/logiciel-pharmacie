using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Security;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

/// <summary>
/// ZSEC-PILOT-01 reproduction candidate: a MANAGER-equivalent administrator
/// (every permission except backup.restore, per SecurityRolePresets) mints an
/// OWNER-equivalent account through two individually-permitted administration
/// operations: creating a custom role that includes backup.restore, then
/// creating a new user carrying that role with an initial password chosen by
/// the manager. ZC-001 blocks only SELF-targeted overrides/role changes; the
/// two-step indirection through a new account is the gap under test.
/// If the escalation succeeds the test fails on purpose with "ZSEC-01
/// REPRODUCED"; a green run means the defense holds.
/// </summary>
[Collection("Sprint 09 persistence serial")]
public sealed class ZsecPilot01EscalationReproTests
{
    private const string OwnerPassword = "Zsec01-Owner-Password-2026!";
    private const string ManagerPassword = "Zsec01-Manager-Pass-2026!";
    private const string AccomplicePassword = "Zsec01-Accomplice-Pass-2026!";

    [Fact]
    public async Task ZSEC01_ManagerMustNotMintOwnerEquivalentAccountViaCustomRoleAndUserCreation()
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
            var owner = await Zc04.BootstrapOwnerAsync(factory, clock, "zsec01.owner", OwnerPassword);

            var permissionViews = (await owner.Administration.GetPermissionsAsync(token)).Value;
            var managerPermissionIds = permissionViews
                .Where(permission => SecurityRolePresets.ManagerPermissions.Contains(permission.Code, StringComparer.Ordinal))
                .Select(permission => permission.PermissionId)
                .ToArray();
            Assert.Equal(SecurityRolePresets.ManagerPermissions.Count, managerPermissionIds.Length);

            var managerRole = await owner.Administration.CreateRoleAsync(new CreateSecurityRoleRequest(
                Guid.NewGuid(), "ZSEC01-MANAGER", "Manager ZSEC01", managerPermissionIds), token);
            Assert.True(managerRole.IsSuccess, managerRole.IsFailure ? managerRole.Error.Code : null);
            var manager = await owner.Administration.CreateUserAsync(new CreateSecurityUserRequest(
                Guid.NewGuid(), "ZSEC01MGR", "Manager ZSEC01", ManagerPassword,
                new[] { managerRole.Value.RoleId }), token);
            Assert.True(manager.IsSuccess, manager.IsFailure ? manager.Error.Code : null);

            var managerSession = new CurrentSecuritySession();
            var hasher = Zc04.CreateHasher();
            var managerAuthentication = new SqlAuthenticationService(
                factory, hasher, new Zc04.SequentialRecoveryGenerator(), managerSession, clock);
            var managerLogin = await managerAuthentication.LoginAsync(
                new LoginRequest("ZSEC01MGR", ManagerPassword, Guid.NewGuid()), token);
            Assert.True(managerLogin.IsSuccess, managerLogin.IsFailure ? managerLogin.Error.Code : null);

            var managerAuthorizer = new PermissionAuthorizer(
                managerSession, clock, new SqlSecuritySessionValidator(factory));
            Assert.Equal("security.authorization.denied",
                managerAuthorizer.Authorize(SecurityPermissions.BackupRestore).Error.Code);

            var reauthenticated = await managerAuthentication.ReauthenticateAsync(
                ManagerPassword, Guid.NewGuid(), token);
            Assert.True(reauthenticated.IsSuccess, reauthenticated.IsFailure ? reauthenticated.Error.Code : null);

            var managerAdministration = new SqlSecurityAdministrationService(
                factory, hasher, new Zc04.SequentialRecoveryGenerator(), managerAuthorizer, managerSession, clock);

            // Step 1 — Manager creates a custom role containing EVERY permission,
            // including the OWNER-exclusive backup.restore.
            var allPermissionIds = permissionViews.Select(permission => permission.PermissionId).ToArray();
            var allPowerfulRole = await managerAdministration.CreateRoleAsync(new CreateSecurityRoleRequest(
                Guid.NewGuid(), "ZSEC01-ALL", "Rôle tout pouvoir ZSEC01", allPermissionIds), token);

            SecurityUserSummaryView? accomplice = null;
            if (allPowerfulRole.IsSuccess)
            {
                // Step 2 — Manager creates a fresh user carrying that role with an
                // initial password chosen by the manager.
                var created = await managerAdministration.CreateUserAsync(new CreateSecurityUserRequest(
                    Guid.NewGuid(), "ZSEC01ACC", "Complice ZSEC01", AccomplicePassword,
                    new[] { allPowerfulRole.Value.RoleId }), token);
                if (created.IsSuccess)
                {
                    accomplice = created.Value;
                }
                else
                {
                    Assert.Fail(
                        "ZSEC-01 PARTIAL: role creation with backup.restore was accepted by a MANAGER " +
                        $"but user creation failed ({created.Error.Code}) — escalation blocked at the second step.");
                }
            }

            if (accomplice is null)
            {
                // The defense held: the manager could not complete the two-step escalation.
                Assert.False(allPowerfulRole.IsSuccess,
                    "unreachable: accomplice null requires role failure or explicit user failure");
                return;
            }

            // Step 3 — the accomplice account logs in with the manager-chosen password.
            var accompliceSession = new CurrentSecuritySession();
            var accompliceAuthentication = new SqlAuthenticationService(
                factory, hasher, new Zc04.SequentialRecoveryGenerator(), accompliceSession, clock);
            var accompliceLogin = await accompliceAuthentication.LoginAsync(
                new LoginRequest("ZSEC01ACC", AccomplicePassword, Guid.NewGuid()), token);
            Assert.True(accompliceLogin.IsSuccess, accompliceLogin.IsFailure ? accompliceLogin.Error.Code : null);

            // Step 4 — does the minted account hold the OWNER-exclusive capability?
            var accompliceAuthorizer = new PermissionAuthorizer(
                accompliceSession, clock, new SqlSecuritySessionValidator(factory));
            var backupRestoreAuthorization = accompliceAuthorizer.Authorize(SecurityPermissions.BackupRestore);
            var manageUsersAuthorization = accompliceAuthorizer.Authorize(SecurityPermissions.SecurityManageUsers);

            Assert.Fail(
                "ZSEC-01 REPRODUCED: a MANAGER-equivalent administrator minted an OWNER-equivalent account. " +
                $"Role creation accepted={allPowerfulRole.IsSuccess} " +
                $"(role '{allPowerfulRole.Value.Code}' carries all {allPermissionIds.Length} permissions incl. backup.restore); " +
                $"accomplice user id={accomplice.UserId}; login success={accompliceLogin.IsSuccess}; " +
                $"accomplice Authorize(backup.restore)={backupRestoreAuthorization.IsSuccess}; " +
                $"accomplice Authorize(security.manage_users)={manageUsersAuthorization.IsSuccess}.");
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }
}
