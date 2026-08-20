using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Security;
using Pharmacy.Infrastructure.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class EmployeeAccessControlTests : IClassFixture<SecurityGateSqlFixture>
{
    private const string Password = "EMP-Access-Strong-Password-2026!";
    private static readonly DateTime Now = new(2026, 8, 20, 15, 0, 0, DateTimeKind.Utc);
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private readonly SecurityGateSqlFixture fixture;

    public EmployeeAccessControlTests(SecurityGateSqlFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task EMP001_ManagerPresetExists()
    {
        var manager = await RoleAsync(SecurityRolePresets.ManagerCode);
        Assert.True(manager.IsSystemRole);
        Assert.Equal("Manager", manager.Name);
        Assert.Contains(manager.Permissions, item => item.Code == SecurityPermissions.SecurityManageUsers);
        Assert.Contains(manager.Permissions, item => item.Code == SecurityPermissions.BackupCreate);
        Assert.DoesNotContain(manager.Permissions, item => item.Code == SecurityPermissions.BackupRestore);
    }

    [Fact]
    public async Task EMP002_VendeurPresetExists()
    {
        var seller = await RoleAsync(SecurityRolePresets.SellerCode);
        Assert.True(seller.IsSystemRole);
        Assert.Equal("Vendeur", seller.Name);
        Assert.Equal(SecurityRolePresets.SellerPermissions.Order(StringComparer.Ordinal),
            seller.Permissions.Select(item => item.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task EMP003_TwoDistinctManagerAccountsCanUseManagerRole()
    {
        var first = await CreateUserAsync("emp003-a", SecurityRolePresets.ManagerCode);
        var second = await CreateUserAsync("emp003-b", SecurityRolePresets.ManagerCode);
        var loginA = await LoginAsync("emp003-a");
        var loginB = await LoginAsync("emp003-b");
        Assert.NotEqual(first.UserId, second.UserId);
        Assert.NotEqual(loginA.Session.UserId, loginB.Session.UserId);
        Assert.Equal(loginA.Session.Permissions.Order(StringComparer.Ordinal), loginB.Session.Permissions.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task EMP004_ManagersHaveIndependentIdentitiesAndAudits()
    {
        var first = await CreateUserAsync("emp004-a", SecurityRolePresets.ManagerCode);
        var second = await CreateUserAsync("emp004-b", SecurityRolePresets.ManagerCode);
        await LoginAsync("emp004-a");
        await LoginAsync("emp004-b");
        Assert.Equal(2, await fixture.ScalarAsync<int>(
            "SELECT COUNT(DISTINCT ActorUserId) FROM audit.AuditEvents WHERE Action=N'login' AND ActorUserId IN (@a,@b);",
            P("@a", first.UserId), P("@b", second.UserId)));
    }

    [Fact]
    public async Task EMP005_VendeurReceivesDefaultSalePermissions()
    {
        await CreateUserAsync("emp005", SecurityRolePresets.SellerCode);
        var login = await LoginAsync("emp005");
        Assert.Contains(SecurityPermissions.SalesCreate, login.Session.Permissions);
        Assert.Contains(SecurityPermissions.SalesViewHistory, login.Session.Permissions);
    }

    [Fact]
    public async Task EMP006_VendeurDoesNotReceivePurchasingPermissionsByDefault()
    {
        await CreateUserAsync("emp006", SecurityRolePresets.SellerCode);
        var login = await LoginAsync("emp006");
        Assert.DoesNotContain(SecurityPermissions.PurchasingView, login.Session.Permissions);
        Assert.DoesNotContain(SecurityPermissions.PurchasingReceive, login.Session.Permissions);
    }

    [Fact]
    public async Task EMP007_VendeurDoesNotReceiveReportingOrAdminPermissionsByDefault()
    {
        await CreateUserAsync("emp007", SecurityRolePresets.SellerCode);
        var permissions = (await LoginAsync("emp007")).Session.Permissions;
        Assert.DoesNotContain(SecurityPermissions.ReportsView, permissions);
        Assert.DoesNotContain(SecurityPermissions.SecurityManageUsers, permissions);
        Assert.DoesNotContain(SecurityPermissions.SecurityManageRoles, permissions);
    }

    [Fact]
    public async Task EMP008_UserAllowOverrideAddsPermission()
    {
        var user = await CreateUserAsync("emp008", SecurityRolePresets.SellerCode);
        await SetOverridesAsync(user, (SecurityPermissions.CustomersManage, SecurityPermissionOverrideEffect.Allow));
        Assert.Contains(SecurityPermissions.CustomersManage, (await LoginAsync("emp008")).Session.Permissions);
    }

    [Fact]
    public async Task EMP009_UserDenyOverrideRemovesInheritedPermission()
    {
        var user = await CreateUserAsync("emp009", SecurityRolePresets.SellerCode);
        await SetOverridesAsync(user, (SecurityPermissions.SalesViewHistory, SecurityPermissionOverrideEffect.Deny));
        Assert.DoesNotContain(SecurityPermissions.SalesViewHistory, (await LoginAsync("emp009")).Session.Permissions);
    }

    [Fact]
    public void EMP010_DenyOverridesRoleGrant()
    {
        var permissions = EffectivePermissionResolver.Resolve(
            [SecurityPermissions.SalesCreate],
            [new(SecurityPermissions.SalesCreate, SecurityPermissionOverrideEffect.Allow),
             new(SecurityPermissions.SalesCreate, SecurityPermissionOverrideEffect.Deny)]);
        Assert.DoesNotContain(SecurityPermissions.SalesCreate, permissions);
    }

    [Fact]
    public async Task EMP011_RemovingOverrideRestoresRoleBehavior()
    {
        var user = await CreateUserAsync("emp011", SecurityRolePresets.SellerCode);
        await SetOverridesAsync(user, (SecurityPermissions.SalesCreate, SecurityPermissionOverrideEffect.Deny));
        Assert.DoesNotContain(SecurityPermissions.SalesCreate, (await LoginAsync("emp011")).Session.Permissions);
        var refreshed = Success(await Administration().Service.GetUsersAsync(Token)).Single(item => item.UserId == user.UserId);
        await SetOverridesAsync(refreshed, (SecurityPermissions.ReportsView, SecurityPermissionOverrideEffect.Allow));
        var replaced = await LoginAsync("emp011");
        Assert.Contains(SecurityPermissions.SalesCreate, replaced.Session.Permissions);
        Assert.Contains(SecurityPermissions.ReportsView, replaced.Session.Permissions);
        refreshed = Success(await Administration().Service.GetUsersAsync(Token)).Single(item => item.UserId == user.UserId);
        await SetOverridesAsync(refreshed);
        var reset = await LoginAsync("emp011");
        Assert.Contains(SecurityPermissions.SalesCreate, reset.Session.Permissions);
        Assert.DoesNotContain(SecurityPermissions.ReportsView, reset.Session.Permissions);
    }

    [Fact]
    public async Task EMP012_OverridesAffectOnlyTargetUser()
    {
        var target = await CreateUserAsync("emp012-a", SecurityRolePresets.SellerCode);
        await CreateUserAsync("emp012-b", SecurityRolePresets.SellerCode);
        await SetOverridesAsync(target, (SecurityPermissions.PurchasingView, SecurityPermissionOverrideEffect.Allow));
        Assert.Contains(SecurityPermissions.PurchasingView, (await LoginAsync("emp012-a")).Session.Permissions);
        Assert.DoesNotContain(SecurityPermissions.PurchasingView, (await LoginAsync("emp012-b")).Session.Permissions);
    }

    [Fact]
    public async Task EMP013_RoleEditsStillAffectAllMembersAsExpected()
    {
        var admin = Administration();
        var catalogId = (await PermissionAsync(SecurityPermissions.CatalogView)).PermissionId;
        var inventoryId = (await PermissionAsync(SecurityPermissions.InventoryView)).PermissionId;
        var role = Success(await admin.Service.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "EMP013-STOCK", "Responsable stock", [catalogId]), Token));
        await CreateUserAsync("emp013-a", role.RoleId);
        await CreateUserAsync("emp013-b", role.RoleId);
        Success(await admin.Service.UpdateRoleAsync(new UpdateSecurityRoleRequest(
            Guid.NewGuid(), role.RoleId, role.Name, [inventoryId], role.RowVersion), Token));
        Assert.Contains(SecurityPermissions.InventoryView, (await LoginAsync("emp013-a")).Session.Permissions);
        Assert.Contains(SecurityPermissions.InventoryView, (await LoginAsync("emp013-b")).Session.Permissions);
    }

    [Fact]
    public async Task EMP014_EffectivePermissionsPersistAfterNewDbContext()
    {
        var user = await CreateUserAsync("emp014", SecurityRolePresets.SellerCode);
        await SetOverridesAsync(user, (SecurityPermissions.ReportsView, SecurityPermissionOverrideEffect.Allow));
        await using var context = await fixture.Factory.CreateDbContextAsync(Token);
        Assert.Equal(1, await context.UserPermissionOverrides.AsNoTracking().CountAsync(
            item => item.UserAccountId == user.UserId && item.Effect == "Allow", Token));
        Assert.Contains(SecurityPermissions.ReportsView, (await LoginAsync("emp014")).Session.Permissions);
    }

    [Fact]
    public async Task EMP015_EffectivePermissionsPersistAfterFullProviderRebuild()
    {
        var user = await CreateUserAsync("emp015", SecurityRolePresets.SellerCode);
        await SetOverridesAsync(user, (SecurityPermissions.ReportsView, SecurityPermissionOverrideEffect.Allow));
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<PharmacyDbContext>>(fixture.Factory);
        services.AddLocalSqlSecurity();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var authentication = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var login = await authentication.LoginAsync(new LoginRequest("emp015", Password, Guid.NewGuid()), Token);
        Assert.True(login.IsSuccess, login.IsFailure ? login.Error.Code : null);
        Assert.Contains(SecurityPermissions.ReportsView, login.Value.Session.Permissions);
    }

    [Fact]
    public void EMP016_UnauthorizedModuleHiddenFromSidebar()
    {
        var sidebar = DesktopText("Components", "Layout", "AppSidebar.razor");
        Assert.Contains(".Where(Allowed)", sidebar, StringComparison.Ordinal);
        Assert.DoesNotContain("Allowed(item)\n                            ?", sidebar, StringComparison.Ordinal);
    }

    [Fact]
    public void EMP017_AuthorizedModuleVisible()
    {
        var sidebar = DesktopText("Components", "Layout", "AppSidebar.razor");
        Assert.Contains("@foreach (var item in group)", sidebar, StringComparison.Ordinal);
        Assert.Contains("Items\n        .Where(Allowed)\n        .GroupBy", sidebar, StringComparison.Ordinal);
    }

    [Fact]
    public void EMP018_UnauthorizedHomeShortcutHidden()
    {
        var home = DesktopText("Components", "Pages", "Home.razor");
        Assert.Contains("QuickActions.Where(CanAccess)", home, StringComparison.Ordinal);
        Assert.Contains("Authorizer.Authorize(action.RequiredPermission)", home, StringComparison.Ordinal);
    }

    [Fact]
    public void EMP019_DirectNavigationToUnauthorizedModuleRefused()
    {
        var root = DesktopText("Components", "RootComponent.razor");
        var guard = DesktopText("Components", "Security", "AuthorizedRouteView.razor");
        Assert.Contains("<AuthorizedRouteView", root, StringComparison.Ordinal);
        Assert.Contains("Authorizer.Authorize(item.RequiredPermission)", guard, StringComparison.Ordinal);
        Assert.Contains("<RouteView", guard, StringComparison.Ordinal);
        Assert.Contains("Accès refusé", guard, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EMP020_BackendCommandDeniedDespiteManipulatedUi()
    {
        var session = SellerSession();
        var service = new SqlSecurityAdministrationService(
            new ThrowingFactory(), fixture.Hasher, new UniqueRecoveryGenerator(),
            new PermissionAuthorizer(session, new MutableSecurityClock(Now)), session, new MutableSecurityClock(Now));
        var result = await service.SetUserPermissionOverridesAsync(new SetUserPermissionOverridesRequest(
            Guid.NewGuid(), 1, [], new byte[8]), Token);
        Assert.Equal(SecurityErrors.PermissionDenied.Code, result.Error.Code);
    }

    [Fact]
    public async Task EMP021_ManagerCanManageUsers()
    {
        await CreateUserAsync("emp021", SecurityRolePresets.ManagerCode);
        var login = await LoginAsync("emp021");
        Assert.True(Authorizer(login.Session).Authorize(SecurityPermissions.SecurityManageUsers).IsSuccess);
    }

    [Fact]
    public async Task EMP022_VendeurCannotManageUsers()
    {
        await CreateUserAsync("emp022", SecurityRolePresets.SellerCode);
        var login = await LoginAsync("emp022");
        Assert.Equal(SecurityErrors.PermissionDenied.Code,
            Authorizer(login.Session).Authorize(SecurityPermissions.SecurityManageUsers).Error.Code);
    }

    [Fact]
    public async Task EMP023_ConcurrentUserPermissionUpdateControlled()
    {
        var stale = await CreateUserAsync("emp023", SecurityRolePresets.SellerCode);
        await SetOverridesAsync(stale, (SecurityPermissions.ReportsView, SecurityPermissionOverrideEffect.Allow));
        var permission = await PermissionAsync(SecurityPermissions.PurchasingView);
        var conflict = await Administration().Service.SetUserPermissionOverridesAsync(new SetUserPermissionOverridesRequest(
            Guid.NewGuid(), stale.UserId,
            [new(permission.PermissionId, SecurityPermissionOverrideEffect.Allow)], stale.RowVersion), Token);
        Assert.Equal(SecurityErrors.ConcurrencyConflict.Code, conflict.Error.Code);
    }

    [Fact]
    public async Task EMP024_DisabledUserRemainsDenied()
    {
        var user = await CreateUserAsync("emp024", SecurityRolePresets.SellerCode);
        var disabled = await Administration().Service.SetUserActiveAsync(
            new SetUserActiveRequest(Guid.NewGuid(), user.UserId, false, user.RowVersion), Token);
        Assert.True(disabled.IsSuccess, disabled.IsFailure ? disabled.Error.Code : null);
        var login = await Authentication().LoginAsync(new LoginRequest("emp024", Password, Guid.NewGuid()), Token);
        Assert.True(login.IsFailure);
    }

    [Fact]
    public async Task EMP025_AuditRecordsRoleAndPermissionChangesWithoutSecrets()
    {
        var user = await CreateUserAsync("emp025", SecurityRolePresets.SellerCode);
        await SetOverridesAsync(user, (SecurityPermissions.ReportsView, SecurityPermissionOverrideEffect.Allow));
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM audit.AuditEvents WHERE EntityId=@id AND Action=N'user.permission_overrides.set';",
            P("@id", user.UserId.ToString())));
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM audit.AuditEventDetails d JOIN audit.AuditEvents e ON e.Id=d.AuditEventId WHERE e.EntityId=@id AND (d.OldValueRedacted LIKE N'%Password%' OR d.NewValueRedacted LIKE N'%Recovery%');",
            P("@id", user.UserId.ToString())));
    }

    [Fact]
    public async Task EMP026_CustomRolesRemainSupported()
    {
        var permission = await PermissionAsync(SecurityPermissions.InventoryView);
        var role = await Administration().Service.CreateRoleAsync(new CreateSecurityRoleRequest(
            Guid.NewGuid(), "EMP026-CUSTOM", "Responsable stock", [permission.PermissionId]), Token);
        Assert.True(role.IsSuccess, role.IsFailure ? role.Error.Code : null);
        Assert.False(role.Value.IsSystemRole);
    }

    [Fact]
    public async Task EMP027_OwnerTechnicalSafeguardsRemainIntact()
    {
        var admin = Administration();
        var owner = Success(await admin.Service.GetUsersAsync(Token)).Single(item => item.UserId == fixture.OwnerUserId);
        var disable = await admin.Service.SetUserActiveAsync(
            new SetUserActiveRequest(Guid.NewGuid(), owner.UserId, false, owner.RowVersion), Token);
        Assert.True(disable.IsFailure);
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserAccounts WHERE Id=@id AND IsActive=1;", P("@id", owner.UserId)));
        var ownerRole = await RoleAsync(SecurityRolePresets.OwnerCode);
        var secondOwner = await admin.Service.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), "emp027-owner", "Second propriétaire", Password, [ownerRole.RoleId]), Token);
        Assert.Equal(SecurityErrors.SystemRoleProtected.Code, secondOwner.Error.Code);
        Assert.Equal(SecurityErrors.SystemRoleProtected.Code, (await admin.Service.UpdateRoleAsync(
            new UpdateSecurityRoleRequest(Guid.NewGuid(), ownerRole.RoleId, ownerRole.Name, [], ownerRole.RowVersion), Token)).Error.Code);
    }

    [Fact]
    public async Task EMP028_BrandManagerAccessRemainsValid()
    {
        await CreateUserAsync("emp028", SecurityRolePresets.ManagerCode);
        var session = (await LoginAsync("emp028")).Session;
        Assert.Contains(SecurityPermissions.SecurityManageUsers, session.Permissions);
        Assert.Contains("SecurityPermissions.SecurityManageUsers", InfrastructureText("Branding", "SqlPharmacyBrandingService.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void EMP029_CustomJargonDoesNotReturn()
    {
        var visibleSources = new[]
        {
            DesktopText("Components", "Layout", "AppTopBar.razor"),
            DesktopText("Components", "Layout", "AppSidebar.razor"),
            DesktopText("Components", "Pages", "Home.razor")
        };
        Assert.All(visibleSources, source =>
        {
            Assert.DoesNotContain(">OWNER<", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(">SELLER<", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(">Sprint<", source, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task EMP030_ExistingPermissionCatalogRemainsCoherent()
    {
        Assert.Equal(28, SecurityPermissions.All.Count);
        Assert.Equal(28, SecurityPermissions.All.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(28, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM security.Permissions;"));
        Assert.All(SecurityRolePresets.SellerPermissions, code => Assert.Contains(code, SecurityPermissions.All));
        Assert.All(SecurityRolePresets.ManagerPermissions, code => Assert.Contains(code, SecurityPermissions.All));
    }

    private AdministrationHarness Administration()
    {
        var clock = new MutableSecurityClock(Now);
        var session = new CurrentSecuritySession();
        session.Establish(new UserSessionView(
            fixture.OwnerUserId, SecurityGateSqlFixture.OwnerLogin, "Manager principal",
            [SecurityRolePresets.OwnerCode], SecurityPermissions.All.ToHashSet(StringComparer.Ordinal), Now, Now), Now);
        return new(new SqlSecurityAdministrationService(
            fixture.Factory, fixture.Hasher, new UniqueRecoveryGenerator(),
            new PermissionAuthorizer(session, clock), session, clock));
    }

    private SqlAuthenticationService Authentication()
    {
        var clock = new MutableSecurityClock(Now);
        return new SqlAuthenticationService(
            fixture.Factory, fixture.Hasher, new UniqueRecoveryGenerator(), new CurrentSecuritySession(), clock);
    }

    private async Task<AuthenticationResultView> LoginAsync(string login) =>
        Success(await Authentication().LoginAsync(new LoginRequest(login, Password, Guid.NewGuid()), Token));

    private async Task<SecurityUserSummaryView> CreateUserAsync(string login, string roleCode)
    {
        var role = await RoleAsync(roleCode);
        return await CreateUserAsync(login, role.RoleId);
    }

    private async Task<SecurityUserSummaryView> CreateUserAsync(string login, long roleId) =>
        Success(await Administration().Service.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), login, login.ToUpperInvariant(), Password, [roleId]), Token));

    private async Task<SecurityUserSummaryView> SetOverridesAsync(
        SecurityUserSummaryView user,
        params (string Code, SecurityPermissionOverrideEffect Effect)[] values)
    {
        var permissions = new List<SecurityPermissionOverrideSelection>();
        foreach (var value in values)
            permissions.Add(new((await PermissionAsync(value.Code)).PermissionId, value.Effect));
        return Success(await Administration().Service.SetUserPermissionOverridesAsync(
            new SetUserPermissionOverridesRequest(Guid.NewGuid(), user.UserId, permissions, user.RowVersion), Token));
    }

    private async Task<SecurityRoleView> RoleAsync(string code) =>
        Success(await Administration().Service.GetRolesAsync(Token)).Single(item => item.Code == code);

    private async Task<SecurityPermissionView> PermissionAsync(string code) =>
        Success(await Administration().Service.GetPermissionsAsync(Token)).Single(item => item.Code == code);

    private static PermissionAuthorizer Authorizer(UserSessionView user)
    {
        var clock = new MutableSecurityClock(Now);
        var session = new CurrentSecuritySession();
        session.Establish(user, Now);
        return new PermissionAuthorizer(session, clock);
    }

    private static CurrentSecuritySession SellerSession()
    {
        var session = new CurrentSecuritySession();
        session.Establish(new UserSessionView(
            999, "seller", "Vendeur", [SecurityRolePresets.SellerCode],
            SecurityRolePresets.SellerPermissions.ToHashSet(StringComparer.Ordinal), Now, Now), Now);
        return session;
    }

    private static T Success<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    private static Microsoft.Data.SqlClient.SqlParameter P(string name, object value) => new(name, value);
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string DesktopText(params string[] segments) => File.ReadAllText(Path.Combine(
        new[] { RepositoryRoot, "src", "Pharmacy.Desktop" }.Concat(segments).ToArray()));
    private static string InfrastructureText(params string[] segments) => File.ReadAllText(Path.Combine(
        new[] { RepositoryRoot, "src", "Pharmacy.Infrastructure" }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx"))) return directory.FullName;
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException("Racine du dépôt introuvable.");
    }

    private sealed record AdministrationHarness(SqlSecurityAdministrationService Service);

    private sealed class ThrowingFactory : IDbContextFactory<PharmacyDbContext>
    {
        public PharmacyDbContext CreateDbContext() => throw new InvalidOperationException("Database access was not expected.");
        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Database access was not expected.");
    }
}
