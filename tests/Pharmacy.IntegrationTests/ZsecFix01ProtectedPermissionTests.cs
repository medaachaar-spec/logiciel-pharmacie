using Microsoft.Data.SqlClient;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Infrastructure.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class ZsecFix01ProtectedPermissionTests : IClassFixture<SecurityGateSqlFixture>
{
    private const string ManagerPassword = "Zsec-Fix01-Manager-Password-2026!";
    private const string EmployeePassword = "Zsec-Fix01-Employee-Password-2026!";
    private static readonly DateTime Now = new(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
    private readonly SecurityGateSqlFixture fixture;

    public ZsecFix01ProtectedPermissionTests(SecurityGateSqlFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task ZSEC_FIX01_ManagerCannotCreateRoleContainingOwnerExclusivePermission()
    {
        var manager = await CreateManagerAsync("CREATE");
        var permissionIds = await PermissionIdsAsync(
            manager.Administration,
            SecurityPermissions.CatalogView,
            SecurityPermissions.BackupRestore);

        var result = await manager.Administration.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "ZSF01-PROTECTED-CREATE", "Protected create", permissionIds), Token);

        Assert.Equal(SecurityErrors.PermissionDenied.Code, result.Error.Code);
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.Roles WHERE Code=N'ZSF01-PROTECTED-CREATE';"));
    }

    [Fact]
    public async Task ZSEC_FIX01_ManagerCannotAddOwnerExclusivePermissionToExistingRole()
    {
        var owner = await OwnerAsync();
        var ordinaryPermissionIds = await PermissionIdsAsync(owner.Administration, SecurityPermissions.CatalogView);
        var role = Success(await owner.Administration.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "ZSF01-PROTECTED-UPDATE", "Protected update", ordinaryPermissionIds), Token));
        var manager = await CreateManagerAsync("UPDATE");
        var protectedPermissionIds = await PermissionIdsAsync(
            manager.Administration,
            SecurityPermissions.CatalogView,
            SecurityPermissions.BackupRestore);

        var result = await manager.Administration.UpdateRoleAsync(new UpdateSecurityRoleRequest(
            Guid.NewGuid(), role.RoleId, role.Name, protectedPermissionIds, role.RowVersion), Token);

        Assert.Equal(SecurityErrors.PermissionDenied.Code, result.Error.Code);
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.RolePermissions rp JOIN security.Permissions p ON p.Id=rp.PermissionId WHERE rp.RoleId=@roleId AND p.Code=N'backup.restore';",
            new SqlParameter("@roleId", role.RoleId)));
    }

    [Fact]
    public async Task ZSEC_FIX01_ManagerCannotAllowOwnerExclusivePermissionForThirdParty()
    {
        var owner = await OwnerAsync();
        var target = Success(await owner.Administration.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "ZSF01OVRTGT", "Override target", EmployeePassword, [fixture.ManagerRoleId]), Token));
        var manager = await CreateManagerAsync("OVERRIDE");
        var backupRestoreId = (await PermissionIdsAsync(
            manager.Administration, SecurityPermissions.BackupRestore)).Single();

        var result = await manager.Administration.SetUserPermissionOverridesAsync(
            new SetUserPermissionOverridesRequest(
                Guid.NewGuid(),
                target.UserId,
                [new SecurityPermissionOverrideSelection(
                    backupRestoreId,
                    SecurityPermissionOverrideEffect.Allow)],
                target.RowVersion),
            Token);

        Assert.Equal(SecurityErrors.PermissionDenied.Code, result.Error.Code);
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserPermissionOverrides upo JOIN security.Permissions p ON p.Id=upo.PermissionId WHERE upo.UserAccountId=@userId AND p.Code=N'backup.restore';",
            new SqlParameter("@userId", target.UserId)));
    }

    [Fact]
    public async Task ZSEC_FIX01_ManagerCannotAssignOwnerEquivalentRoleIndirectly()
    {
        var owner = await OwnerAsync();
        var protectedPermissionIds = await PermissionIdsAsync(
            owner.Administration,
            SecurityPermissions.CatalogView,
            SecurityPermissions.BackupRestore);
        var protectedRole = Success(await owner.Administration.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "ZSF01-PROTECTED-ASSIGN", "Protected assignment", protectedPermissionIds), Token));
        var target = Success(await owner.Administration.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "ZSF01ROLETGT", "Role target", EmployeePassword, [fixture.ManagerRoleId]), Token));
        var manager = await CreateManagerAsync("ASSIGN");

        var createResult = await manager.Administration.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "ZSF01MINTED", "Minted account", EmployeePassword, [protectedRole.RoleId]), Token);
        var assignResult = await manager.Administration.SetUserRolesAsync(new SetUserRolesRequest(
            Guid.NewGuid(), target.UserId, [protectedRole.RoleId], target.RowVersion), Token);

        Assert.Equal(SecurityErrors.PermissionDenied.Code, createResult.Error.Code);
        Assert.Equal(SecurityErrors.PermissionDenied.Code, assignResult.Error.Code);
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserAccounts WHERE NormalizedLogin=N'ZSF01MINTED';"));
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserRoles WHERE UserAccountId=@userId AND RoleId=@roleId;",
            new SqlParameter("@userId", target.UserId),
            new SqlParameter("@roleId", protectedRole.RoleId)));
    }

    [Fact]
    public async Task ZSEC_FIX01_ManagerCannotRemoveDenyThatWouldExposeOwnerExclusivePermission()
    {
        var owner = await OwnerAsync();
        var backupRestoreId = (await PermissionIdsAsync(
            owner.Administration, SecurityPermissions.BackupRestore)).Single();
        var protectedRole = Success(await owner.Administration.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "ZSF01-PROTECTED-DENY", "Protected deny", [backupRestoreId]), Token));
        var target = Success(await owner.Administration.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "ZSF01DENYTGT", "Deny target", EmployeePassword, [protectedRole.RoleId]), Token));
        var denied = Success(await owner.Administration.SetUserPermissionOverridesAsync(
            new SetUserPermissionOverridesRequest(
                Guid.NewGuid(),
                target.UserId,
                [new SecurityPermissionOverrideSelection(
                    backupRestoreId,
                    SecurityPermissionOverrideEffect.Deny)],
                target.RowVersion),
            Token));
        Assert.DoesNotContain(SecurityPermissions.BackupRestore, denied.EffectivePermissions);
        var manager = await CreateManagerAsync("DENY");

        var result = await manager.Administration.SetUserPermissionOverridesAsync(
            new SetUserPermissionOverridesRequest(Guid.NewGuid(), target.UserId, [], denied.RowVersion), Token);

        Assert.Equal(SecurityErrors.PermissionDenied.Code, result.Error.Code);
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserPermissionOverrides upo JOIN security.Permissions p ON p.Id=upo.PermissionId WHERE upo.UserAccountId=@userId AND p.Code=N'backup.restore' AND upo.Effect=N'Deny';",
            new SqlParameter("@userId", target.UserId)));
    }

    [Fact]
    public async Task ZSEC_FIX01_ManagerCannotReactivateOrRecoverProtectedAccount()
    {
        var owner = await OwnerAsync();
        var backupRestoreId = (await PermissionIdsAsync(
            owner.Administration, SecurityPermissions.BackupRestore)).Single();
        var protectedRole = Success(await owner.Administration.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "ZSF01-PROTECTED-RECOVERY", "Protected recovery", [backupRestoreId]), Token));
        var protectedUser = Success(await owner.Administration.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "ZSF01RECTGT", "Recovery target", EmployeePassword, [protectedRole.RoleId]), Token));
        var disabledProtectedUser = Success(await owner.Administration.SetUserActiveAsync(
            new SetUserActiveRequest(Guid.NewGuid(), protectedUser.UserId, false, protectedUser.RowVersion), Token));
        var manager = await CreateManagerAsync("RECOVERY");
        var ownerActiveCodesBefore = await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.RecoveryCodes WHERE UserAccountId=@userId AND UsedAtUtc IS NULL;",
            new SqlParameter("@userId", fixture.OwnerUserId));

        var ownerResult = await manager.Administration.GenerateRecoveryCodesAsync(
            fixture.OwnerUserId, Guid.NewGuid(), Token);
        var protectedUserResult = await manager.Administration.GenerateRecoveryCodesAsync(
            protectedUser.UserId, Guid.NewGuid(), Token);
        var activationResult = await manager.Administration.SetUserActiveAsync(
            new SetUserActiveRequest(
                Guid.NewGuid(), disabledProtectedUser.UserId, true, disabledProtectedUser.RowVersion), Token);

        Assert.Equal(SecurityErrors.PermissionDenied.Code, ownerResult.Error.Code);
        Assert.Equal(SecurityErrors.PermissionDenied.Code, protectedUserResult.Error.Code);
        Assert.Equal(SecurityErrors.PermissionDenied.Code, activationResult.Error.Code);
        Assert.Equal(ownerActiveCodesBefore, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.RecoveryCodes WHERE UserAccountId=@userId AND UsedAtUtc IS NULL;",
            new SqlParameter("@userId", fixture.OwnerUserId)));
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.RecoveryCodes WHERE UserAccountId=@userId;",
            new SqlParameter("@userId", protectedUser.UserId)));
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserAccounts WHERE Id=@userId AND IsActive=1;",
            new SqlParameter("@userId", protectedUser.UserId)));
    }

    [Fact]
    public async Task ZSEC_FIX01_OwnerCanPerformLegitimateProtectedAdministration()
    {
        var owner = await OwnerAsync();
        var catalogViewId = (await PermissionIdsAsync(
            owner.Administration, SecurityPermissions.CatalogView)).Single();
        var backupRestoreId = (await PermissionIdsAsync(
            owner.Administration, SecurityPermissions.BackupRestore)).Single();
        var role = Success(await owner.Administration.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "ZSF01-OWNER-CONTROL", "Owner control", [catalogViewId]), Token));
        var protectedRole = Success(await owner.Administration.UpdateRoleAsync(new UpdateSecurityRoleRequest(
            Guid.NewGuid(), role.RoleId, role.Name, [catalogViewId, backupRestoreId], role.RowVersion), Token));
        var createdWithProtectedRole = Success(await owner.Administration.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "ZSF01OWNERUSR", "Owner protected user", EmployeePassword, [protectedRole.RoleId]), Token));
        var reassignmentTarget = Success(await owner.Administration.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "ZSF01OWNERSET", "Owner assignment target", EmployeePassword, [fixture.ManagerRoleId]), Token));
        var reassigned = Success(await owner.Administration.SetUserRolesAsync(new SetUserRolesRequest(
            Guid.NewGuid(), reassignmentTarget.UserId, [protectedRole.RoleId], reassignmentTarget.RowVersion), Token));
        var overrideTarget = Success(await owner.Administration.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "ZSF01OWNEROVR", "Owner override target", EmployeePassword, [fixture.ManagerRoleId]), Token));
        var overridden = Success(await owner.Administration.SetUserPermissionOverridesAsync(
            new SetUserPermissionOverridesRequest(
                Guid.NewGuid(),
                overrideTarget.UserId,
                [new SecurityPermissionOverrideSelection(
                    backupRestoreId,
                    SecurityPermissionOverrideEffect.Allow)],
                overrideTarget.RowVersion),
            Token));
        var recoveryCodes = Success(await owner.Administration.GenerateRecoveryCodesAsync(
            createdWithProtectedRole.UserId, Guid.NewGuid(), Token));

        var reloadedUsers = Success(await owner.Administration.GetUsersAsync(Token));
        Assert.Contains(SecurityPermissions.BackupRestore, protectedRole.Permissions.Select(item => item.Code));
        Assert.Contains(SecurityPermissions.BackupRestore,
            reloadedUsers.Single(item => item.UserId == createdWithProtectedRole.UserId).EffectivePermissions);
        Assert.Contains(SecurityPermissions.BackupRestore,
            reloadedUsers.Single(item => item.UserId == reassigned.UserId).EffectivePermissions);
        Assert.Contains(SecurityPermissions.BackupRestore,
            reloadedUsers.Single(item => item.UserId == overridden.UserId).EffectivePermissions);
        Assert.Equal(8, recoveryCodes.Codes.Count);
    }

    [Fact]
    public async Task ZSEC_FIX01_ManagerCanStillAdministerOrdinaryRolesAndUsers()
    {
        var manager = await CreateManagerAsync("NORMAL");
        var catalogViewId = (await PermissionIdsAsync(
            manager.Administration, SecurityPermissions.CatalogView)).Single();
        var inventoryViewId = (await PermissionIdsAsync(
            manager.Administration, SecurityPermissions.InventoryView)).Single();

        var role = Success(await manager.Administration.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "ZSF01-NORMAL", "Normal role", [catalogViewId]), Token));
        var user = Success(await manager.Administration.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "ZSF01NORMAL", "Normal employee", EmployeePassword, [role.RoleId]), Token));
        var updated = Success(await manager.Administration.UpdateRoleAsync(new UpdateSecurityRoleRequest(
            Guid.NewGuid(), role.RoleId, "Normal role updated", [catalogViewId, inventoryViewId], role.RowVersion), Token));
        var recoveryCodes = Success(await manager.Administration.GenerateRecoveryCodesAsync(
            user.UserId, Guid.NewGuid(), Token));

        var reloadedUser = Success(await manager.Administration.GetUsersAsync(Token))
            .Single(item => item.UserId == user.UserId);
        Assert.Equal(
            [SecurityPermissions.CatalogView, SecurityPermissions.InventoryView],
            reloadedUser.EffectivePermissions.Order(StringComparer.Ordinal));
        Assert.Equal(
            [SecurityPermissions.CatalogView, SecurityPermissions.InventoryView],
            updated.Permissions.Select(item => item.Code).Order(StringComparer.Ordinal));
        Assert.Equal(8, recoveryCodes.Codes.Count);
    }

    private CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<AdministrationActor> OwnerAsync() =>
        await AuthenticateAsync(SecurityGateSqlFixture.OwnerLogin, SecurityGateSqlFixture.OwnerPassword);

    private async Task<AdministrationActor> CreateManagerAsync(string suffix)
    {
        var owner = await OwnerAsync();
        var login = $"ZSF01MGR{suffix}";
        Success(await owner.Administration.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), login, $"Manager {suffix}", ManagerPassword, [fixture.ManagerRoleId]), Token));
        return await AuthenticateAsync(login, ManagerPassword);
    }

    private async Task<AdministrationActor> AuthenticateAsync(string login, string password)
    {
        var clock = new Zc04.Zc04Clock(Now);
        var session = new CurrentSecuritySession();
        var authentication = new SqlAuthenticationService(
            fixture.Factory, fixture.Hasher, new UniqueRecoveryGenerator(), session, clock);
        var loginResult = await authentication.LoginAsync(new LoginRequest(login, password, Guid.NewGuid()), Token);
        Assert.True(loginResult.IsSuccess, loginResult.IsFailure ? loginResult.Error.Code : null);
        var reauthentication = await authentication.ReauthenticateAsync(password, Guid.NewGuid(), Token);
        Assert.True(reauthentication.IsSuccess, reauthentication.IsFailure ? reauthentication.Error.Code : null);
        var authorizer = new PermissionAuthorizer(session, clock, new SqlSecuritySessionValidator(fixture.Factory));
        var administration = new SqlSecurityAdministrationService(
            fixture.Factory, fixture.Hasher, new UniqueRecoveryGenerator(), authorizer, session, clock);
        return new AdministrationActor(administration);
    }

    private async Task<long[]> PermissionIdsAsync(
        SqlSecurityAdministrationService administration,
        params string[] codes)
    {
        var permissions = Success(await administration.GetPermissionsAsync(Token));
        var ids = permissions
            .Where(permission => codes.Contains(permission.Code, StringComparer.Ordinal))
            .Select(permission => permission.PermissionId)
            .ToArray();
        Assert.Equal(codes.Length, ids.Length);
        return ids;
    }

    private static T Success<T>(Pharmacy.Domain.Common.Result<T> result)
    {
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    private sealed record AdministrationActor(SqlSecurityAdministrationService Administration);
}
