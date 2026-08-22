using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Branding;
using Pharmacy.Application.Dashboard.Contracts;
using Pharmacy.Application.Dashboard.Errors;
using Pharmacy.Application.Dashboard.Models;
using Pharmacy.Application.Reporting.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Desktop.Components.Branding;
using Pharmacy.Desktop.Components.Dashboard;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Reporting;
using Pharmacy.Infrastructure.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class DashboardTests : IClassFixture<SecurityGateSqlFixture>
{
    private static readonly DateTime Now = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly ConcurrentDictionary<string, Lazy<Task<DashboardScenario>>> Scenarios = new(StringComparer.Ordinal);
    private readonly SecurityGateSqlFixture fixture;

    public DashboardTests(SecurityGateSqlFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task DASH001_ManagerDashboardLoadsAllowedGlobalKpis()
    {
        var scenario = await ScenarioAsync();
        var result = await ManagerDashboard(scenario).GetGlobalSalesAsync(new DashboardSalesQuery(7), Token);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
    }

    [Fact]
    public async Task DASH002_TodaysSaleCountIsExact()
    {
        var scenario = await ScenarioAsync();
        Assert.Equal(3, Success(await ManagerDashboard(scenario).GetGlobalSalesAsync(new(7), Token)).TodaySaleCount);
    }

    [Fact]
    public async Task DASH003_TodaysSalesAmountIsExact()
    {
        var scenario = await ScenarioAsync();
        var dashboardAmount = Success(await ManagerDashboard(scenario).GetGlobalSalesAsync(new(7), Token)).TodayGrossSales;
        var reporting = ReportingService(scenario.ManagerId);
        var reportingAmount = Success(await reporting.GetSnapshotAsync(
            new ReportingQuery(new ReportingPeriod(Now.Date, Now.Date.AddDays(1))), Token)).Summary.GrossSales;
        Assert.Equal(500m, dashboardAmount);
        Assert.Equal(reportingAmount, dashboardAmount);
    }

    [Fact]
    public async Task DASH004_LinesPaymentsAndAllocationsDoNotMultiplyKpis()
    {
        var scenario = await ScenarioAsync();
        Assert.True(await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM sales.SaleLines WHERE SaleId=@id;", P("@id", scenario.SellerSaleOneId)) > 0);
        Assert.Equal(2, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM sales.Payments WHERE SaleId=@id;", P("@id", scenario.SellerSaleOneId)));
        Assert.Equal(2, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM sales.SaleLineAllocations a JOIN sales.SaleLines l ON l.Id=a.SaleLineId WHERE l.SaleId=@id;", P("@id", scenario.SellerSaleOneId)));
        var view = Success(await ManagerDashboard(scenario).GetGlobalSalesAsync(new(7), Token));
        Assert.Equal(3, view.TodaySaleCount);
        Assert.Equal(500m, view.TodayGrossSales);
    }

    [Fact]
    public async Task DASH005_OpenCreditIsExact()
    {
        var scenario = await ScenarioAsync();
        Assert.Equal(300m, Success(await DashboardService(scenario.ManagerId, SecurityPermissions.CustomersManageCredit).GetOpenCreditAsync(Token)).OpenCredit);
    }

    [Fact]
    public async Task DASH006_LowStockCountIsExact()
    {
        var scenario = await ScenarioAsync();
        Assert.Equal(2, Success(await DashboardService(scenario.ManagerId, SecurityPermissions.InventoryView).GetInventoryAlertsAsync(cancellationToken: Token)).LowStockProducts);
    }

    [Fact]
    public async Task DASH007_SevenDayTrendContainsEveryDay()
    {
        var scenario = await ScenarioAsync();
        var trend = Success(await ManagerDashboard(scenario).GetGlobalSalesAsync(new(7), Token)).Trend;
        Assert.Equal(7, trend.Count);
        Assert.Equal(Enumerable.Range(0, 7).Select(value => DateOnly.FromDateTime(Now.Date.AddDays(-6 + value))), trend.Select(item => item.Day));
    }

    [Fact]
    public async Task DASH008_ThirtyDayTrendContainsEveryDay()
    {
        var scenario = await ScenarioAsync();
        var trend = Success(await ManagerDashboard(scenario).GetGlobalSalesAsync(new(30), Token)).Trend;
        Assert.Equal(30, trend.Count);
        Assert.Equal(DateOnly.FromDateTime(Now.Date.AddDays(-29)), trend[0].Day);
        Assert.Equal(DateOnly.FromDateTime(Now.Date), trend[^1].Day);
    }

    [Fact]
    public async Task DASH009_EmptySalesDaysProduceSafeZeroPoints()
    {
        var scenario = await ScenarioAsync();
        var trend = Success(await ManagerDashboard(scenario).GetGlobalSalesAsync(new(7), Token)).Trend;
        Assert.Contains(trend, item => item.Day == DateOnly.FromDateTime(Now.Date.AddDays(-2)) && item.SaleCount == 0 && item.GrossSales == 0m);
    }

    [Fact]
    public async Task DASH010_TopProductsQuantityRankingIsExact()
    {
        var scenario = await ScenarioAsync();
        var products = Success(await ManagerDashboard(scenario).GetGlobalSalesAsync(new(7), Token)).TopProducts;
        Assert.Equal(scenario.LongProductName, products[0].ProductName);
        Assert.Equal(5, products[0].SoldQuantity);
        Assert.True(products.Zip(products.Skip(1), (left, right) => left.SoldQuantity >= right.SoldQuantity).All(value => value));
    }

    [Fact]
    public async Task DASH011_TopProductsAreNotMultipliedByFefoAllocations()
    {
        var scenario = await ScenarioAsync();
        var product = Success(await ManagerDashboard(scenario).GetGlobalSalesAsync(new(7), Token)).TopProducts.Single(item => item.ProductId == scenario.LongProductId);
        Assert.Equal(5, product.SoldQuantity);
        Assert.Equal(250m, product.GrossSales);
    }

    [Fact]
    public async Task DASH012_ExpiryAlertsRespectConfiguredHorizon()
    {
        var scenario = await ScenarioAsync();
        var inventory = Success(await DashboardService(scenario.ManagerId, SecurityPermissions.InventoryView).GetInventoryAlertsAsync(20, Token));
        Assert.Equal(30, inventory.ExpiryHorizonDays);
        Assert.Contains(inventory.ExpiryAlerts, item => item.DaysRemaining == 20);
        Assert.DoesNotContain(inventory.ExpiryAlerts, item => item.DaysRemaining == 60);
    }

    [Fact]
    public async Task DASH013_ExpiredItemIsDifferentiatedFromUpcomingExpiry()
    {
        var scenario = await ScenarioAsync();
        var expiry = Success(await DashboardService(scenario.ManagerId, SecurityPermissions.InventoryView).GetInventoryAlertsAsync(20, Token)).ExpiryAlerts;
        Assert.Contains(expiry, item => item.DaysRemaining < 0 && item.Status == "Périmé");
        Assert.Contains(expiry, item => item.DaysRemaining == 5 && item.Status == "Très proche");
        Assert.Contains(expiry, item => item.DaysRemaining == 20 && item.Status == "Dans l’horizon");
    }

    [Fact]
    public async Task DASH014_EmployeeSalesComparisonIsExact()
    {
        var scenario = await ScenarioAsync();
        var summaries = Success(await EmployeeReader(scenario.ManagerId, SecurityPermissions.SecurityViewEmployeeActivity).GetSummariesAsync(Week(), Token));
        var seller = summaries.Single(item => item.UserId == scenario.SellerId);
        Assert.Equal(2, seller.SalesCount);
        Assert.Equal(250m, seller.GrossSalesAmount);
    }

    [Fact]
    public async Task DASH015_ManagersPerformingSalesAreIncluded()
    {
        var scenario = await ScenarioAsync();
        var summaries = Success(await EmployeeReader(scenario.ManagerId, SecurityPermissions.SecurityViewEmployeeActivity).GetSummariesAsync(Week(), Token));
        var manager = summaries.Single(item => item.UserId == scenario.ManagerId);
        Assert.Equal("MANAGER", manager.PrimaryRole);
        Assert.Equal(1, manager.SalesCount);
        Assert.Equal(250m, manager.GrossSalesAmount);
    }

    [Fact]
    public async Task DASH016_EmployeeComparisonRequiresEmployeeActivityPermission()
    {
        var scenario = await ScenarioAsync();
        var result = await EmployeeReader(scenario.ManagerId, SecurityPermissions.ReportsView).GetSummariesAsync(Week(), Token);
        Assert.Equal(SecurityErrors.PermissionDenied.Code, result.Error.Code);
    }

    [Fact]
    public async Task DASH017_SellerIsDeniedGlobalEmployeeStatisticsBackend()
    {
        var scenario = await ScenarioAsync();
        var result = await EmployeeReader(scenario.SellerId, SecurityPermissions.SalesViewHistory).GetSummariesAsync(Week(), Token);
        Assert.Equal(SecurityErrors.PermissionDenied.Code, result.Error.Code);
        var global = await DashboardService(scenario.SellerId, SecurityPermissions.SalesViewHistory).GetGlobalSalesAsync(new(7), Token);
        Assert.Equal(SecurityErrors.PermissionDenied.Code, global.Error.Code);
    }

    [Fact]
    public async Task DASH018_SellerSeesOwnTodaysSaleCount()
    {
        var scenario = await ScenarioAsync();
        var coordinator = PersonalCoordinator(scenario);
        await coordinator.LoadAsync(Token);
        Assert.Equal(2, coordinator.PersonalTodaySaleCount);
    }

    [Fact]
    public async Task DASH019_SellerSeesOwnTodaysAmount()
    {
        var scenario = await ScenarioAsync();
        var coordinator = PersonalCoordinator(scenario);
        await coordinator.LoadAsync(Token);
        Assert.Equal(250m, coordinator.PersonalTodaySalesAmount);
    }

    [Fact]
    public async Task DASH020_SellerCannotSeeAnotherSellerMetrics()
    {
        var scenario = await ScenarioAsync();
        var result = await EmployeeReader(scenario.SellerId, SecurityPermissions.SalesViewHistory).GetEmployeeAsync(scenario.OtherSellerId, Week(), Token);
        Assert.Equal(SecurityErrors.PermissionDenied.Code, result.Error.Code);
    }

    [Fact]
    public async Task DASH021_SellerRecentSalesContainOnlyOwnSales()
    {
        var scenario = await ScenarioAsync();
        var coordinator = PersonalCoordinator(scenario);
        await coordinator.LoadAsync(Token);
        Assert.Equal(2, coordinator.RecentPersonalSales.Count);
        Assert.All(coordinator.RecentPersonalSales, sale => Assert.StartsWith("DASH-SELLER-", sale.Reference, StringComparison.Ordinal));
    }

    [Fact]
    public void DASH022_NewSaleShortcutRequiresSalesCreate()
    {
        var home = DesktopText("Components", "Pages", "Home.razor");
        var coordinator = DesktopText("Components", "Dashboard", "DashboardCoordinator.cs");
        Assert.Contains("@if (Dashboard.CanCreateSale)", home, StringComparison.Ordinal);
        Assert.Contains("Has(SecurityPermissions.SalesCreate)", coordinator, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", home[home.IndexOf("Nouvelle vente", StringComparison.Ordinal)..Math.Min(home.Length, home.IndexOf("Nouvelle vente", StringComparison.Ordinal) + 120)], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DASH023_UnauthorizedDashboardModulesAreNotRendered()
    {
        var home = DesktopText("Components", "Pages", "Home.razor");
        Assert.Contains("Dashboard.GlobalSales is", home, StringComparison.Ordinal);
        Assert.Contains("Dashboard.Credit is", home, StringComparison.Ordinal);
        Assert.Contains("Dashboard.Inventory is", home, StringComparison.Ordinal);
        Assert.Contains("Dashboard.Employees is", home, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DASH024_UnauthorizedDataQueriesAreNotExecuted()
    {
        var dashboard = new FakeDashboardService();
        var employees = new FakeEmployeeReader();
        var coordinator = FakeCoordinator([SecurityPermissions.SalesCreate], dashboard, employees);
        await coordinator.LoadAsync(Token);
        Assert.Equal(0, dashboard.TotalCalls);
        Assert.Equal(0, employees.TotalCalls);
    }

    [Fact]
    public async Task DASH025_PermissionOverrideRecomposesManagerDashboard()
    {
        var dashboard = new FakeDashboardService { SalesResult = Result<DashboardSalesView>.Success(EmptySales()) };
        var employees = new FakeEmployeeReader();
        var coordinator = FakeCoordinator([SecurityPermissions.ReportsView], dashboard, employees);
        await coordinator.LoadAsync(Token);
        Assert.True(coordinator.CanViewGlobalSales);
        Assert.False(coordinator.CanViewEmployeeActivity);
        Assert.NotNull(coordinator.GlobalSales);
        Assert.Null(coordinator.Employees);
        Assert.Equal(0, employees.SummaryCalls);
    }

    [Fact]
    public void DASH026_SessionLabelUsesDurationOfSessionOnly()
    {
        var home = DesktopText("Components", "Pages", "Home.razor");
        Assert.Contains("Durée de session", home, StringComparison.Ordinal);
        Assert.DoesNotContain("temps de travail", home, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("heures travaillées", home, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DASH027_DashboardLoadingStateIsControlled()
    {
        var pending = new TaskCompletionSource<Result<DashboardSalesView>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dashboard = new FakeDashboardService { SalesHandler = (_, _) => pending.Task };
        var coordinator = FakeCoordinator([SecurityPermissions.ReportsView], dashboard, new FakeEmployeeReader());
        var load = coordinator.LoadAsync(Token);
        Assert.True(coordinator.IsBusy);
        Assert.False(coordinator.HasAnyContent);
        pending.SetResult(Result<DashboardSalesView>.Success(EmptySales()));
        await load;
        Assert.False(coordinator.IsBusy);
        Assert.True(coordinator.HasAnyContent);
    }

    [Fact]
    public async Task DASH028_ReportingFailureDoesNotShowStaleKpi()
    {
        var dashboard = new FakeDashboardService { SalesResult = Result<DashboardSalesView>.Success(EmptySales() with { TodayGrossSales = 99m }) };
        var coordinator = FakeCoordinator([SecurityPermissions.ReportsView], dashboard, new FakeEmployeeReader());
        await coordinator.LoadAsync(Token);
        Assert.Equal(99m, coordinator.GlobalSales?.TodayGrossSales);
        dashboard.SalesResult = Result<DashboardSalesView>.Failure(DashboardErrors.DatabaseUnavailable);
        await coordinator.LoadAsync(Token);
        Assert.Null(coordinator.GlobalSales);
        Assert.NotNull(coordinator.GlobalSalesError);
    }

    [Fact]
    public async Task DASH029_EmployeeFailureDoesNotShowStaleComparison()
    {
        var employees = new FakeEmployeeReader
        {
            SummaryResult = Result<IReadOnlyList<EmployeeActivitySummaryView>>.Success([
                new(1, "manager", "Manager", "MANAGER", true, Now, Now, 1, 10m, 0, 0m)])
        };
        var coordinator = FakeCoordinator([SecurityPermissions.SecurityViewEmployeeActivity], new FakeDashboardService(), employees);
        await coordinator.LoadAsync(Token);
        Assert.NotNull(coordinator.Employees);
        employees.SummaryResult = Result<IReadOnlyList<EmployeeActivitySummaryView>>.Failure(DashboardErrors.DatabaseUnavailable);
        await coordinator.LoadAsync(Token);
        Assert.Null(coordinator.Employees);
        Assert.NotNull(coordinator.EmployeesError);
    }

    [Fact]
    public void DASH030_EmptyStatesAreUserFriendly()
    {
        var home = DesktopText("Components", "Pages", "Home.razor");
        Assert.Contains("Aucune vente sur la période", home, StringComparison.Ordinal);
        Assert.Contains("Aucune alerte de péremption", home, StringComparison.Ordinal);
        Assert.Contains("Aucune vente récente", home, StringComparison.Ordinal);
        Assert.DoesNotContain("No data", home, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DASH031_ChartsUseBrandingTokensInsteadOfFixedClientGreen()
    {
        var css = DesktopText("wwwroot", "css", "dashboard.css");
        Assert.Contains("var(--color-accent)", css, StringComparison.Ordinal);
        Assert.Contains("var(--color-warning)", css, StringComparison.Ordinal);
        Assert.Contains("var(--color-danger)", css, StringComparison.Ordinal);
        Assert.DoesNotContain("#0D9A48", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Pharmacie Diar Douha", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DASH032_LongProductNamesDoNotBreakProjection()
    {
        var scenario = await ScenarioAsync();
        var product = Success(await ManagerDashboard(scenario).GetGlobalSalesAsync(new(7), Token)).TopProducts.Single(item => item.ProductId == scenario.LongProductId);
        Assert.Equal(scenario.LongProductName, product.ProductName);
        var css = DesktopText("wwwroot", "css", "dashboard.css");
        Assert.Contains("text-overflow: ellipsis", css, StringComparison.Ordinal);
        Assert.Contains("min-width: 0", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DASH033_AggregationAvoidsNPlusOneAndRemainsReadOnly()
    {
        var scenario = await ScenarioAsync();
        var before = await BusinessRowCountsAsync();
        var service = DashboardService(scenario.ManagerId,
            SecurityPermissions.ReportsView,
            SecurityPermissions.CustomersManageCredit,
            SecurityPermissions.InventoryView);
        Success(await service.GetGlobalSalesAsync(new(30), Token));
        Success(await service.GetOpenCreditAsync(Token));
        Success(await service.GetInventoryAlertsAsync(cancellationToken: Token));
        Assert.Equal(before, await BusinessRowCountsAsync());
        var source = InfrastructureText("Reporting", "SqlDashboardService.cs");
        Assert.Contains("GroupBy", source, StringComparison.Ordinal);
        Assert.Contains("reporting.vw_ProductSales", source, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DASH034_CustomForbiddenJargonIsAbsent()
    {
        var home = DesktopText("Components", "Pages", "Home.razor");
        Assert.DoesNotContain("Demo", home, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Prototype", home, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sprint", home, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SQL", home, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DASH035_BrandRuntimeCustomizationRemainsEffective()
    {
        var style = BrandingCoordinator.BuildThemeStyle("#246B45");
        Assert.Contains("--pharmacy-primary:#246B45", style, StringComparison.Ordinal);
        Assert.Contains("var(--color-accent)", DesktopText("wwwroot", "css", "dashboard.css"), StringComparison.Ordinal);
        Assert.Contains("@import url(\"./dashboard.css\")", DesktopText("wwwroot", "css", "app.css"), StringComparison.Ordinal);
    }

    private async Task<DashboardScenario> ScenarioAsync()
    {
        var lazy = Scenarios.GetOrAdd(fixture.ConnectionString, _ => new Lazy<Task<DashboardScenario>>(
            () => SeedScenarioAsync(fixture), LazyThreadSafetyMode.ExecutionAndPublication));
        return await lazy.Value;
    }

    private SqlDashboardService ManagerDashboard(DashboardScenario scenario) =>
        DashboardService(scenario.ManagerId, SecurityPermissions.ReportsView);

    private SqlDashboardService DashboardService(long userId, params string[] permissions)
    {
        var session = Session(userId, permissions);
        return new SqlDashboardService(fixture.Factory, new PermissionAuthorizer(session, new FixedDashboardClock(Now)), new FixedDashboardClock(Now));
    }

    private SqlEmployeeActivityReader EmployeeReader(long userId, params string[] permissions)
    {
        var session = Session(userId, permissions);
        return new SqlEmployeeActivityReader(fixture.Factory, new PermissionAuthorizer(session, new FixedDashboardClock(Now)), session);
    }

    private SqlReportingService ReportingService(long userId)
    {
        var session = Session(userId, [SecurityPermissions.ReportsView]);
        var clock = new FixedDashboardClock(Now);
        var authorizer = new PermissionAuthorizer(session, clock);
        var audit = new BusinessAuditRecorder(new SqlAuditStore(fixture.Factory, authorizer), session, clock);
        return new SqlReportingService(fixture.Factory, authorizer, audit, clock);
    }

    private DashboardCoordinator PersonalCoordinator(DashboardScenario scenario)
    {
        var permissions = new[] { SecurityPermissions.SalesCreate, SecurityPermissions.SalesViewHistory };
        var session = Session(scenario.SellerId, permissions);
        var clock = new FixedDashboardClock(Now);
        var authorizer = new PermissionAuthorizer(session, clock);
        return new DashboardCoordinator(
            new SqlDashboardService(fixture.Factory, authorizer, clock),
            new SqlEmployeeActivityReader(fixture.Factory, authorizer, session),
            session,
            authorizer,
            clock);
    }

    private static DashboardCoordinator FakeCoordinator(
        IReadOnlyList<string> permissions,
        FakeDashboardService dashboard,
        FakeEmployeeReader employees)
    {
        var clock = new FixedDashboardClock(Now);
        var session = Session(1, permissions);
        var authorizer = new PermissionAuthorizer(session, clock);
        return new DashboardCoordinator(dashboard, employees, session, authorizer, clock);
    }

    private static CurrentSecuritySession Session(long userId, IEnumerable<string> permissions)
    {
        var session = new CurrentSecuritySession();
        session.Establish(new UserSessionView(
            userId,
            $"user-{userId}",
            $"Utilisateur {userId}",
            ["TEST"],
            new HashSet<string>(permissions, StringComparer.Ordinal),
            Now.AddHours(-2),
            Now.AddHours(-2)), Now.AddHours(-2));
        return session;
    }

    private EmployeeActivityPeriod Week() => new(Now.Date.AddDays(-6), Now.Date.AddDays(1));

    private async Task<(int Sales, int Lines, int Payments, int Allocations, int Credit, int Balances)> BusinessRowCountsAsync() => (
        await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM sales.Sales;"),
        await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM sales.SaleLines;"),
        await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM sales.Payments;"),
        await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM sales.SaleLineAllocations;"),
        await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM customers.CustomerLedgerEntries;"),
        await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM inventory.StockBalances;"));

    private static DashboardSalesView EmptySales() => new(
        new DashboardPeriod(Now.Date.AddDays(-6), Now.Date.AddDays(1)),
        Now,
        0,
        0m,
        Enumerable.Range(0, 7).Select(index => new DashboardDailySalesPoint(DateOnly.FromDateTime(Now.Date.AddDays(-6 + index)), 0, 0m)).ToArray(),
        Array.Empty<DashboardTopProductView>());

    private static T Success<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);
        return result.Value;
    }

    private static SqlParameter P(string name, object value) => new(name, value);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<DashboardScenario> SeedScenarioAsync(SecurityGateSqlFixture fixture)
    {
        const string longName = "Paracétamol 500 mg — boîte familiale avec une désignation volontairement longue pour vérifier la projection du graphique";
        const string sql = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            DECLARE @manager bigint, @seller bigint, @other bigint, @category bigint, @location bigint;
            DECLARE @productA bigint, @productB bigint, @productC bigint, @productD bigint;
            DECLARE @batchA1 bigint, @batchA2 bigint, @batchB bigint, @batchC bigint, @batchFar bigint;
            DECLARE @balanceA1 bigint, @balanceA2 bigint, @sale1 bigint, @line1 bigint;
            DECLARE @op uniqueidentifier;

            INSERT security.UserAccounts(Login, NormalizedLogin, DisplayName, IsActive, PasswordHash, FailedLoginCount, MustChangePassword, LastLoginAtUtc, CreatedAtUtc, UpdatedAtUtc, SecurityStamp)
            VALUES
              (N'dash.manager', N'DASH.MANAGER', N'Manager Opérations', 1, N'test-hash', 0, 0, @now, @now, @now, NEWID()),
              (N'dash.seller', N'DASH.SELLER', N'Sara Vendeuse', 1, N'test-hash', 0, 0, @now, @now, @now, NEWID()),
              (N'dash.other', N'DASH.OTHER', N'Ahmed Vendeur', 1, N'test-hash', 0, 0, @now, @now, @now, NEWID());
            SELECT @manager=Id FROM security.UserAccounts WHERE NormalizedLogin=N'DASH.MANAGER';
            SELECT @seller=Id FROM security.UserAccounts WHERE NormalizedLogin=N'DASH.SELLER';
            SELECT @other=Id FROM security.UserAccounts WHERE NormalizedLogin=N'DASH.OTHER';
            INSERT security.UserRoles(UserAccountId, RoleId, AssignedAtUtc) VALUES(@manager,2,@now),(@seller,3,@now),(@other,3,@now);
            INSERT security.UserSessions(UserAccountId, StartedAtUtc, Status) VALUES(@manager,DATEADD(hour,-3,@now),N'Open'),(@seller,DATEADD(hour,-2,@now),N'Open');

            INSERT catalog.ProductCategories(Code, Name, IsActive, DefaultTracksLots, DefaultTracksExpiry, DefaultDiscountAllowed, SortOrder)
            VALUES(N'DASH', N'Tableau de bord', 1, 1, 1, 0, 0);
            SET @category=SCOPE_IDENTITY();
            INSERT catalog.Products(Name, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES(@longName,1,@now,@now); SET @productA=SCOPE_IDENTITY();
            INSERT catalog.ProductLocalSettings(ProductId,InternalCode,CategoryId,IsActive,IsSellable,IsPurchasable,TracksLots,TracksExpiry,DiscountAllowed,QuantityScale,MinimumStock,CurrentSalePrice,CurrencyCode)
            VALUES(@productA,N'DASH-A',@category,1,1,1,1,1,0,0,10,50,N'MAD');
            INSERT catalog.Products(Name, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES(N'Produit périmé',1,@now,@now); SET @productB=SCOPE_IDENTITY();
            INSERT catalog.ProductLocalSettings(ProductId,InternalCode,CategoryId,IsActive,IsSellable,IsPurchasable,TracksLots,TracksExpiry,DiscountAllowed,QuantityScale,MinimumStock,CurrentSalePrice,CurrencyCode)
            VALUES(@productB,N'DASH-B',@category,1,1,1,1,1,0,0,5,62.5,N'MAD');
            INSERT catalog.Products(Name, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES(N'Produit horizon trente jours',1,@now,@now); SET @productC=SCOPE_IDENTITY();
            INSERT catalog.ProductLocalSettings(ProductId,InternalCode,CategoryId,IsActive,IsSellable,IsPurchasable,TracksLots,TracksExpiry,DiscountAllowed,QuantityScale,MinimumStock,CurrentSalePrice,CurrencyCode)
            VALUES(@productC,N'DASH-C',@category,1,1,1,1,1,0,0,NULL,40,N'MAD');
            INSERT catalog.Products(Name, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES(N'Produit vente veille',1,@now,@now); SET @productD=SCOPE_IDENTITY();
            INSERT catalog.ProductLocalSettings(ProductId,InternalCode,CategoryId,IsActive,IsSellable,IsPurchasable,TracksLots,TracksExpiry,DiscountAllowed,QuantityScale,MinimumStock,CurrentSalePrice,CurrencyCode)
            VALUES(@productD,N'DASH-D',@category,1,1,1,0,0,0,0,NULL,300,N'MAD');

            INSERT inventory.StockLocations(CreationOperationId,CreationFingerprint,Code,Name,IsActive,IsPrimary,CreatedAtUtc)
            VALUES(NEWID(),N'DASH-LOCATION',N'DASH-MAIN',N'Principal dashboard',1,1,@now); SET @location=SCOPE_IDENTITY();
            INSERT inventory.StockBatches(CreationOperationId,CreationFingerprint,ProductId,BatchNumber,ExpiryDate,IsActive,CreatedAtUtc)
            VALUES(NEWID(),N'DASH-A1',@productA,N'DASH-A-05',DATEADD(day,5,CONVERT(date,@now)),1,@now); SET @batchA1=SCOPE_IDENTITY();
            INSERT inventory.StockBatches(CreationOperationId,CreationFingerprint,ProductId,BatchNumber,ExpiryDate,IsActive,CreatedAtUtc)
            VALUES(NEWID(),N'DASH-A2',@productA,N'DASH-A-60',DATEADD(day,60,CONVERT(date,@now)),1,@now); SET @batchA2=SCOPE_IDENTITY();
            INSERT inventory.StockBatches(CreationOperationId,CreationFingerprint,ProductId,BatchNumber,ExpiryDate,IsActive,CreatedAtUtc)
            VALUES(NEWID(),N'DASH-B',@productB,N'DASH-B-EXP',DATEADD(day,-1,CONVERT(date,@now)),1,@now); SET @batchB=SCOPE_IDENTITY();
            INSERT inventory.StockBatches(CreationOperationId,CreationFingerprint,ProductId,BatchNumber,ExpiryDate,IsActive,CreatedAtUtc)
            VALUES(NEWID(),N'DASH-C',@productC,N'DASH-C-20',DATEADD(day,20,CONVERT(date,@now)),1,@now); SET @batchC=SCOPE_IDENTITY();
            INSERT inventory.StockBatches(CreationOperationId,CreationFingerprint,ProductId,BatchNumber,ExpiryDate,IsActive,CreatedAtUtc)
            VALUES(NEWID(),N'DASH-C-FAR',@productC,N'DASH-C-60',DATEADD(day,60,CONVERT(date,@now)),1,@now); SET @batchFar=SCOPE_IDENTITY();
            INSERT inventory.StockBalances(StockLocationId,ProductId,StockBatchId,AvailableQuantity,UpdatedAtUtc) VALUES(@location,@productA,@batchA1,3,@now); SET @balanceA1=SCOPE_IDENTITY();
            INSERT inventory.StockBalances(StockLocationId,ProductId,StockBatchId,AvailableQuantity,UpdatedAtUtc) VALUES(@location,@productA,@batchA2,2,@now); SET @balanceA2=SCOPE_IDENTITY();
            INSERT inventory.StockBalances(StockLocationId,ProductId,StockBatchId,AvailableQuantity,UpdatedAtUtc) VALUES(@location,@productB,@batchB,4,@now);
            INSERT inventory.StockBalances(StockLocationId,ProductId,StockBatchId,AvailableQuantity,UpdatedAtUtc) VALUES(@location,@productC,@batchC,8,@now);
            INSERT inventory.StockBalances(StockLocationId,ProductId,StockBatchId,AvailableQuantity,UpdatedAtUtc) VALUES(@location,@productC,@batchFar,2,@now);
            INSERT inventory.StockBalances(StockLocationId,ProductId,StockBatchId,AvailableQuantity,UpdatedAtUtc) VALUES(@location,@productD,NULL,10,@now);
            UPDATE reporting.ReportSettings SET LowStockAlertsEnabled=1, ExpiryAlertsEnabled=1, ExpiryHorizonDays=30, UpdatedAtUtc=@now WHERE Id=1;

            INSERT customers.Customers(Reference,FullName,IsActive,CreditAllowed,CreatedAtUtc,UpdatedAtUtc) VALUES(N'DASH-CUSTOMER',N'Client dashboard',1,1,@now,@now);
            DECLARE @customer bigint=SCOPE_IDENTITY();
            INSERT customers.CustomerLedgerEntries(OperationId,CustomerId,EntryType,Amount,SourceType,SourceReference,OccurredAtUtc,CreatedAtUtc)
            VALUES(NEWID(),@customer,N'CreditSale',400,N'Sale',N'DASH-CREDIT',@now,@now),(NEWID(),@customer,N'CreditPayment',100,N'CreditPayment',N'DASH-PAY',@now,@now);

            SET @op=NEWID(); INSERT sales.SaleOperations(OperationId,OperationFingerprint,Status,CreatedAtUtc,CompletedAtUtc) VALUES(@op,N'DASH-S1',N'Processing',@now,NULL);
            INSERT sales.Sales(Reference,OperationId,Status,CompletedAtUtc,GrandTotal,PaidTotal,CreditTotal,ChangeGiven,TotalQuantity,LineCount,PaymentMethod,FinalizedByUserAccountId)
            VALUES(N'DASH-SELLER-001',@op,N'Completed',DATEADD(hour,-2,@now),100,100,0,0,2,1,N'Cash',@seller); SET @sale1=SCOPE_IDENTITY();
            UPDATE sales.SaleOperations SET Status=N'Completed',ResultSaleId=@sale1,CompletedAtUtc=DATEADD(hour,-2,@now) WHERE OperationId=@op;
            INSERT sales.SaleLines(SaleId,ProductId,ProductReferenceSnapshot,ProductNameSnapshot,UnitPrice,Quantity,LineTotal) VALUES(@sale1,@productA,N'DASH-A',@longName,50,2,100); SET @line1=SCOPE_IDENTITY();
            INSERT sales.SaleLineAllocations(SaleLineId,StockBalanceId,StockBatchId,Quantity,BatchNumberSnapshot,ExpiryDateSnapshot) VALUES(@line1,@balanceA1,@batchA1,1,N'DASH-A-05',DATEADD(day,5,CONVERT(date,@now))),(@line1,@balanceA2,@batchA2,1,N'DASH-A-60',DATEADD(day,60,CONVERT(date,@now)));
            INSERT sales.Payments(SaleId,OperationId,PaymentMethod,Status,Amount,TenderedAmount,ChangeGiven,ExternalCardConfirmed,ConfirmedAtUtc) VALUES(@sale1,NEWID(),N'Cash',N'Completed',50,50,0,0,DATEADD(hour,-2,@now)),(@sale1,NEWID(),N'ExternalCard',N'Completed',50,NULL,0,1,DATEADD(hour,-2,@now));

            SET @op=NEWID(); INSERT sales.SaleOperations(OperationId,OperationFingerprint,Status,CreatedAtUtc,CompletedAtUtc) VALUES(@op,N'DASH-S2',N'Processing',@now,NULL);
            INSERT sales.Sales(Reference,OperationId,Status,CompletedAtUtc,GrandTotal,PaidTotal,CreditTotal,ChangeGiven,TotalQuantity,LineCount,PaymentMethod,FinalizedByUserAccountId) VALUES(N'DASH-SELLER-002',@op,N'Completed',DATEADD(hour,-1,@now),150,150,0,0,3,1,N'Cash',@seller); DECLARE @sale2 bigint=SCOPE_IDENTITY();
            UPDATE sales.SaleOperations SET Status=N'Completed',ResultSaleId=@sale2,CompletedAtUtc=DATEADD(hour,-1,@now) WHERE OperationId=@op;
            INSERT sales.SaleLines(SaleId,ProductId,ProductReferenceSnapshot,ProductNameSnapshot,UnitPrice,Quantity,LineTotal) VALUES(@sale2,@productA,N'DASH-A',@longName,50,3,150);

            SET @op=NEWID(); INSERT sales.SaleOperations(OperationId,OperationFingerprint,Status,CreatedAtUtc,CompletedAtUtc) VALUES(@op,N'DASH-M1',N'Processing',@now,NULL);
            INSERT sales.Sales(Reference,OperationId,Status,CompletedAtUtc,GrandTotal,PaidTotal,CreditTotal,ChangeGiven,TotalQuantity,LineCount,PaymentMethod,FinalizedByUserAccountId) VALUES(N'DASH-MANAGER-001',@op,N'Completed',DATEADD(hour,-3,@now),250,250,0,0,4,1,N'ExternalCard',@manager); DECLARE @sale3 bigint=SCOPE_IDENTITY();
            UPDATE sales.SaleOperations SET Status=N'Completed',ResultSaleId=@sale3,CompletedAtUtc=DATEADD(hour,-3,@now) WHERE OperationId=@op;
            INSERT sales.SaleLines(SaleId,ProductId,ProductReferenceSnapshot,ProductNameSnapshot,UnitPrice,Quantity,LineTotal) VALUES(@sale3,@productB,N'DASH-B',N'Produit périmé',62.5,4,250);

            SET @op=NEWID(); INSERT sales.SaleOperations(OperationId,OperationFingerprint,Status,CreatedAtUtc,CompletedAtUtc) VALUES(@op,N'DASH-O1',N'Processing',@now,NULL);
            INSERT sales.Sales(Reference,OperationId,Status,CompletedAtUtc,GrandTotal,PaidTotal,CreditTotal,ChangeGiven,TotalQuantity,LineCount,PaymentMethod,FinalizedByUserAccountId) VALUES(N'DASH-OTHER-001',@op,N'Completed',DATEADD(day,-1,@now),300,300,0,0,1,1,N'Cash',@other); DECLARE @sale4 bigint=SCOPE_IDENTITY();
            UPDATE sales.SaleOperations SET Status=N'Completed',ResultSaleId=@sale4,CompletedAtUtc=DATEADD(day,-1,@now) WHERE OperationId=@op;
            INSERT sales.SaleLines(SaleId,ProductId,ProductReferenceSnapshot,ProductNameSnapshot,UnitPrice,Quantity,LineTotal) VALUES(@sale4,@productD,N'DASH-D',N'Produit vente veille',300,1,300);
            COMMIT TRANSACTION;
            """;
        await fixture.ExecuteAsync(sql, P("@now", Now), P("@longName", longName));
        return new DashboardScenario(
            await fixture.ScalarAsync<long>("SELECT Id FROM security.UserAccounts WHERE NormalizedLogin=N'DASH.MANAGER';"),
            await fixture.ScalarAsync<long>("SELECT Id FROM security.UserAccounts WHERE NormalizedLogin=N'DASH.SELLER';"),
            await fixture.ScalarAsync<long>("SELECT Id FROM security.UserAccounts WHERE NormalizedLogin=N'DASH.OTHER';"),
            await fixture.ScalarAsync<long>("SELECT Id FROM catalog.Products WHERE Name=@name;", P("@name", longName)),
            await fixture.ScalarAsync<long>("SELECT Id FROM sales.Sales WHERE Reference=N'DASH-SELLER-001';"),
            longName);
    }

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

    private sealed record DashboardScenario(
        long ManagerId,
        long SellerId,
        long OtherSellerId,
        long LongProductId,
        long SellerSaleOneId,
        string LongProductName);

    private sealed class FixedDashboardClock(DateTime now) : ISecurityClock
    {
        public DateTime UtcNow { get; } = now;
    }

    private sealed class FakeDashboardService : IDashboardService
    {
        public int SalesCalls { get; private set; }
        public int CreditCalls { get; private set; }
        public int InventoryCalls { get; private set; }
        public int TotalCalls => SalesCalls + CreditCalls + InventoryCalls;
        public Result<DashboardSalesView> SalesResult { get; set; } = Result<DashboardSalesView>.Failure(DashboardErrors.DatabaseUnavailable);
        public Func<DashboardSalesQuery, CancellationToken, Task<Result<DashboardSalesView>>>? SalesHandler { get; set; }
        public Task<Result<DashboardSalesView>> GetGlobalSalesAsync(DashboardSalesQuery query, CancellationToken cancellationToken = default)
        {
            SalesCalls++;
            return SalesHandler?.Invoke(query, cancellationToken) ?? Task.FromResult(SalesResult);
        }
        public Task<Result<DashboardCreditView>> GetOpenCreditAsync(CancellationToken cancellationToken = default)
        {
            CreditCalls++;
            return Task.FromResult(Result<DashboardCreditView>.Failure(DashboardErrors.DatabaseUnavailable));
        }
        public Task<Result<DashboardInventoryView>> GetInventoryAlertsAsync(int expiryLimit = 6, CancellationToken cancellationToken = default)
        {
            InventoryCalls++;
            return Task.FromResult(Result<DashboardInventoryView>.Failure(DashboardErrors.DatabaseUnavailable));
        }
    }

    private sealed class FakeEmployeeReader : IEmployeeActivityReader
    {
        public int SummaryCalls { get; private set; }
        public int DetailCalls { get; private set; }
        public int TotalCalls => SummaryCalls + DetailCalls;
        public Result<IReadOnlyList<EmployeeActivitySummaryView>> SummaryResult { get; set; } =
            Result<IReadOnlyList<EmployeeActivitySummaryView>>.Failure(DashboardErrors.DatabaseUnavailable);
        public Task<Result<IReadOnlyList<EmployeeActivitySummaryView>>> GetSummariesAsync(EmployeeActivityPeriod period, CancellationToken cancellationToken = default)
        {
            SummaryCalls++;
            return Task.FromResult(SummaryResult);
        }
        public Task<Result<EmployeeActivityDetailView>> GetEmployeeAsync(long userId, EmployeeActivityPeriod period, CancellationToken cancellationToken = default)
        {
            DetailCalls++;
            return Task.FromResult(Result<EmployeeActivityDetailView>.Failure(DashboardErrors.DatabaseUnavailable));
        }
    }
}
