using Pharmacy.Application.Dashboard.Contracts;
using Pharmacy.Application.Dashboard.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;

namespace Pharmacy.Desktop.Components.Dashboard;

public sealed class DashboardCoordinator
{
    private readonly IDashboardService dashboard;
    private readonly IEmployeeActivityReader employeeActivity;
    private readonly ICurrentSecuritySession session;
    private readonly IPermissionAuthorizer authorizer;
    private readonly ISecurityClock clock;
    private int operationInProgress;

    public DashboardCoordinator(
        IDashboardService dashboard,
        IEmployeeActivityReader employeeActivity,
        ICurrentSecuritySession session,
        IPermissionAuthorizer authorizer,
        ISecurityClock clock)
    {
        this.dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
        this.employeeActivity = employeeActivity ?? throw new ArgumentNullException(nameof(employeeActivity));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public int SelectedDays { get; private set; } = 7;
    public bool IsBusy { get; private set; }
    public DateTime? LoadedAtUtc { get; private set; }

    public bool CanCreateSale { get; private set; }
    public bool CanViewPersonalSales { get; private set; }
    public bool CanViewGlobalSales { get; private set; }
    public bool CanViewCredit { get; private set; }
    public bool CanViewInventory { get; private set; }
    public bool CanViewEmployeeActivity { get; private set; }

    public DashboardSalesView? GlobalSales { get; private set; }
    public DashboardCreditView? Credit { get; private set; }
    public DashboardInventoryView? Inventory { get; private set; }
    public IReadOnlyList<EmployeeActivitySummaryView>? Employees { get; private set; }
    public EmployeeActivityDetailView? PersonalActivity { get; private set; }

    public string? GlobalSalesError { get; private set; }
    public string? CreditError { get; private set; }
    public string? InventoryError { get; private set; }
    public string? EmployeesError { get; private set; }
    public string? PersonalError { get; private set; }

    public UserSessionView? CurrentUser => session.User;
    public bool IsGlobalExperience => CanViewGlobalSales || CanViewEmployeeActivity;
    public bool HasAnyContent => GlobalSales is not null
        || Credit is not null
        || Inventory is not null
        || Employees is not null
        || PersonalActivity is not null;

    public int PersonalTodaySaleCount => PersonalTodaySales.Count;
    public decimal PersonalTodaySalesAmount => PersonalTodaySales.Sum(item => item.Amount);
    public IReadOnlyList<EmployeeSaleHistoryView> RecentPersonalSales =>
        PersonalActivity?.Sales.Take(5).ToArray() ?? Array.Empty<EmployeeSaleHistoryView>();

    public TimeSpan? CurrentSessionDuration
    {
        get
        {
            var startedAt = PersonalActivity?.Summary.CurrentSessionStartedAtUtc
                ?? CurrentUser?.AuthenticatedAtUtc;
            var end = LoadedAtUtc;
            return startedAt.HasValue && end.HasValue && end >= startedAt
                ? end.Value - startedAt.Value
                : null;
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref operationInProgress, 1, 0) != 0)
        {
            return;
        }

        IsBusy = true;
        ResetContent();
        RefreshPermissions();
        try
        {
            var tasks = new List<Task>(5);
            if (CanViewGlobalSales) tasks.Add(LoadGlobalSalesAsync(cancellationToken));
            if (CanViewCredit) tasks.Add(LoadCreditAsync(cancellationToken));
            if (CanViewInventory) tasks.Add(LoadInventoryAsync(cancellationToken));
            if (CanViewEmployeeActivity) tasks.Add(LoadEmployeesAsync(cancellationToken));
            if (CanViewPersonalSales && CurrentUser is not null) tasks.Add(LoadPersonalAsync(CurrentUser.UserId, cancellationToken));

            await Task.WhenAll(tasks);
            LoadedAtUtc = clock.UtcNow;
        }
        finally
        {
            IsBusy = false;
            Interlocked.Exchange(ref operationInProgress, 0);
        }
    }

    public async Task SelectPeriodAsync(int days, CancellationToken cancellationToken = default)
    {
        if (days is not (7 or 30) || days == SelectedDays)
        {
            return;
        }

        SelectedDays = days;
        await LoadAsync(cancellationToken);
    }

    private async Task LoadGlobalSalesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await dashboard.GetGlobalSalesAsync(
                new DashboardSalesQuery(SelectedDays), cancellationToken);
            if (result.IsSuccess) GlobalSales = result.Value;
            else GlobalSalesError = result.Error.Message;
        }
        catch (Exception)
        {
            GlobalSalesError = "Les indicateurs de vente sont temporairement indisponibles.";
        }
    }

    private async Task LoadCreditAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await dashboard.GetOpenCreditAsync(cancellationToken);
            if (result.IsSuccess) Credit = result.Value;
            else CreditError = result.Error.Message;
        }
        catch (Exception)
        {
            CreditError = "Le crédit clients est temporairement indisponible.";
        }
    }

    private async Task LoadInventoryAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await dashboard.GetInventoryAlertsAsync(cancellationToken: cancellationToken);
            if (result.IsSuccess) Inventory = result.Value;
            else InventoryError = result.Error.Message;
        }
        catch (Exception)
        {
            InventoryError = "Les alertes de stock sont temporairement indisponibles.";
        }
    }

    private async Task LoadEmployeesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await employeeActivity.GetSummariesAsync(SelectedPeriod(), cancellationToken);
            if (result.IsSuccess) Employees = result.Value;
            else EmployeesError = result.Error.Message;
        }
        catch (Exception)
        {
            EmployeesError = "La comparaison des employés est temporairement indisponible.";
        }
    }

    private async Task LoadPersonalAsync(long userId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await employeeActivity.GetEmployeeAsync(userId, PersonalPeriod(), cancellationToken);
            if (result.IsSuccess) PersonalActivity = result.Value;
            else PersonalError = result.Error.Message;
        }
        catch (Exception)
        {
            PersonalError = "Vos ventes récentes sont temporairement indisponibles.";
        }
    }

    private void RefreshPermissions()
    {
        CanCreateSale = Has(SecurityPermissions.SalesCreate);
        CanViewPersonalSales = Has(SecurityPermissions.SalesViewHistory);
        CanViewGlobalSales = Has(SecurityPermissions.ReportsView);
        CanViewCredit = Has(SecurityPermissions.CustomersManageCredit);
        CanViewInventory = Has(SecurityPermissions.InventoryView);
        CanViewEmployeeActivity = Has(SecurityPermissions.SecurityViewEmployeeActivity);
    }

    private bool Has(string permission) => authorizer.Authorize(permission).IsSuccess;

    private EmployeeActivityPeriod SelectedPeriod()
    {
        var today = DateTime.SpecifyKind(clock.UtcNow.Date, DateTimeKind.Utc);
        return new EmployeeActivityPeriod(today.AddDays(-(SelectedDays - 1)), today.AddDays(1));
    }

    private EmployeeActivityPeriod PersonalPeriod()
    {
        var today = DateTime.SpecifyKind(clock.UtcNow.Date, DateTimeKind.Utc);
        return new EmployeeActivityPeriod(today.AddDays(-6), today.AddDays(1));
    }

    private IReadOnlyList<EmployeeSaleHistoryView> PersonalTodaySales
    {
        get
        {
            if (PersonalActivity is null) return Array.Empty<EmployeeSaleHistoryView>();
            var today = DateTime.SpecifyKind((LoadedAtUtc ?? clock.UtcNow).Date, DateTimeKind.Utc);
            var tomorrow = today.AddDays(1);
            return PersonalActivity.Sales
                .Where(item => item.CompletedAtUtc >= today && item.CompletedAtUtc < tomorrow)
                .ToArray();
        }
    }

    private void ResetContent()
    {
        LoadedAtUtc = null;
        GlobalSales = null;
        Credit = null;
        Inventory = null;
        Employees = null;
        PersonalActivity = null;
        GlobalSalesError = null;
        CreditError = null;
        InventoryError = null;
        EmployeesError = null;
        PersonalError = null;
    }
}
