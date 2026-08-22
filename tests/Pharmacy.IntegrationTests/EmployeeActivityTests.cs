using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class EmployeeActivityTests :
    IClassFixture<SecurityGateSqlFixture>,
    IClassFixture<SqlSalesGateFixture>
{
    private const string Password = "EMP-Activity-Strong-Password-2026!";
    private static readonly DateTime Now = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private readonly SecurityGateSqlFixture fixture;
    private readonly SqlSalesGateFixture salesFixture;

    public EmployeeActivityTests(SecurityGateSqlFixture fixture, SqlSalesGateFixture salesFixture)
    {
        this.fixture = fixture;
        this.salesFixture = salesFixture;
    }

    [Fact]
    public async Task EMP031_SuccessfulLoginCreatesUserSession()
    {
        var user = await CreateUserAsync("emp031", SecurityRolePresets.SellerCode);
        var authentication = Authentication(Now);
        var login = Success(await authentication.Service.LoginAsync(
            new LoginRequest("emp031", Password, Guid.NewGuid()), Token));

        Assert.NotNull(login.Session.PersistedSessionId);
        Assert.Equal(1, await CountAsync(
            "security.UserSessions",
            "UserAccountId=@user AND Status=N'Open'",
            P("@user", user.UserId)));
    }

    [Fact]
    public async Task EMP032_FailedLoginCreatesNoSession()
    {
        var user = await CreateUserAsync("emp032", SecurityRolePresets.SellerCode);
        var result = await Authentication(Now).Service.LoginAsync(
            new LoginRequest("emp032", "wrong-password", Guid.NewGuid()), Token);

        Assert.True(result.IsFailure);
        Assert.Equal(0, await CountAsync("security.UserSessions", "UserAccountId=@user", P("@user", user.UserId)));
    }

    [Fact]
    public async Task EMP033_DisabledOrLockedLoginCreatesNoSession()
    {
        var user = await CreateUserAsync("emp033", SecurityRolePresets.SellerCode);
        Success(await Administration().SetUserActiveAsync(
            new SetUserActiveRequest(Guid.NewGuid(), user.UserId, false, user.RowVersion), Token));

        Assert.True((await Authentication(Now).Service.LoginAsync(
            new LoginRequest("emp033", Password, Guid.NewGuid()), Token)).IsFailure);
        Assert.Equal(0, await CountAsync("security.UserSessions", "UserAccountId=@user", P("@user", user.UserId)));
    }

    [Fact]
    public async Task EMP034_ExplicitLogoutClosesSession()
    {
        var user = await CreateUserAsync("emp034", SecurityRolePresets.SellerCode);
        var authentication = Authentication(Now);
        Success(await authentication.Service.LoginAsync(new LoginRequest("emp034", Password, Guid.NewGuid()), Token));
        authentication.Clock.Advance(TimeSpan.FromMinutes(25));
        Success(await authentication.Service.LogoutAsync(Guid.NewGuid(), Token));

        Assert.Equal(1, await CountAsync(
            "security.UserSessions",
            "UserAccountId=@user AND Status=N'Closed' AND EndReason=N'ExplicitLogout' AND EndedAtUtc=@ended",
            P("@user", user.UserId), P("@ended", Now.AddMinutes(25))));
    }

    [Fact]
    public async Task EMP035_SessionDurationIsExact()
    {
        var user = await CreateUserAsync("emp035", SecurityRolePresets.SellerCode);
        var authentication = Authentication(Now);
        Success(await authentication.Service.LoginAsync(new LoginRequest("emp035", Password, Guid.NewGuid()), Token));
        authentication.Clock.Advance(TimeSpan.FromHours(6).Add(TimeSpan.FromMinutes(12)));
        Success(await authentication.Service.LogoutAsync(Guid.NewGuid(), Token));

        var detail = Success(await ManagerReader().GetEmployeeAsync(user.UserId, Day(), Token));
        Assert.Equal(TimeSpan.FromHours(6).Add(TimeSpan.FromMinutes(12)), Assert.Single(detail.Sessions).Duration);
    }

    [Fact]
    public async Task EMP036_InterruptedSessionKeepsUnknownEndTime()
    {
        var user = await CreateUserAsync("emp036", SecurityRolePresets.SellerCode);
        Success(await Authentication(Now).Service.LoginAsync(new LoginRequest("emp036", Password, Guid.NewGuid()), Token));
        Success(await Authentication(Now.AddHours(1)).Service.LoginAsync(new LoginRequest("emp036", Password, Guid.NewGuid()), Token));

        Assert.Equal(1, await CountAsync(
            "security.UserSessions",
            "UserAccountId=@user AND Status=N'Interrupted' AND EndedAtUtc IS NULL AND EndReason=N'PreviousSessionDetected'",
            P("@user", user.UserId)));
        Assert.Equal(1, await CountAsync(
            "security.UserSessions", "UserAccountId=@user AND Status=N'Open'", P("@user", user.UserId)));
    }

    [Fact]
    public async Task EMP037_LastLoginUsesSuccessfulAuthenticationOnly()
    {
        var user = await CreateUserAsync("emp037", SecurityRolePresets.SellerCode);
        await Authentication(Now).Service.LoginAsync(new LoginRequest("emp037", "wrong", Guid.NewGuid()), Token);
        Success(await Authentication(Now.AddMinutes(1)).Service.LoginAsync(new LoginRequest("emp037", Password, Guid.NewGuid()), Token));
        await Authentication(Now.AddMinutes(2)).Service.LoginAsync(new LoginRequest("emp037", "wrong", Guid.NewGuid()), Token);

        Assert.Equal(Now.AddMinutes(1), await fixture.ScalarAsync<DateTime>(
            "SELECT LastLoginAtUtc FROM security.UserAccounts WHERE Id=@user;", P("@user", user.UserId)));
    }

    [Fact]
    public async Task EMP038_SaleIsAttributedToUserA()
    {
        var userId = await CreateSalesActorAsync("emp038-a");
        var prepared = await salesFixture.PrepareAsync("EMP038-A", price: 120m);
        var result = Success(await salesFixture.FinalizeAsync(
            prepared with { Request = prepared.Request with { PerformedByUserAccountId = userId } }, Token));

        Assert.Equal(userId, await salesFixture.ScalarAsync<long>(
            "SELECT FinalizedByUserAccountId FROM sales.Sales WHERE Id=@id;", P("@id", result.SaleId)));
    }

    [Fact]
    public async Task EMP039_SaleIsAttributedIndependentlyToUserB()
    {
        var userA = await CreateSalesActorAsync("emp039-a");
        var userB = await CreateSalesActorAsync("emp039-b");
        var preparedA = await salesFixture.PrepareAsync("EMP039-A", price: 100m);
        var preparedB = await salesFixture.PrepareAsync("EMP039-B", price: 80m);
        var saleA = Success(await salesFixture.FinalizeAsync(
            preparedA with { Request = preparedA.Request with { PerformedByUserAccountId = userA } }, Token));
        var saleB = Success(await salesFixture.FinalizeAsync(
            preparedB with { Request = preparedB.Request with { PerformedByUserAccountId = userB } }, Token));

        Assert.Equal(userA, await SalesGateActorAsync(saleA.SaleId));
        Assert.Equal(userB, await SalesGateActorAsync(saleB.SaleId));
    }

    [Fact]
    public async Task EMP040_ConcurrentSalesDoNotCrossActors()
    {
        var userA = await CreateSalesActorAsync("emp040-a");
        var userB = await CreateSalesActorAsync("emp040-b");
        var preparedA = await salesFixture.PrepareAsync("EMP040-A", price: 70m);
        var preparedB = await salesFixture.PrepareAsync("EMP040-B", price: 90m);
        var results = await Task.WhenAll(
            salesFixture.FinalizeAsync(
                preparedA with { Request = preparedA.Request with { PerformedByUserAccountId = userA } }, Token),
            salesFixture.FinalizeAsync(
                preparedB with { Request = preparedB.Request with { PerformedByUserAccountId = userB } }, Token));

        if (results[0].IsFailure)
        {
            Assert.Equal("sales.stock.concurrency_conflict", results[0].Error.Code);
            results[0] = await salesFixture.FinalizeAsync(
                preparedA with { Request = preparedA.Request with { PerformedByUserAccountId = userA } }, Token);
        }
        if (results[1].IsFailure)
        {
            Assert.Equal("sales.stock.concurrency_conflict", results[1].Error.Code);
            results[1] = await salesFixture.FinalizeAsync(
                preparedB with { Request = preparedB.Request with { PerformedByUserAccountId = userB } }, Token);
        }

        Assert.All(results, result => Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null));
        Assert.Equal(userA, await SalesGateActorAsync(results[0].Value.SaleId));
        Assert.Equal(userB, await SalesGateActorAsync(results[1].Value.SaleId));
    }

    [Fact]
    public async Task EMP041_EmployeeSalesCountIsExact()
    {
        var user = await CreateUserAsync("emp041", SecurityRolePresets.SellerCode);
        await PersistSaleAsync("EMP041-1", user.UserId, Now, 100m);
        await PersistSaleAsync("EMP041-2", user.UserId, Now.AddMinutes(1), 150m);
        await PersistSaleAsync("EMP041-3", user.UserId, Now.AddMinutes(2), 250m);

        Assert.Equal(3, (await SalesSummaryAsync(user.UserId, Day())).SalesCount);
    }

    [Fact]
    public async Task EMP042_EmployeeSalesAmountIsExact()
    {
        var user = await CreateUserAsync("emp042", SecurityRolePresets.SellerCode);
        await PersistSaleAsync("EMP042-1", user.UserId, Now, 100m);
        await PersistSaleAsync("EMP042-2", user.UserId, Now.AddMinutes(1), 150m);
        await PersistSaleAsync("EMP042-3", user.UserId, Now.AddMinutes(2), 250m);

        Assert.Equal(500m, (await SalesSummaryAsync(user.UserId, Day())).GrossSalesAmount);
    }

    [Fact]
    public async Task EMP043_PaymentsAndLineCountsDoNotMultiplyTotals()
    {
        var user = await CreateUserAsync("emp043", SecurityRolePresets.SellerCode);
        var sale = await PersistSaleAsync("EMP043", user.UserId, Now, 500m, lineCount: 3);
        await InsertPaymentAsync(sale, "EMP043-P1", 250m);
        await InsertPaymentAsync(sale, "EMP043-P2", 250m);

        var summary = await SalesSummaryAsync(user.UserId, Day());
        Assert.Equal(1, summary.SalesCount);
        Assert.Equal(500m, summary.GrossSalesAmount);
        Assert.Contains("context.Sales.AsNoTracking()", InfrastructureText("Security", "SqlEmployeeActivityReader.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EMP044_ReturnPerformerIsIndependentFromSalePerformer()
    {
        var seller = await CreateUserAsync("emp044-sale", SecurityRolePresets.SellerCode);
        var returner = await CreateUserAsync("emp044-return", SecurityRolePresets.ManagerCode);
        var sale = await PersistSaleAsync("EMP044-SALE", seller.UserId, Now, 200m);
        await PersistReturnAsync("EMP044-RETURN", sale, returner.UserId, Now.AddMinutes(5), 50m);

        Assert.Equal(0, (await SalesSummaryAsync(seller.UserId, Day())).ReturnsPerformedCount);
        Assert.Equal(1, (await SalesSummaryAsync(returner.UserId, Day())).ReturnsPerformedCount);
    }

    [Fact]
    public async Task EMP045_CreditSettlementPerformerIsAvailableInActivity()
    {
        var user = await CreateUserAsync("emp045", SecurityRolePresets.SellerCode);
        await PersistAuditAsync(user.UserId, "customers", "Customer", "45", "credit.payment", Now, Guid.NewGuid());

        var activity = Success(await ManagerReader().GetEmployeeAsync(user.UserId, Day(), Token)).Activity;
        Assert.Contains(activity, item => item.Category == "Crédit" && item.Action == "Règlement crédit enregistré" && item.BusinessReference == "45");
    }

    [Fact]
    public async Task EMP046_AuthorizedInventoryAndPurchasingActionsAppearInActivity()
    {
        var user = await CreateUserAsync("emp046", SecurityRolePresets.SellerCode);
        await PersistAuditAsync(user.UserId, "inventory", "InventorySession", "INV-46", "inventory.finalize", Now, Guid.NewGuid());
        await PersistAuditAsync(user.UserId, "purchasing", "GoodsReceipt", "REC-46", "receipt.finalize", Now.AddMinutes(1), Guid.NewGuid());

        var activity = Success(await ManagerReader().GetEmployeeAsync(user.UserId, Day(), Token)).Activity;
        Assert.Contains(activity, item => item.Category == "Stock" && item.BusinessReference == "INV-46");
        Assert.Contains(activity, item => item.Category == "Achats" && item.BusinessReference == "REC-46");
    }

    [Fact]
    public async Task EMP047_ManagerCanReadEmployeeSummaries()
    {
        await CreateUserAsync("emp047", SecurityRolePresets.SellerCode);
        var result = await ManagerReader().GetSummariesAsync(Day(), Token);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
    }

    [Fact]
    public async Task EMP048_SellerCannotReadAllEmployeeSummaries()
    {
        var seller = await CreateUserAsync("emp048", SecurityRolePresets.SellerCode);
        var result = await Reader(SellerSession(seller)).GetSummariesAsync(Day(), Token);
        Assert.Equal(SecurityErrors.PermissionDenied.Code, result.Error.Code);
    }

    [Fact]
    public async Task EMP049_SellerCanReadOwnHistory()
    {
        var seller = await CreateUserAsync("emp049", SecurityRolePresets.SellerCode);
        await PersistSaleAsync("EMP049", seller.UserId, Now, 75m);
        var result = await Reader(SellerSession(seller)).GetEmployeeAsync(seller.UserId, Day(), Token);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        Assert.Single(result.Value.Sales);
    }

    [Fact]
    public async Task EMP050_SellerCannotReadAnotherSellerHistory()
    {
        var sellerA = await CreateUserAsync("emp050-a", SecurityRolePresets.SellerCode);
        var sellerB = await CreateUserAsync("emp050-b", SecurityRolePresets.SellerCode);
        var result = await Reader(SellerSession(sellerA)).GetEmployeeAsync(sellerB.UserId, Day(), Token);
        Assert.Equal(SecurityErrors.PermissionDenied.Code, result.Error.Code);
    }

    [Fact]
    public async Task EMP051_DisabledEmployeeHistoryRemainsReadable()
    {
        var seller = await CreateUserAsync("emp051", SecurityRolePresets.SellerCode);
        await PersistSaleAsync("EMP051", seller.UserId, Now, 60m);
        Success(await Administration().SetUserActiveAsync(
            new SetUserActiveRequest(Guid.NewGuid(), seller.UserId, false, seller.RowVersion), Token));

        var detail = Success(await ManagerReader().GetEmployeeAsync(seller.UserId, Day(), Token));
        Assert.False(detail.Summary.IsActive);
        Assert.Single(detail.Sales);
    }

    [Fact]
    public async Task EMP052_RenamedEmployeeRemainsLinkedToOldSales()
    {
        var seller = await CreateUserAsync("emp052", SecurityRolePresets.SellerCode);
        await PersistSaleAsync("EMP052", seller.UserId, Now, 99m);
        await fixture.ExecuteAsync(
            "UPDATE security.UserAccounts SET DisplayName=N'Nouveau nom', UpdatedAtUtc=@now WHERE Id=@id;",
            P("@now", Now.AddMinutes(1)), P("@id", seller.UserId));

        var detail = Success(await ManagerReader().GetEmployeeAsync(seller.UserId, Day(), Token));
        Assert.Equal("Nouveau nom", detail.Summary.DisplayName);
        Assert.Single(detail.Sales);
    }

    [Fact]
    public async Task EMP053_TimelineOrderingIsDeterministic()
    {
        var user = await CreateUserAsync("emp053", SecurityRolePresets.SellerCode);
        await PersistAuditAsync(user.UserId, "inventory", "InventorySession", "B", "inventory.finalize", Now, Guid.NewGuid());
        await PersistAuditAsync(user.UserId, "inventory", "InventorySession", "A", "inventory.finalize", Now, Guid.NewGuid());

        var first = Success(await ManagerReader().GetEmployeeAsync(user.UserId, Day(), Token)).Activity;
        var second = Success(await ManagerReader().GetEmployeeAsync(user.UserId, Day(), Token)).Activity;
        Assert.Equal(first.Select(item => item.ActivityId), second.Select(item => item.ActivityId));
        Assert.Equal(new[] { "A", "B" }, first.Select(item => item.BusinessReference));
    }

    [Fact]
    public async Task EMP054_TimelineContainsUsefulBusinessReference()
    {
        var user = await CreateUserAsync("emp054", SecurityRolePresets.SellerCode);
        await PersistSaleAsync("EMP054-REFERENCE", user.UserId, Now, 42m);
        var activity = Success(await ManagerReader().GetEmployeeAsync(user.UserId, Day(), Token)).Activity;
        Assert.Contains(activity, item => item.Category == "Vente" && item.BusinessReference == "EMP054-REFERENCE");
    }

    [Fact]
    public async Task EMP055_TimelineHidesTechnicalAndSecretFields()
    {
        var user = await CreateUserAsync("emp055", SecurityRolePresets.SellerCode);
        await PersistAuditAsync(user.UserId, "security", "UserAccount", user.UserId.ToString(), "security.password_hash.export", Now, Guid.NewGuid());
        var detail = Success(await ManagerReader().GetEmployeeAsync(user.UserId, Day(), Token));
        var publicProperties = typeof(EmployeeActivityItemView).GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain(publicProperties, name => name.Contains("Correlation", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(detail.Activity, item => item.Action.Contains("password", StringComparison.OrdinalIgnoreCase)
            || item.Action.Contains("hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EMP056_TodayPeriodUsesInclusiveExclusiveBounds()
    {
        var user = await CreateUserAsync("emp056", SecurityRolePresets.SellerCode);
        var period = EmployeeActivityPeriod.Today(Now);
        await PersistSaleAsync("EMP056-FROM", user.UserId, period.FromUtc, 1m);
        await PersistSaleAsync("EMP056-IN", user.UserId, period.ToUtc.AddSeconds(-1), 2m);
        await PersistSaleAsync("EMP056-TO", user.UserId, period.ToUtc, 4m);
        var summary = await SalesSummaryAsync(user.UserId, period);
        Assert.Equal(2, summary.SalesCount);
        Assert.Equal(3m, summary.GrossSalesAmount);
    }

    [Fact]
    public async Task EMP057_SevenDayPeriodIsExact()
    {
        var user = await CreateUserAsync("emp057", SecurityRolePresets.SellerCode);
        var period = EmployeeActivityPeriod.LastDays(Now, 7);
        await PersistSaleAsync("EMP057-FROM", user.UserId, period.FromUtc, 7m);
        await PersistSaleAsync("EMP057-BEFORE", user.UserId, period.FromUtc.AddSeconds(-1), 8m);
        Assert.Equal(1, (await SalesSummaryAsync(user.UserId, period)).SalesCount);
    }

    [Fact]
    public async Task EMP058_ThirtyDayAndCustomPeriodsAreExact()
    {
        var user = await CreateUserAsync("emp058", SecurityRolePresets.SellerCode);
        var thirtyDays = EmployeeActivityPeriod.LastDays(Now, 30);
        var custom = new EmployeeActivityPeriod(Now.AddDays(-3), Now.AddDays(-1));
        await PersistSaleAsync("EMP058-30", user.UserId, Now.AddDays(-20), 30m);
        await PersistSaleAsync("EMP058-CUSTOM", user.UserId, Now.AddDays(-2), 10m);
        Assert.Equal(2, (await SalesSummaryAsync(user.UserId, thirtyDays)).SalesCount);
        Assert.Equal(1, (await SalesSummaryAsync(user.UserId, custom)).SalesCount);
    }

    [Fact]
    public async Task EMP059_NewDbContextReturnsSameEmployeeMetrics()
    {
        var user = await CreateUserAsync("emp059", SecurityRolePresets.SellerCode);
        await PersistSaleAsync("EMP059", user.UserId, Now, 59m);
        var first = Success(await ManagerReader().GetEmployeeAsync(user.UserId, Day(), Token)).Summary;
        var second = Success(await ManagerReader().GetEmployeeAsync(user.UserId, Day(), Token)).Summary;
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task EMP060_FullProviderRebuildKeepsSessionsAndHistory()
    {
        var manager = await CreateUserAsync("emp060", SecurityRolePresets.ManagerCode);
        await PersistSaleAsync("EMP060", manager.UserId, Now, 60m);
        var first = await ProviderSummaryAsync("emp060", manager.UserId);
        var second = await ProviderSummaryAsync("emp060", manager.UserId);
        Assert.Equal(first.SalesCount, second.SalesCount);
        Assert.Equal(first.GrossSalesAmount, second.GrossSalesAmount);
        Assert.True(await CountAsync("security.UserSessions", "UserAccountId=@id", P("@id", manager.UserId)) >= 2);
    }

    [Fact]
    public void EMP061_EmployeeSummaryUsesGroupedQueriesInsteadOfUserNPlusOne()
    {
        var source = InfrastructureText("Security", "SqlEmployeeActivityReader.cs");
        Assert.Contains(".GroupBy(sale => sale.FinalizedByUserAccountId", source, StringComparison.Ordinal);
        Assert.Contains(".GroupBy(item => item.PerformedByUserAccountId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var user in users)", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EMP062_Emp01PermissionOverridesStillGovernAccess()
    {
        var manager = await CreateUserAsync("emp062", SecurityRolePresets.ManagerCode);
        var permission = Success(await Administration().GetPermissionsAsync(Token))
            .Single(item => item.Code == SecurityPermissions.SecurityViewEmployeeActivity);
        Success(await Administration().SetUserPermissionOverridesAsync(new SetUserPermissionOverridesRequest(
            Guid.NewGuid(), manager.UserId,
            [new SecurityPermissionOverrideSelection(permission.PermissionId, SecurityPermissionOverrideEffect.Deny)],
            manager.RowVersion), Token));
        var login = Success(await Authentication(Now).Service.LoginAsync(
            new LoginRequest("emp062", Password, Guid.NewGuid()), Token));

        var result = await Reader(login.Session).GetSummariesAsync(Day(), Token);
        Assert.Equal(SecurityErrors.PermissionDenied.Code, result.Error.Code);
    }

    [Fact]
    public async Task EMP063_ManagerActivityIsAlsoAttributable()
    {
        var manager = await CreateUserAsync("emp063", SecurityRolePresets.ManagerCode);
        await PersistSaleAsync("EMP063", manager.UserId, Now, 63m);
        var summary = await SalesSummaryAsync(manager.UserId, Day());
        Assert.Equal(1, summary.SalesCount);
        Assert.Equal(63m, summary.GrossSalesAmount);
    }

    [Fact]
    public void EMP064_NoApplicationContractCanChangeFinalizedSalePerformer()
    {
        var methods = typeof(Pharmacy.Application.Sales.Contracts.ISaleFinalizer).GetMethods();
        Assert.Single(methods);
        Assert.Equal("FinalizeAsync", methods[0].Name);
        Assert.DoesNotContain(methods, method => method.Name.Contains("Actor", StringComparison.OrdinalIgnoreCase)
            || method.Name.Contains("Performer", StringComparison.OrdinalIgnoreCase)
            || method.Name.Contains("Update", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EMP065_AuditAndBusinessAttributionAgreeForFinalizedSale()
    {
        var user = await CreateUserAsync("emp065", SecurityRolePresets.SellerCode);
        var operation = Guid.NewGuid();
        await PersistSaleAsync("EMP065", user.UserId, Now, 65m, operationId: operation);
        await PersistAuditAsync(user.UserId, "sales", "Sale", "EMP065", "sale.finalize", Now, operation);
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sales.Sales s JOIN audit.AuditEvents a ON a.OperationId=s.OperationId WHERE s.OperationId=@operation AND s.FinalizedByUserAccountId=a.ActorUserId;",
            P("@operation", operation)));
    }

    private ISecurityAdministrationService Administration()
    {
        var session = new CurrentSecuritySession();
        session.Establish(ManagerSession(fixture.OwnerUserId, SecurityGateSqlFixture.OwnerLogin), Now);
        var clock = new MutableSecurityClock(Now);
        return new SqlSecurityAdministrationService(
            fixture.Factory,
            fixture.Hasher,
            new UniqueRecoveryGenerator(),
            new PermissionAuthorizer(session, clock),
            session,
            clock);
    }

    private async Task<SecurityUserSummaryView> CreateUserAsync(string login, string roleCode)
    {
        var administration = Administration();
        var role = Success(await administration.GetRolesAsync(Token)).Single(item => item.Code == roleCode);
        return Success(await administration.CreateUserAsync(new CreateSecurityUserRequest(
            Guid.NewGuid(), login, login.ToUpperInvariant(), Password, [role.RoleId]), Token));
    }

    private AuthenticationHarness Authentication(DateTime now)
    {
        var clock = new MutableSecurityClock(now);
        var session = new CurrentSecuritySession();
        return new AuthenticationHarness(
            new SqlAuthenticationService(
                fixture.Factory,
                fixture.Hasher,
                new UniqueRecoveryGenerator(),
                session,
                clock),
            session,
            clock);
    }

    private IEmployeeActivityReader ManagerReader() => Reader(ManagerSession(
        fixture.OwnerUserId,
        SecurityGateSqlFixture.OwnerLogin));

    private IEmployeeActivityReader Reader(UserSessionView user)
    {
        var session = new CurrentSecuritySession();
        session.Establish(user, Now);
        return new SqlEmployeeActivityReader(
            fixture.Factory,
            new PermissionAuthorizer(session, new MutableSecurityClock(Now)),
            session);
    }

    private static UserSessionView ManagerSession(long userId, string login) => new(
        userId,
        login,
        "Manager",
        [SecurityRolePresets.ManagerCode],
        SecurityRolePresets.ManagerPermissions.ToHashSet(StringComparer.Ordinal),
        Now,
        Now);

    private static UserSessionView SellerSession(SecurityUserSummaryView user) => new(
        user.UserId,
        user.Login,
        user.DisplayName,
        [SecurityRolePresets.SellerCode],
        SecurityRolePresets.SellerPermissions.ToHashSet(StringComparer.Ordinal),
        Now,
        Now);

    private async Task<EmployeeActivitySummaryView> SalesSummaryAsync(long userId, EmployeeActivityPeriod period) =>
        Success(await ManagerReader().GetEmployeeAsync(userId, period, Token)).Summary;

    private async Task<EmployeeActivitySummaryView> ProviderSummaryAsync(string login, long userId)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<PharmacyDbContext>>(fixture.Factory);
        services.AddLocalSqlSecurity();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var authentication = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        Success(await authentication.LoginAsync(new LoginRequest(login, Password, Guid.NewGuid()), Token));
        return Success(await scope.ServiceProvider.GetRequiredService<IEmployeeActivityReader>()
            .GetEmployeeAsync(userId, Day(), Token)).Summary;
    }

    private async Task<long> PersistSaleAsync(
        string reference,
        long userId,
        DateTime completedAtUtc,
        decimal amount,
        int lineCount = 1,
        Guid? operationId = null)
    {
        var operation = operationId ?? Guid.NewGuid();
        return await fixture.ScalarAsync<long>(
            "INSERT INTO sales.SaleOperations(OperationId,OperationFingerprint,Status,ResultSaleId,CreatedAtUtc,CompletedAtUtc) VALUES(@operation,@fingerprint,N'Processing',NULL,@at,NULL); " +
            "INSERT INTO sales.Sales(Reference,OperationId,Status,CompletedAtUtc,GrandTotal,PaidTotal,CreditTotal,ChangeGiven,TotalQuantity,LineCount,PaymentMethod,CustomerId,FinalizedByUserAccountId) VALUES(@reference,@operation,N'Completed',@at,@amount,@amount,0,0,@lines,@lines,N'Cash',NULL,@user); " +
            "DECLARE @saleId bigint=SCOPE_IDENTITY(); UPDATE sales.SaleOperations SET Status=N'Completed',ResultSaleId=@saleId,CompletedAtUtc=@at WHERE OperationId=@operation; SELECT @saleId;",
            P("@operation", operation),
            P("@fingerprint", $"EMP|{reference}"),
            P("@at", completedAtUtc),
            P("@reference", reference),
            P("@amount", amount),
            P("@lines", lineCount),
            P("@user", userId));
    }

    private async Task<long> CreateSalesActorAsync(string login)
    {
        return await salesFixture.ScalarAsync<long>(
            "INSERT INTO security.UserAccounts(Login,NormalizedLogin,DisplayName,IsActive,PasswordHash,FailedLoginCount,LockedUntilUtc,MustChangePassword,LastLoginAtUtc,CreatedAtUtc,UpdatedAtUtc,DisabledAtUtc,DisabledByUserId,SecurityStamp) VALUES(@login,@normalized,@display,1,N'EMP-TEST-HASH',0,NULL,0,NULL,@now,@now,NULL,NULL,@stamp); SELECT CONVERT(bigint,SCOPE_IDENTITY());",
            P("@login", login), P("@normalized", login.ToUpperInvariant()), P("@display", login.ToUpperInvariant()),
            P("@now", Now), P("@stamp", Guid.NewGuid()));
    }

    private async Task PersistReturnAsync(
        string reference,
        long saleId,
        long userId,
        DateTime completedAtUtc,
        decimal amount)
    {
        await fixture.ExecuteAsync(
            "INSERT INTO sales.SaleReturns(OperationId,Reference,OriginalSaleId,Status,RefundAmount,RefundMethod,CompletedAtUtc,PerformedByUserAccountId) VALUES(@operation,@reference,@sale,N'Completed',@amount,N'Cash',@at,@user);",
            P("@operation", Guid.NewGuid()), P("@reference", reference), P("@sale", saleId),
            P("@amount", amount), P("@at", completedAtUtc), P("@user", userId));
    }

    private async Task InsertPaymentAsync(long saleId, string tag, decimal amount)
    {
        await fixture.ExecuteAsync(
            "INSERT INTO sales.Payments(SaleId,OperationId,PaymentMethod,Status,Amount,TenderedAmount,ChangeGiven,ExternalCardConfirmed,ConfirmedAtUtc) VALUES(@sale,@operation,N'ExternalCard',N'Completed',@amount,NULL,0,1,@at);",
            P("@sale", saleId), P("@operation", Guid.NewGuid()), P("@amount", amount), P("@at", Now));
    }

    private async Task PersistAuditAsync(
        long userId,
        string module,
        string entityType,
        string entityId,
        string action,
        DateTime occurredAtUtc,
        Guid operationId)
    {
        await fixture.ExecuteAsync(
            "INSERT INTO audit.AuditEvents(EventType,Module,EntityType,EntityId,Action,ActorUserId,ActorDisplayName,WorkstationId,Result,OccurredAtUtc,OperationId,CorrelationId,ReasonCode) VALUES(N'Business',@module,@entityType,@entityId,@action,@user,N'Employé',N'TEST',N'Success',@at,@operation,@correlation,NULL);",
            P("@module", module), P("@entityType", entityType), P("@entityId", entityId),
            P("@action", action), P("@user", userId), P("@at", occurredAtUtc),
            P("@operation", operationId), P("@correlation", Guid.NewGuid()));
    }

    private Task<long> SalesGateActorAsync(long saleId) => salesFixture.ScalarAsync<long>(
        "SELECT FinalizedByUserAccountId FROM sales.Sales WHERE Id=@id;", P("@id", saleId));

    private Task<int> CountAsync(string table, string predicate, params SqlParameter[] parameters) =>
        fixture.ScalarAsync<int>($"SELECT COUNT(*) FROM {table} WHERE {predicate};", parameters);

    private static EmployeeActivityPeriod Day() => new(Now.Date, Now.Date.AddDays(1));
    private static SqlParameter P(string name, object value) => new(name, value);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static T Success<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    private static void Success(Result result) => Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);

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

    private sealed record AuthenticationHarness(
        SqlAuthenticationService Service,
        CurrentSecuritySession Session,
        MutableSecurityClock Clock);
}
