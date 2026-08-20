using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Desktop.Components.Security;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Security;
using Pharmacy.Infrastructure.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class SqlSecurityGateIntegrationTests : IClassFixture<SecurityGateSqlFixture>
{
    private const string Password = "SEC-Gate-Strong-Password-2026!";
    private const string NewPassword = "SEC-Gate-New-Password-2026!";
    private static readonly DateTime Now = new(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);
    private readonly SecurityGateSqlFixture fixture;

    public SqlSecurityGateIntegrationTests(SecurityGateSqlFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task SEC001_LoginSuccessPersistsAndUpdatesLastLogin()
    {
        var first = Authentication(Now);
        var loginA = await first.Service.LoginAsync(new LoginRequest(
            SecurityGateSqlFixture.OwnerLogin, SecurityGateSqlFixture.OwnerPassword, Guid.NewGuid()), Token);
        Assert.True(loginA.IsSuccess, Error(loginA));
        Assert.Equal(Now, await fixture.ScalarAsync<DateTime>(
            "SELECT LastLoginAtUtc FROM security.UserAccounts WHERE Id=@id;", P("@id", fixture.OwnerUserId)));

        var second = Authentication(Now.AddMinutes(1));
        Assert.True((await second.Service.HasUsersAsync(Token)).Value);
        var loginB = await second.Service.LoginAsync(new LoginRequest(
            SecurityGateSqlFixture.OwnerLogin, SecurityGateSqlFixture.OwnerPassword, Guid.NewGuid()), Token);
        Assert.True(loginB.IsSuccess, Error(loginB));
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT FailedLoginCount FROM security.UserAccounts WHERE Id=@id;", P("@id", fixture.OwnerUserId)));
    }

    [Fact]
    public async Task SEC002_BadPasswordReturnsNeutralError()
    {
        await CreateUserAsync("sec002", "SEC 002");
        var result = await Authentication(Now).Service.LoginAsync(
            new LoginRequest("sec002", "not-the-password", Guid.NewGuid()), Token);
        Assert.Equal(SecurityErrors.InvalidCredentials.Code, result.Error.Code);
        Assert.Equal(SecurityErrors.InvalidCredentials.Message, result.Error.Message);
    }

    [Fact]
    public async Task SEC003_UnknownLoginHasSameNeutralErrorAsExistingLogin()
    {
        await CreateUserAsync("sec003", "SEC 003");
        var service = Authentication(Now).Service;
        var existing = await service.LoginAsync(new LoginRequest("sec003", "wrong", Guid.NewGuid()), Token);
        var unknown = await service.LoginAsync(new LoginRequest("sec003-unknown", "wrong", Guid.NewGuid()), Token);
        Assert.Equal(existing.Error.Code, unknown.Error.Code);
        Assert.Equal(existing.Error.Message, unknown.Error.Message);
        Assert.Equal(SecurityErrors.InvalidCredentials.Code, unknown.Error.Code);
    }

    [Fact]
    public async Task SEC004_FiveFailuresTriggerLockout()
    {
        var user = await CreateUserAsync("sec004", "SEC 004");
        var authentication = Authentication(Now);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var result = await authentication.Service.LoginAsync(
                new LoginRequest("sec004", "wrong", Guid.NewGuid()), Token);
            Assert.Equal(attempt == 5 ? SecurityErrors.AccountLocked.Code : SecurityErrors.InvalidCredentials.Code, result.Error.Code);
        }
        Assert.Equal(5, await fixture.ScalarAsync<int>(
            "SELECT FailedLoginCount FROM security.UserAccounts WHERE Id=@id;", P("@id", user.UserId)));
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserAccounts WHERE Id=@id AND LockedUntilUtc>@now;", P("@id", user.UserId), P("@now", Now)));
    }

    [Fact]
    public async Task SEC005_CorrectPasswordDuringLockoutIsRejectedWithoutSession()
    {
        var user = await CreateUserAsync("sec005", "SEC 005");
        var authentication = Authentication(Now);
        await LockAsync(authentication.Service, "sec005");
        var result = await authentication.Service.LoginAsync(new LoginRequest("sec005", Password, Guid.NewGuid()), Token);
        Assert.Equal(SecurityErrors.AccountLocked.Code, result.Error.Code);
        Assert.False(authentication.Session.IsAuthenticated);
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserAccounts WHERE Id=@id AND LastLoginAtUtc IS NOT NULL;", P("@id", user.UserId)));
    }

    [Fact]
    public async Task SEC006_LoginAfterLockoutExpirationSucceedsAndResetsState()
    {
        var user = await CreateUserAsync("sec006", "SEC 006");
        var authentication = Authentication(Now);
        await LockAsync(authentication.Service, "sec006");
        authentication.Clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));
        var result = await authentication.Service.LoginAsync(new LoginRequest("sec006", Password, Guid.NewGuid()), Token);
        Assert.True(result.IsSuccess, Error(result));
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserAccounts WHERE Id=@id AND FailedLoginCount=0 AND LockedUntilUtc IS NULL;", P("@id", user.UserId)));
    }

    [Fact]
    public async Task SEC007_SuccessfulLoginResetsFailureCountAndNextFailureStartsAtOne()
    {
        var user = await CreateUserAsync("sec007", "SEC 007");
        var authentication = Authentication(Now);
        await authentication.Service.LoginAsync(new LoginRequest("sec007", "wrong-1", Guid.NewGuid()), Token);
        await authentication.Service.LoginAsync(new LoginRequest("sec007", "wrong-2", Guid.NewGuid()), Token);
        Assert.True((await authentication.Service.LoginAsync(new LoginRequest("sec007", Password, Guid.NewGuid()), Token)).IsSuccess);
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT FailedLoginCount FROM security.UserAccounts WHERE Id=@id;", P("@id", user.UserId)));
        await authentication.Service.LogoutAsync(Guid.NewGuid(), Token);
        await authentication.Service.LoginAsync(new LoginRequest("sec007", "wrong-3", Guid.NewGuid()), Token);
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT FailedLoginCount FROM security.UserAccounts WHERE Id=@id;", P("@id", user.UserId)));
    }

    [Fact]
    public async Task SEC008_DisabledAccountIsRetainedRejectedAndCanBeReactivated()
    {
        var admin = Administration(Now);
        var created = await admin.Service.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "sec008", "SEC 008", Password, [fixture.ManagerRoleId]), Token);
        Assert.True(created.IsSuccess, Error(created));
        var disabled = await admin.Service.SetUserActiveAsync(new SetUserActiveRequest(
            Guid.NewGuid(), created.Value.UserId, false, created.Value.RowVersion), Token);
        Assert.True(disabled.IsSuccess, Error(disabled));
        var login = await Authentication(Now).Service.LoginAsync(new LoginRequest("sec008", Password, Guid.NewGuid()), Token);
        Assert.Equal(SecurityErrors.InvalidCredentials.Code, login.Error.Code);
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserAccounts WHERE Id=@id AND IsActive=0 AND LastLoginAtUtc IS NULL;", P("@id", created.Value.UserId)));

        var refreshed = (await admin.Service.GetUsersAsync(Token)).Value.Single(item => item.UserId == created.Value.UserId);
        var enabled = await admin.Service.SetUserActiveAsync(new SetUserActiveRequest(
            Guid.NewGuid(), created.Value.UserId, true, refreshed.RowVersion), Token);
        Assert.True(enabled.IsSuccess, Error(enabled));
        Assert.True((await Authentication(Now).Service.LoginAsync(
            new LoginRequest("sec008", Password, Guid.NewGuid()), Token)).IsSuccess);
    }

    [Fact]
    public async Task SEC009_WrongReauthenticationCreatesNoAuthorizationWindow()
    {
        await CreateUserAsync("sec009", "SEC 009");
        var authentication = Authentication(Now);
        Assert.True((await authentication.Service.LoginAsync(new LoginRequest("sec009", Password, Guid.NewGuid()), Token)).IsSuccess);
        authentication.Clock.Advance(TimeSpan.FromMinutes(6));
        var before = authentication.Session.User!.LastReauthenticatedAtUtc;
        var result = await authentication.Service.ReauthenticateAsync("wrong", Guid.NewGuid(), Token);
        Assert.Equal(SecurityErrors.InvalidCredentials.Code, result.Error.Code);
        Assert.Equal(before, authentication.Session.User!.LastReauthenticatedAtUtc);
        Assert.Equal(SecurityErrors.ReauthenticationRequired.Code,
            new PermissionAuthorizer(authentication.Session, authentication.Clock)
                .Authorize(SecurityPermissions.SecurityManageUsers, true).Error.Code);
    }

    [Fact]
    public async Task SEC010_CorrectReauthenticationCreatesTemporaryAuthorizationWindow()
    {
        await CreateUserAsync("sec010", "SEC 010");
        var authentication = Authentication(Now);
        Assert.True((await authentication.Service.LoginAsync(new LoginRequest("sec010", Password, Guid.NewGuid()), Token)).IsSuccess);
        authentication.Clock.Advance(TimeSpan.FromMinutes(6));
        Assert.True((await authentication.Service.ReauthenticateAsync(Password, Guid.NewGuid(), Token)).IsSuccess);
        Assert.Equal(authentication.Clock.UtcNow, authentication.Session.User!.LastReauthenticatedAtUtc);
        Assert.True(new PermissionAuthorizer(authentication.Session, authentication.Clock)
            .Authorize(SecurityPermissions.SecurityManageUsers, true).IsSuccess);
    }

    [Fact]
    public async Task SEC011_ReauthenticationExpiresAndASecondValidSecretRestoresAccess()
    {
        await CreateUserAsync("sec011", "SEC 011");
        var authentication = Authentication(Now);
        Assert.True((await authentication.Service.LoginAsync(new LoginRequest("sec011", Password, Guid.NewGuid()), Token)).IsSuccess);
        var authorizer = new PermissionAuthorizer(authentication.Session, authentication.Clock);
        authentication.Clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));
        Assert.Equal(SecurityErrors.ReauthenticationRequired.Code,
            authorizer.Authorize(SecurityPermissions.SecurityManageRoles, true).Error.Code);
        Assert.True((await authentication.Service.ReauthenticateAsync(Password, Guid.NewGuid(), Token)).IsSuccess);
        Assert.True(authorizer.Authorize(SecurityPermissions.SecurityManageRoles, true).IsSuccess);
    }

    [Fact]
    public async Task SEC012_UserCreationPersistsRoleAuditAndLoginAcrossContexts()
    {
        var created = await CreateUserAsync("sec012", "SEC 012");
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserRoles WHERE UserAccountId=@id AND RoleId=@role;", P("@id", created.UserId), P("@role", fixture.ManagerRoleId)));
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM audit.AuditEvents WHERE EntityType=N'UserAccount' AND EntityId=@id AND Action=N'user.create' AND Result=N'Success';",
            P("@id", created.UserId.ToString())));
        var login = await Authentication(Now.AddMinutes(1)).Service.LoginAsync(
            new LoginRequest("sec012", Password, Guid.NewGuid()), Token);
        Assert.True(login.IsSuccess, Error(login));
        Assert.True(login.Value.MustChangePassword);
    }

    [Fact]
    public async Task SEC013_DuplicateNormalizedUserIsRejectedAtomically()
    {
        var first = await CreateUserAsync(" sec013 ", "SEC 013");
        var second = await Administration(Now).Service.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "SEC013", "Duplicate", Password, [fixture.ManagerRoleId]), Token);
        Assert.Equal(SecurityErrors.LoginAlreadyExists.Code, second.Error.Code);
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserAccounts WHERE NormalizedLogin=N'SEC013';"));
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserRoles WHERE UserAccountId=@id;", P("@id", first.UserId)));
    }

    [Fact]
    public async Task SEC014_RoleCreationNormalizesAndPersistsExactPermissions()
    {
        var admin = Administration(Now);
        var permissionIds = await PermissionIdsAsync(SecurityPermissions.CatalogView, SecurityPermissions.InventoryView);
        var created = await admin.Service.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), " role-sec014 ", " SEC 014 ", permissionIds), Token);
        Assert.True(created.IsSuccess, Error(created));
        Assert.Equal("ROLE-SEC014", created.Value.Code);
        Assert.False(created.Value.IsSystemRole);
        Assert.True(created.Value.IsActive);
        Assert.Equal(new[] { SecurityPermissions.CatalogView, SecurityPermissions.InventoryView }.Order(),
            created.Value.Permissions.Select(item => item.Code));
        var reloaded = await Administration(Now.AddMinutes(1)).Service.GetRolesAsync(Token);
        Assert.Contains(reloaded.Value, role => role.Code == "ROLE-SEC014" && role.Name == "SEC 014");
    }

    [Fact]
    public async Task SEC015_UnknownPermissionIsRejectedWithoutPartialRole()
    {
        var result = await Administration(Now).Service.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "ROLE-SEC015", "SEC 015", [long.MaxValue]), Token);
        Assert.Equal(SecurityErrors.InvalidPermissionSelection.Code, result.Error.Code);
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.Roles WHERE Code=N'ROLE-SEC015';"));
    }

    [Fact]
    public async Task SEC016_RolePermissionUpdateIsExactWithoutDuplicates()
    {
        var admin = Administration(Now);
        var initial = await PermissionIdsAsync(SecurityPermissions.CatalogView, SecurityPermissions.InventoryView);
        var created = await admin.Service.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "ROLE-SEC016", "SEC 016", initial), Token);
        Assert.True(created.IsSuccess, Error(created));
        var next = await PermissionIdsAsync(SecurityPermissions.CatalogView, SecurityPermissions.SalesCreate);
        var updated = await admin.Service.UpdateRoleAsync(new UpdateSecurityRoleRequest(
            Guid.NewGuid(), created.Value.RoleId, "SEC 016 updated", next, created.Value.RowVersion), Token);
        Assert.True(updated.IsSuccess, Error(updated));
        Assert.Equal(new[] { SecurityPermissions.CatalogView, SecurityPermissions.SalesCreate }.Order(),
            updated.Value.Permissions.Select(item => item.Code));
        Assert.Equal(2, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.RolePermissions WHERE RoleId=@id;", P("@id", created.Value.RoleId)));
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.RolePermissions rp JOIN security.Permissions p ON p.Id=rp.PermissionId WHERE rp.RoleId=@id AND p.Code=N'inventory.view';",
            P("@id", created.Value.RoleId)));
        Assert.Equal("ROLE-SEC016", await fixture.ScalarAsync<string>(
            "SELECT Code FROM security.Roles WHERE Id=@id;", P("@id", created.Value.RoleId)));
        Assert.DoesNotContain(typeof(UpdateSecurityRoleRequest).GetProperties(), property => property.Name == "Code");
    }

    [Fact]
    public async Task SEC017_OwnerSystemRoleIsProtectedByBackendContract()
    {
        var admin = Administration(Now);
        var owner = (await admin.Service.GetRolesAsync(Token)).Value.Single(role => role.Code == "OWNER");
        var update = await admin.Service.UpdateRoleAsync(new UpdateSecurityRoleRequest(
            Guid.NewGuid(), owner.RoleId, "Modified", [], owner.RowVersion), Token);
        Assert.Equal(SecurityErrors.SystemRoleProtected.Code, update.Error.Code);
        Assert.True(owner.IsSystemRole);
        Assert.Equal(SecurityPermissions.All.Count, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.RolePermissions WHERE RoleId=@id;", P("@id", owner.RoleId)));
        Assert.DoesNotContain(typeof(ISecurityAdministrationService).GetMethods(), method =>
            method.Name.Contains("DeleteRole", StringComparison.Ordinal) || method.Name.Contains("SetRoleActive", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SEC018_RoleReassignmentIsExclusiveAndPersists()
    {
        var admin = Administration(Now);
        var permissions = await PermissionIdsAsync(SecurityPermissions.CatalogView);
        var roleA = await admin.Service.CreateRoleAsync(new CreateSecurityRoleRequest(Guid.NewGuid(), "ROLE-SEC018-A", "A", permissions), Token);
        var roleB = await admin.Service.CreateRoleAsync(new CreateSecurityRoleRequest(Guid.NewGuid(), "ROLE-SEC018-B", "B", permissions), Token);
        var user = await admin.Service.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "sec018", "SEC 018", Password, [roleA.Value.RoleId]), Token);
        var reassigned = await admin.Service.SetUserRolesAsync(new SetUserRolesRequest(
            Guid.NewGuid(), user.Value.UserId, [roleB.Value.RoleId], user.Value.RowVersion), Token);
        Assert.True(reassigned.IsSuccess, Error(reassigned));
        Assert.Equal(["ROLE-SEC018-B"], reassigned.Value.Roles);
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserRoles WHERE UserAccountId=@id AND RoleId=@role;", P("@id", user.Value.UserId), P("@role", roleB.Value.RoleId)));
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserRoles WHERE UserAccountId=@id AND RoleId=@role;", P("@id", user.Value.UserId), P("@role", roleA.Value.RoleId)));
    }

    [Fact]
    public async Task SEC019_EffectivePermissionsAreExactAfterLogin()
    {
        var expected = new[]
        {
            SecurityPermissions.CatalogView,
            SecurityPermissions.InventoryView,
            SecurityPermissions.SalesCreate,
            SecurityPermissions.SalesViewHistory
        };
        var admin = Administration(Now);
        var role = await admin.Service.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "ROLE-SEC019", "SEC 019", await PermissionIdsAsync(expected)), Token);
        Assert.True(role.IsSuccess, Error(role));
        var user = await admin.Service.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "sec019", "SEC 019", Password, [role.Value.RoleId]), Token);
        Assert.True(user.IsSuccess, Error(user));
        var login = await Authentication(Now).Service.LoginAsync(new LoginRequest("sec019", Password, Guid.NewGuid()), Token);
        Assert.True(login.IsSuccess, Error(login));
        Assert.Equal(expected.Order(StringComparer.Ordinal), login.Value.Session.Permissions.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task SEC025_RoleRowVersionRejectsStaleConcurrentUpdate()
    {
        var admin = Administration(Now);
        var initial = await PermissionIdsAsync(SecurityPermissions.CatalogView);
        var created = await admin.Service.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "ROLE-SEC025", "SEC 025", initial), Token);
        Assert.True(created.IsSuccess, Error(created));
        var first = await admin.Service.UpdateRoleAsync(new UpdateSecurityRoleRequest(
            Guid.NewGuid(), created.Value.RoleId, "SEC 025 first", initial, created.Value.RowVersion), Token);
        Assert.True(first.IsSuccess, Error(first));
        var stale = await admin.Service.UpdateRoleAsync(new UpdateSecurityRoleRequest(
            Guid.NewGuid(), created.Value.RoleId, "SEC 025 stale", initial, created.Value.RowVersion), Token);
        Assert.Equal(SecurityErrors.ConcurrencyConflict.Code, stale.Error.Code);
        Assert.Equal("SEC 025 first", await fixture.ScalarAsync<string>(
            "SELECT Name FROM security.Roles WHERE Id=@id;", P("@id", created.Value.RoleId)));
    }

    [Fact]
    public async Task SEC026_SecurityAuditContainsActorActionTargetUtcAndResult()
    {
        var admin = Administration(Now);
        var catalog = await PermissionIdsAsync(SecurityPermissions.CatalogView);
        var role = await admin.Service.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "ROLE-SEC026", "SEC 026", catalog), Token);
        Assert.True(role.IsSuccess, Error(role));
        var inventory = await PermissionIdsAsync(SecurityPermissions.InventoryView);
        var roleUpdated = await admin.Service.UpdateRoleAsync(new UpdateSecurityRoleRequest(
            Guid.NewGuid(), role.Value.RoleId, "SEC 026 updated", inventory, role.Value.RowVersion), Token);
        Assert.True(roleUpdated.IsSuccess, Error(roleUpdated));
        var user = await admin.Service.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "sec026", "SEC 026", Password, [fixture.ManagerRoleId]), Token);
        Assert.True(user.IsSuccess, Error(user));
        var assigned = await admin.Service.SetUserRolesAsync(new SetUserRolesRequest(
            Guid.NewGuid(), user.Value.UserId, [role.Value.RoleId], user.Value.RowVersion), Token);
        Assert.True(assigned.IsSuccess, Error(assigned));
        var disabled = await admin.Service.SetUserActiveAsync(new SetUserActiveRequest(
            Guid.NewGuid(), user.Value.UserId, false, assigned.Value.RowVersion), Token);
        Assert.True(disabled.IsSuccess, Error(disabled));
        var enabled = await admin.Service.SetUserActiveAsync(new SetUserActiveRequest(
            Guid.NewGuid(), user.Value.UserId, true, disabled.Value.RowVersion), Token);
        Assert.True(enabled.IsSuccess, Error(enabled));

        var authentication = Authentication(Now.AddMinutes(1));
        await authentication.Service.LoginAsync(new LoginRequest("sec026", "wrong", Guid.NewGuid()), Token);
        Assert.True((await authentication.Service.LoginAsync(new LoginRequest("sec026", Password, Guid.NewGuid()), Token)).IsSuccess);
        await authentication.Service.ReauthenticateAsync("wrong", Guid.NewGuid(), Token);
        Assert.True((await authentication.Service.ReauthenticateAsync(Password, Guid.NewGuid(), Token)).IsSuccess);

        var actions = new[]
        {
            "role.create", "role.permissions.change", "user.create", "user.roles.set",
            "user.disable", "user.activate", "login", "reauthenticate"
        };
        foreach (var action in actions)
        {
            Assert.True(await fixture.ScalarAsync<int>(
                "SELECT COUNT(*) FROM audit.AuditEvents WHERE Action=@action AND OccurredAtUtc IS NOT NULL AND Result IN (N'Success',N'Failure',N'Denied') AND CorrelationId<>CAST(0x0 AS uniqueidentifier);",
                P("@action", action)) > 0, $"Audit absent pour {action}.");
        }
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM audit.AuditEvents WHERE Action=N'user.roles.set' AND EntityId=@id AND ActorUserId=@actor AND Result=N'Success';",
            P("@id", user.Value.UserId.ToString()), P("@actor", fixture.OwnerUserId)));
    }

    [Fact]
    public async Task SEC027_AuditAndAuthenticationEventsContainNoPasswordHashRecoveryOrCardSecrets()
    {
        const string rawPassword = "SEC027-Unique-Raw-Password-2026!";
        var admin = Administration(Now);
        var user = await admin.Service.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "sec027", "SEC 027", rawPassword, [fixture.ManagerRoleId]), Token);
        Assert.True(user.IsSuccess, Error(user));
        var codes = await admin.Service.GenerateRecoveryCodesAsync(user.Value.UserId, Guid.NewGuid(), Token);
        Assert.True(codes.IsSuccess, Error(codes));
        await Authentication(Now).Service.LoginAsync(new LoginRequest("sec027", rawPassword, Guid.NewGuid()), Token);

        var forbidden = new[]
        {
            rawPassword,
            codes.Value.Codes[0],
            "PasswordHash",
            "4111111111111111",
            "123"
        };
        foreach (var value in forbidden)
        {
            Assert.Equal(0, await fixture.ScalarAsync<int>(
                """
                SELECT COUNT(*)
                FROM audit.AuditEvents e
                LEFT JOIN audit.AuditEventDetails d ON d.AuditEventId=e.Id
                WHERE CONCAT_WS(N'|',e.EventType,e.Module,e.EntityType,e.EntityId,e.Action,e.ActorDisplayName,e.Result,e.ReasonCode,d.FieldName,d.OldValueRedacted,d.NewValueRedacted)
                      LIKE N'%' + @value + N'%';
                """, P("@value", value)));
            Assert.Equal(0, await fixture.ScalarAsync<int>(
                "SELECT COUNT(*) FROM security.AuthenticationEvents WHERE CONCAT_WS(N'|',EventType,UsernameAttemptHash,FailureReasonCode) LIKE N'%' + @value + N'%';",
                P("@value", value)));
        }
        var exposedProperties = typeof(SecurityUserSummaryView).GetProperties().Select(property => property.Name).ToArray();
        Assert.DoesNotContain("PasswordHash", exposedProperties);
        Assert.DoesNotContain("RecoveryCodes", exposedProperties);
        Assert.DoesNotContain("CodeHash", exposedProperties);
        Assert.Equal(["SearchAsync"], typeof(IAuditReader).GetMethods().Select(method => method.Name));
        var coordinatorDependencies = typeof(SecurityCoordinator)
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Select(field => field.FieldType)
            .ToArray();
        Assert.Contains(typeof(IAuditReader), coordinatorDependencies);
        Assert.DoesNotContain(typeof(IAuditWriter), coordinatorDependencies);
    }

    [Fact]
    public async Task SEC028_AuthenticationEventsDistinguishSuccessFailureAndLockoutWithoutFalseSuccess()
    {
        await CreateUserAsync("sec028", "SEC 028");
        var authentication = Authentication(Now);
        var failureId = Guid.NewGuid();
        var successId = Guid.NewGuid();
        Assert.True((await authentication.Service.LoginAsync(new LoginRequest("sec028", "wrong", failureId), Token)).IsFailure);
        Assert.True((await authentication.Service.LoginAsync(new LoginRequest("sec028", Password, successId), Token)).IsSuccess);
        await authentication.Service.LogoutAsync(Guid.NewGuid(), Token);
        var lockIds = new List<Guid>();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var id = Guid.NewGuid();
            lockIds.Add(id);
            await authentication.Service.LoginAsync(new LoginRequest("sec028", "wrong-again", id), Token);
        }

        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.AuthenticationEvents WHERE CorrelationId=@id AND WasSuccessful=0 AND FailureReasonCode=N'invalid_credentials';", P("@id", failureId)));
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.AuthenticationEvents WHERE CorrelationId=@id AND WasSuccessful=1 AND FailureReasonCode IS NULL;", P("@id", successId)));
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.AuthenticationEvents WHERE CorrelationId=@id AND WasSuccessful=0 AND FailureReasonCode=N'locked';", P("@id", lockIds[^1])));
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.AuthenticationEvents WHERE CorrelationId IN (@f,@l) AND WasSuccessful=1;",
            P("@f", failureId), P("@l", lockIds[^1])));
    }

    [Fact]
    public async Task SEC029_RebuildingServiceProviderKeepsAccountAndAuthenticationWithoutBootstrap()
    {
        await using (var providerA = Services().BuildServiceProvider())
        {
            await using var scope = providerA.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
            Assert.True((await service.HasUsersAsync(Token)).Value);
            Assert.True((await service.LoginAsync(new LoginRequest(
                SecurityGateSqlFixture.OwnerLogin, SecurityGateSqlFixture.OwnerPassword, Guid.NewGuid()), Token)).IsSuccess);
        }
        await using (var providerB = Services().BuildServiceProvider())
        {
            await using var scope = providerB.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
            Assert.True((await service.HasUsersAsync(Token)).Value);
            Assert.True((await service.LoginAsync(new LoginRequest(
                SecurityGateSqlFixture.OwnerLogin, SecurityGateSqlFixture.OwnerPassword, Guid.NewGuid()), Token)).IsSuccess);
        }
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserAccounts WHERE NormalizedLogin=N'OWNER.SECURITY.GATE';"));
    }

    [Fact]
    public async Task SEC030_DuplicateRoleFailureRollsBackRolePermissionsAndAudit()
    {
        var admin = Administration(Now);
        var initial = await PermissionIdsAsync(SecurityPermissions.CatalogView);
        var created = await admin.Service.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "ROLE-SEC030", "SEC 030", initial), Token);
        Assert.True(created.IsSuccess, Error(created));
        var duplicate = await admin.Service.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "ROLE-SEC030", "Duplicate", await PermissionIdsAsync(SecurityPermissions.InventoryView)), Token);
        Assert.Equal(SecurityErrors.RoleCodeAlreadyExists.Code, duplicate.Error.Code);
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.Roles WHERE Code=N'ROLE-SEC030';"));
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.RolePermissions WHERE RoleId=@id;", P("@id", created.Value.RoleId)));
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM audit.AuditEvents WHERE EntityType=N'Role' AND EntityId=@id AND Action=N'role.create';",
            P("@id", created.Value.RoleId.ToString())));
    }

    [Fact]
    public async Task SEC031_MustChangePasswordWorkflowReplacesSecretAndClearsFlag()
    {
        var user = await CreateUserAsync("sec031", "SEC 031");
        var authentication = Authentication(Now);
        var login = await authentication.Service.LoginAsync(new LoginRequest("sec031", Password, Guid.NewGuid()), Token);
        Assert.True(login.Value.MustChangePassword);
        Assert.True((await authentication.Service.ChangePasswordAsync(Password, NewPassword, Guid.NewGuid(), Token)).IsSuccess);
        await authentication.Service.LogoutAsync(Guid.NewGuid(), Token);
        Assert.Equal(SecurityErrors.InvalidCredentials.Code,
            (await Authentication(Now).Service.LoginAsync(new LoginRequest("sec031", Password, Guid.NewGuid()), Token)).Error.Code);
        Assert.True((await Authentication(Now).Service.LoginAsync(new LoginRequest("sec031", NewPassword, Guid.NewGuid()), Token)).IsSuccess);
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserAccounts WHERE Id=@id AND MustChangePassword=0;", P("@id", user.UserId)));
    }

    [Fact]
    public async Task SEC032_UserRowVersionRejectsStaleConcurrentMutation()
    {
        var admin = Administration(Now);
        var user = await admin.Service.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "sec032", "SEC 032", Password, [fixture.ManagerRoleId]), Token);
        Assert.True(user.IsSuccess, Error(user));
        var first = await admin.Service.SetUserActiveAsync(new SetUserActiveRequest(
            Guid.NewGuid(), user.Value.UserId, false, user.Value.RowVersion), Token);
        Assert.True(first.IsSuccess, Error(first));
        var stale = await admin.Service.SetUserActiveAsync(new SetUserActiveRequest(
            Guid.NewGuid(), user.Value.UserId, true, user.Value.RowVersion), Token);
        Assert.Equal(SecurityErrors.ConcurrencyConflict.Code, stale.Error.Code);
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserAccounts WHERE Id=@id AND IsActive=1;", P("@id", user.Value.UserId)));
    }

    [Fact]
    public async Task SEC033_InvalidLengthsMissingRoleAndInactiveRoleFailBeforePartialCreation()
    {
        var admin = Administration(Now);
        var tooLongLogin = new string('L', 129);
        var tooLongName = new string('N', 257);
        var invalidLogin = await admin.Service.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), tooLongLogin, "SEC 033", Password, [fixture.ManagerRoleId]), Token);
        var invalidName = await admin.Service.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "sec033-name", tooLongName, Password, [fixture.ManagerRoleId]), Token);
        var missingRole = await admin.Service.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "sec033-no-role", "SEC 033", Password, []), Token);
        Assert.Equal(SecurityErrors.InvalidUser.Code, invalidLogin.Error.Code);
        Assert.Equal(SecurityErrors.InvalidUser.Code, invalidName.Error.Code);
        Assert.Equal(SecurityErrors.InvalidRoleSelection.Code, missingRole.Error.Code);

        var role = await admin.Service.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "ROLE-SEC033", "SEC 033", await PermissionIdsAsync(SecurityPermissions.CatalogView)), Token);
        Assert.True(role.IsSuccess, Error(role));
        await fixture.ExecuteAsync("UPDATE security.Roles SET IsActive=0 WHERE Id=@id;", P("@id", role.Value.RoleId));
        var inactiveRole = await admin.Service.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "sec033-inactive", "SEC 033", Password, [role.Value.RoleId]), Token);
        Assert.Equal(SecurityErrors.InvalidRoleSelection.Code, inactiveRole.Error.Code);
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserAccounts WHERE NormalizedLogin LIKE N'SEC033%';"));

        var bootstrap = await Authentication(Now).Service.BootstrapOwnerAsync(new BootstrapOwnerRequest(
            Guid.NewGuid(), tooLongLogin, "SEC 033", Password), Token);
        Assert.Equal(SecurityErrors.InvalidUser.Code, bootstrap.Error.Code);
        Assert.Equal(SecurityErrors.InvalidCredentials.Code,
            (await Authentication(Now).Service.LoginAsync(new LoginRequest(new string('X', 2048), "wrong", Guid.NewGuid()), Token)).Error.Code);
        Assert.Equal(SecurityErrors.InvalidRole.Code,
            (await admin.Service.CreateRoleAsync(new CreateSecurityRoleRequest(
                Guid.NewGuid(), "ROLE';DROP", "SEC 033", []), Token)).Error.Code);
    }

    [Fact]
    public async Task SEC034_DatabaseLockoutBlocksReauthenticationOfExistingSession()
    {
        await CreateUserAsync("sec034", "SEC 034");
        var active = Authentication(Now);
        Assert.True((await active.Service.LoginAsync(new LoginRequest("sec034", Password, Guid.NewGuid()), Token)).IsSuccess);
        var originalReauthentication = active.Session.User!.LastReauthenticatedAtUtc;
        active.Clock.Advance(TimeSpan.FromMinutes(1));
        var attackingSession = new CurrentSecuritySession();
        var attacker = new SqlAuthenticationService(
            fixture.Factory, fixture.Hasher, new UniqueRecoveryGenerator(), attackingSession, active.Clock);
        await LockAsync(attacker, "sec034");
        active.Clock.Advance(TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(30)));

        var reauthentication = await active.Service.ReauthenticateAsync(Password, Guid.NewGuid(), Token);
        Assert.Equal(SecurityErrors.AccountLocked.Code, reauthentication.Error.Code);
        Assert.Equal(originalReauthentication, active.Session.User!.LastReauthenticatedAtUtc);
        Assert.Equal(SecurityErrors.ReauthenticationRequired.Code,
            new PermissionAuthorizer(active.Session, active.Clock)
                .Authorize(SecurityPermissions.SecurityManageUsers, true).Error.Code);
    }

    [Fact]
    public async Task SEC035_ConcurrentFailuresAtLockoutThresholdCannotLoseIncrementOrBypassLock()
    {
        await CreateUserAsync("sec035", "SEC 035");
        await fixture.ExecuteAsync(
            "UPDATE security.UserAccounts SET FailedLoginCount=4, LockedUntilUtc=NULL WHERE NormalizedLogin=N'SEC035';");
        var first = Authentication(Now).Service;
        var second = Authentication(Now).Service;

        var results = await Task.WhenAll(
            first.LoginAsync(new LoginRequest("sec035", "wrong-password-a", Guid.NewGuid()), Token),
            second.LoginAsync(new LoginRequest("sec035", "wrong-password-b", Guid.NewGuid()), Token));

        Assert.All(results, result => Assert.True(result.IsFailure));
        Assert.All(results, result => Assert.Contains(
            result.Error.Code,
            new[] { SecurityErrors.InvalidCredentials.Code, SecurityErrors.AccountLocked.Code }));
        Assert.Equal(5, await fixture.ScalarAsync<int>(
            "SELECT FailedLoginCount FROM security.UserAccounts WHERE NormalizedLogin=N'SEC035';"));
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserAccounts WHERE NormalizedLogin=N'SEC035' AND LockedUntilUtc>@now;",
            P("@now", Now)));
        Assert.Equal(SecurityErrors.AccountLocked.Code,
            (await Authentication(Now).Service.LoginAsync(
                new LoginRequest("sec035", Password, Guid.NewGuid()), Token)).Error.Code);
    }

    private IServiceCollection Services() => new ServiceCollection()
        .AddPharmacySqlServerPersistence(fixture.ConnectionString)
        .AddLocalSqlSecurity();

    private CancellationToken Token => TestContext.Current.CancellationToken;

    private AuthenticationHarness Authentication(DateTime now)
    {
        var clock = new MutableSecurityClock(now);
        var session = new CurrentSecuritySession();
        return new AuthenticationHarness(
            new SqlAuthenticationService(fixture.Factory, fixture.Hasher, new UniqueRecoveryGenerator(), session, clock),
            session,
            clock);
    }

    private AdministrationHarness Administration(DateTime now)
    {
        var clock = new MutableSecurityClock(now);
        var session = new CurrentSecuritySession();
        session.Establish(new UserSessionView(
            fixture.OwnerUserId,
            SecurityGateSqlFixture.OwnerLogin,
            "Owner Security Gate",
            ["OWNER"],
            SecurityPermissions.All.ToHashSet(StringComparer.Ordinal),
            now,
            now), now);
        var authorizer = new PermissionAuthorizer(session, clock);
        return new AdministrationHarness(
            new SqlSecurityAdministrationService(
                fixture.Factory, fixture.Hasher, new UniqueRecoveryGenerator(), authorizer, session, clock),
            session,
            clock);
    }

    private async Task<SecurityUserSummaryView> CreateUserAsync(string login, string displayName)
    {
        var result = await Administration(Now).Service.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), login, displayName, Password, [fixture.ManagerRoleId]), Token);
        Assert.True(result.IsSuccess, Error(result));
        return result.Value;
    }

    private async Task<long[]> PermissionIdsAsync(params string[] codes)
    {
        var result = await Administration(Now).Service.GetPermissionsAsync(Token);
        Assert.True(result.IsSuccess, Error(result));
        return result.Value.Where(permission => codes.Contains(permission.Code, StringComparer.Ordinal))
            .Select(permission => permission.PermissionId).ToArray();
    }

    private async Task LockAsync(SqlAuthenticationService service, string login)
    {
        for (var attempt = 0; attempt < 5; attempt++)
            await service.LoginAsync(new LoginRequest(login, "wrong", Guid.NewGuid()), Token);
    }

    private static SqlParameter P(string name, object value) => new(name, value);
    private static string? Error<T>(Pharmacy.Domain.Common.Result<T> result) => result.IsFailure ? result.Error.Code : null;

    private sealed record AuthenticationHarness(
        SqlAuthenticationService Service,
        CurrentSecuritySession Session,
        MutableSecurityClock Clock);

    private sealed record AdministrationHarness(
        SqlSecurityAdministrationService Service,
        CurrentSecuritySession Session,
        MutableSecurityClock Clock);
}

public sealed class SecurityGateSqlFixture : IAsyncLifetime
{
    private const string Prefix = "Pharmacy_SecurityGate_Test_";
    public const string OwnerLogin = "owner.security.gate";
    public const string OwnerPassword = "Owner-Security-Gate-Password-2026!";
    private string serverConnection = string.Empty;

    internal string ConnectionString { get; private set; } = string.Empty;
    internal long OwnerUserId { get; private set; }
    internal long OwnerRoleId { get; private set; }
    internal long ManagerRoleId { get; private set; }
    internal SecurityTestDbContextFactory Factory { get; private set; } = null!;
    internal IPasswordHasher<UserAccountEntity> Hasher { get; } =
        new PasswordHasher<UserAccountEntity>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = 210_000
        }));
    private string DatabaseName { get; set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("PHARMACY_TEST_SQLSERVER_CONNECTION est requise.");
        serverConnection = new SqlConnectionStringBuilder(raw) { InitialCatalog = "master", Pooling = false }.ConnectionString;
        DatabaseName = Prefix + Guid.NewGuid().ToString("N")[..12];
        if (!Regex.IsMatch(DatabaseName, $"^{Prefix}[0-9a-f]{{12}}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Nom de base de test non sûr.");

        await ExecuteServerAsync($"CREATE DATABASE [{DatabaseName}];");
        try
        {
            ConnectionString = new SqlConnectionStringBuilder(serverConnection)
            {
                InitialCatalog = DatabaseName,
                ApplicationName = "Pharmacy.SecurityGate.Tests",
                Pooling = false
            }.ConnectionString;
            Factory = new SecurityTestDbContextFactory(ConnectionString);
            await using (var context = await Factory.CreateDbContextAsync())
            {
                await context.Database.MigrateAsync();
                if ((await context.Database.GetPendingMigrationsAsync()).Any())
                    throw new InvalidOperationException("Des migrations restent en attente dans la base SEC.");
            }

            var clock = new MutableSecurityClock(new DateTime(2026, 8, 17, 11, 0, 0, DateTimeKind.Utc));
            var session = new CurrentSecuritySession();
            var authentication = new SqlAuthenticationService(Factory, Hasher, new UniqueRecoveryGenerator(), session, clock);
            var bootstrap = await authentication.BootstrapOwnerAsync(new BootstrapOwnerRequest(
                Guid.NewGuid(), OwnerLogin, "Owner Security Gate", OwnerPassword));
            if (bootstrap.IsFailure) throw new InvalidOperationException(bootstrap.Error.Code);
            OwnerUserId = bootstrap.Value.UserId;
            OwnerRoleId = await ScalarAsync<long>("SELECT Id FROM security.Roles WHERE Code=N'OWNER';");
            ManagerRoleId = await ScalarAsync<long>("SELECT Id FROM security.Roles WHERE Code=N'MANAGER';");
        }
        catch
        {
            await DropAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync() => await DropAsync();

    internal async Task<T> ScalarAsync<T>(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync();
        if (value is null || value is DBNull) throw new InvalidOperationException("Valeur SQL attendue absente.");
        return (T)Convert.ChangeType(value, typeof(T));
    }

    internal async Task ExecuteAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
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

    private async Task DropAsync()
    {
        if (string.IsNullOrWhiteSpace(DatabaseName)) return;
        SqlConnection.ClearAllPools();
        await ExecuteServerAsync($"IF DB_ID(N'{DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}]; END;");
        DatabaseName = string.Empty;
    }
}

internal sealed class SecurityTestDbContextFactory(string connectionString) : IDbContextFactory<PharmacyDbContext>
{
    private readonly DbContextOptions<PharmacyDbContext> options = CreateOptions(connectionString);
    public PharmacyDbContext CreateDbContext() => new(options);
    public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDbContext());
    }
    private static DbContextOptions<PharmacyDbContext> CreateOptions(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<PharmacyDbContext>();
        builder.UsePharmacySqlServer(connectionString);
        return builder.Options;
    }
}

internal sealed class MutableSecurityClock(DateTime now) : ISecurityClock
{
    public DateTime UtcNow { get; private set; } = now;
    public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
}

internal sealed class UniqueRecoveryGenerator : IRecoveryCodeGenerator
{
    public IReadOnlyList<string> Generate(int count)
    {
        var prefix = Guid.NewGuid().ToString("N")[..12];
        return Enumerable.Range(1, count).Select(index => $"SEC-{prefix}-{index:0000}").ToArray();
    }
}
