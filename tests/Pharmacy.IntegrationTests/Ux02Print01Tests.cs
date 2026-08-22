using Pharmacy.Application.Configuration;
using Pharmacy.Application.Operations.Contracts;
using Pharmacy.Application.Operations.Models;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Application.Security;
using Pharmacy.Desktop.Components.Configuration;
using Pharmacy.Desktop.Components.Pos;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class Ux02Print01Tests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void UX0201_NoPlannedCashEntryIsVisible()
    {
        var sidebar = DesktopText("Components", "Layout", "AppSidebar.razor");
        Assert.DoesNotContain("or \"cash\"", sidebar, StringComparison.Ordinal);
        Assert.Contains("\"/module/cash\"", DesktopText("Components", "Navigation", "AppNavigationCatalog.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void UX0202_MainSidebarContainsOnlyExpectedProductionModules()
    {
        var sidebar = DesktopText("Components", "Layout", "AppSidebar.razor");
        var expected = new[] { "home", "sales", "catalog", "inventory", "purchasing", "customers", "reporting", "administration" };
        Assert.All(expected, id => Assert.Contains($"\"{id}\"", sidebar, StringComparison.Ordinal));
        Assert.DoesNotContain("\"operations\"", sidebar, StringComparison.Ordinal);
        Assert.DoesNotContain("\"audit\"", sidebar, StringComparison.Ordinal);
    }

    [Fact]
    public void UX0203_BackupToolsRemainAccessibleFromAdministration()
    {
        var admin = DesktopText("Components", "Pages", "AdministrationPage.razor");
        Assert.Contains("href=\"/module/operations\"", admin, StringComparison.Ordinal);
        Assert.Contains("Sauvegardes et outils", admin, StringComparison.Ordinal);
    }

    [Fact]
    public void UX0204_PharmacyNameWrapsWithoutSidebarEllipsis()
    {
        var css = DesktopText("wwwroot", "css", "security.css");
        Assert.Contains(".pharmacy-brand-mark--sidebar .pharmacy-brand-mark__name { color:", css, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: normal", css, StringComparison.Ordinal);
        Assert.Contains("object-fit: contain", css, StringComparison.Ordinal);
    }

    [Fact]
    public void UX0205_TechnicalLoginIsNotVisibleInTopbar()
    {
        var topbar = DesktopText("Components", "Layout", "AppTopBar.razor");
        Assert.DoesNotContain("Session.User?.Login", topbar, StringComparison.Ordinal);
        Assert.Contains("@RoleLabel", topbar, StringComparison.Ordinal);
    }

    [Fact]
    public void UX0206_DashboardDecorativeSubtitlesAreRemoved()
    {
        var home = DesktopText("Components", "Pages", "Home.razor");
        Assert.DoesNotContain("Tableau de bord", home, StringComparison.Ordinal);
        Assert.DoesNotContain("L’essentiel de votre activité", home, StringComparison.Ordinal);
        Assert.Contains("<h1 id=\"dashboard-title\" tabindex=\"-1\">@Greeting</h1>", home, StringComparison.Ordinal);
    }

    [Fact]
    public void UX0207_ManagerMainKpisExcludeSessionDuration()
    {
        var home = DesktopText("Components", "Pages", "Home.razor");
        Assert.Contains("Dashboard.GlobalSales is null", home, StringComparison.Ordinal);
        Assert.Contains("Durée de session", home, StringComparison.Ordinal);
    }

    [Fact]
    public void UX0208_EmployeeChartUsesBrandingToken()
    {
        var css = DesktopText("wwwroot", "css", "dashboard.css");
        Assert.Contains(".dashboard-bars--employees .dashboard-bar-track > span { background: var(--pharmacy-primary); }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void UX0209_SaleReferenceIsShortenedWithoutChangingFullValue()
    {
        var component = DesktopText("Components", "Ui", "OperationalReference.razor");
        Assert.Contains("value[..13]", component, StringComparison.Ordinal);
        Assert.Contains("value[^4..]", component, StringComparison.Ordinal);
        Assert.Contains("<code>@Value</code>", component, StringComparison.Ordinal);
        Assert.Contains("user-select: all", DesktopText("wwwroot", "css", "shell.css"), StringComparison.Ordinal);
    }

    [Fact]
    public void UX0210_QuickActionsAreConcise()
    {
        var home = DesktopText("Components", "Pages", "Home.razor");
        Assert.Contains("<strong>@action.Label</strong>", home, StringComparison.Ordinal);
        Assert.DoesNotContain("@action.Description", home, StringComparison.Ordinal);
    }

    [Fact]
    public void UX0211_IdleReadyStatusIsHidden()
    {
        var status = DesktopText("Components", "Layout", "AppStatusBar.razor");
        Assert.DoesNotContain("Application prête", status, StringComparison.Ordinal);
        Assert.Contains("!string.IsNullOrWhiteSpace(Message)", status, StringComparison.Ordinal);
    }

    [Fact]
    public void UX0212_WindowTitleUsesRuntimeBranding()
    {
        var window = DesktopText("MainWindow.xaml.cs");
        Assert.Contains("runtimeTitle.PharmacyName", window, StringComparison.Ordinal);
        Assert.Contains("runtimeTitle.Changed += UpdateBrandedTitle", window, StringComparison.Ordinal);
    }

    [Fact]
    public void UX0213_VersionRemainsAvailableInDiagnostic()
    {
        var diagnostic = DesktopText("Components", "DiagnosticScreen.razor");
        Assert.Contains("PilotReleaseInfo.Version", diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void UX0214_CustomForbiddenJargonStillAbsentFromHomeAndTopbar()
    {
        var copy = DesktopText("Components", "Pages", "Home.razor")
            + DesktopText("Components", "Layout", "AppTopBar.razor");
        Assert.DoesNotContain("Prototype", copy, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sprint", copy, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PRINT001_DefaultReceiptPrintingIsDisabled()
    {
        Assert.False(ReceiptPrintingSettingsDefaults.ReceiptPrintingEnabled);
        Assert.Contains("values: new object[] { 1L, false", File.ReadAllText(MigrationFile()), StringComparison.Ordinal);
    }

    [Fact]
    public void PRINT002_SaleFinalizationPrecedesOptionalPrinting()
    {
        var source = DesktopText("Components", "Pos", "SalesPosCoordinator.cs");
        Assert.True(source.IndexOf("finalizeSaleHandler.HandleAsync", StringComparison.Ordinal)
            < source.IndexOf("if (ReceiptPrintingEnabled)", StringComparison.Ordinal));
    }

    [Fact]
    public void PRINT003_PaymentPersistenceDoesNotDependOnPrintingSetting()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Pharmacy.Application", "Sales", "Finalization", "Commands", "FinalizeSaleHandler.cs"));
        Assert.DoesNotContain("ReceiptPrinting", source, StringComparison.Ordinal);
        Assert.Contains("finalizer.FinalizeAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PRINT004_StockConsumptionDoesNotDependOnPrintingSetting()
    {
        var source = InfrastructureText("Sales", "SqlSaleFinalizer.cs");
        Assert.DoesNotContain("ReceiptPrinting", source, StringComparison.Ordinal);
        Assert.Contains("StockBalances", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PRINT005_PrinterIsNotInvokedWhenDisabled()
    {
        var printer = new RecordingPrinter();
        var service = new DemoTicketPrintService(printer, new FixedPolicy(false));
        var sale = CompletedSale();
        service.PrepareForSale(sale.Reference);

        Assert.False(await service.PrintPersistedAsync(sale, false, false, TestContext.Current.CancellationToken));
        Assert.Empty(printer.Requests);
    }

    [Fact]
    public void PRINT006_NoPrintAttemptStatusIsCreatedWhenDisabled()
    {
        var service = new DemoTicketPrintService(new RecordingPrinter(), new FixedPolicy(false));
        service.PrepareForSale("VTE-20260822-000000000000000000000001");
        Assert.Equal(DemoPrintStatus.NotRequested, service.Status);
        Assert.Equal(0, service.AttemptCount);
    }

    [Fact]
    public void PRINT007_SuccessUiOmitsTicketSectionWhenDisabled()
    {
        var source = DesktopText("Components", "Pos", "DemoPosSuccess.razor");
        Assert.Contains("@if (Session.ReceiptPrintingEnabled)", source, StringComparison.Ordinal);
        Assert.Contains("<section class=\"pos-ticket\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PRINT008_ReprintButtonIsInsideEnabledOnlySection()
    {
        AssertTicketCopyInsideConditional("Réimprimer le ticket");
    }

    [Fact]
    public void PRINT009_AttemptFactsAreInsideEnabledOnlySection()
    {
        AssertTicketCopyInsideConditional("Dernière tentative");
        AssertTicketCopyInsideConditional("Tentatives");
    }

    [Fact]
    public void PRINT010_NewSaleRemainsAvailableOutsideTicketSection()
    {
        var source = DesktopText("Components", "Pos", "DemoPosSuccess.razor");
        Assert.True(source.LastIndexOf("Nouvelle vente", StringComparison.Ordinal)
            > source.LastIndexOf("Dernière tentative", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PRINT011_PrintingSettingPersistsAcrossCoordinatorRestart()
    {
        var service = new MemorySettingsService();
        var first = new ReceiptPrintingSettingsCoordinator(service);
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(await first.UpdateAsync(true, TestContext.Current.CancellationToken));

        var restarted = new ReceiptPrintingSettingsCoordinator(service);
        await restarted.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(restarted.ReceiptPrintingEnabled);
    }

    [Fact]
    public void PRINT012_ManagerSettingUsesAuthorizedDeviceConfigurationService()
    {
        var service = InfrastructureText("Configuration", "SqlReceiptPrintingSettingsService.cs");
        Assert.Contains("SecurityPermissions.DevicesConfigure", service, StringComparison.Ordinal);
        Assert.Contains("UpdateAsync", DesktopText("Components", "Pages", "AdministrationPage.razor"), StringComparison.Ordinal);
    }

    [Fact]
    public void PRINT013_SellerCannotChangePrintingSetting()
    {
        Assert.DoesNotContain(SecurityPermissions.DevicesConfigure, SecurityRolePresets.SellerPermissions);
        Assert.Contains(SecurityPermissions.DevicesConfigure, SecurityRolePresets.ManagerPermissions);
    }

    [Fact]
    public async Task PRINT014_EnabledPrintingRestoresPostCommitPrinterWorkflow()
    {
        var printer = new RecordingPrinter();
        var service = new DemoTicketPrintService(printer, new FixedPolicy(true));
        var sale = CompletedSale();
        service.PrepareForSale(sale.Reference);
        Assert.True(await service.PrintPersistedAsync(sale, false, false, TestContext.Current.CancellationToken));
        Assert.Single(printer.Requests);
        Assert.Equal(DemoPrintStatus.Printed, service.Status);
    }

    [Fact]
    public async Task PRINT015_EnabledPrinterFailureKeepsSaleSnapshot()
    {
        var sale = CompletedSale();
        var service = new DemoTicketPrintService(new ThrowingPrinter(), new FixedPolicy(true));
        service.PrepareForSale(sale.Reference);
        Assert.True(await service.PrintPersistedAsync(sale, false, false, TestContext.Current.CancellationToken));
        Assert.Equal(DemoPrintStatus.Failed, service.Status);
        Assert.Equal(31.40m, sale.Total.Amount);
    }

    [Fact]
    public async Task PRINT016_ReprintDoesNotDuplicateSalePaymentOrStock()
    {
        var sale = CompletedSale();
        var printer = new RecordingPrinter();
        var service = new DemoTicketPrintService(printer, new FixedPolicy(true));
        service.PrepareForSale(sale.Reference);
        await service.PrintPersistedAsync(sale, false, false, TestContext.Current.CancellationToken);
        await service.PrintPersistedAsync(sale, true, false, TestContext.Current.CancellationToken);
        Assert.Equal(2, printer.Requests.Count);
        Assert.Single(sale.Lines);
        Assert.Equal(31.40m, sale.Total.Amount);
    }

    [Fact]
    public async Task PRINT017_UnavailablePrinterDoesNotAffectSaleWhenDisabled()
    {
        var service = new DemoTicketPrintService(new ThrowingPrinter(), new FixedPolicy(false));
        var sale = CompletedSale();
        service.PrepareForSale(sale.Reference);
        Assert.False(await service.PrintPersistedAsync(sale, false, false, TestContext.Current.CancellationToken));
        Assert.Equal(31.40m, sale.Total.Amount);
    }

    [Fact]
    public void PRINT018_DevicePreflightDoesNotRequirePrinterWhenDisabled()
    {
        var source = InfrastructureText("Operations", "DeviceServices.cs");
        Assert.Contains("Aucune imprimante requise pour vendre", source, StringComparison.Ordinal);
        Assert.Contains("receiptPrintingPolicy?.ReceiptPrintingEnabled", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PRINT019_BackupRestoreHealthCheckIncludesPrintingSetting()
    {
        var source = InfrastructureText("Operations", "SqlBackupRecoveryService.cs");
        Assert.Contains("configuration.ApplicationSettings", source, StringComparison.Ordinal);
        Assert.Contains("ReceiptPrintingEnabled", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PRINT020_SettingChangeIsAuditedWithoutSensitiveData()
    {
        var source = InfrastructureText("Configuration", "SqlReceiptPrintingSettingsService.cs");
        Assert.Contains("receipt-printing.update", source, StringComparison.Ordinal);
        Assert.Contains("OldValueRedacted", source, StringComparison.Ordinal);
        Assert.Contains("NewValueRedacted", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", source, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertTicketCopyInsideConditional(string value)
    {
        var source = DesktopText("Components", "Pos", "DemoPosSuccess.razor");
        var start = source.IndexOf("@if (Session.ReceiptPrintingEnabled)", StringComparison.Ordinal);
        var copy = source.IndexOf(value, StringComparison.Ordinal);
        var actions = source.IndexOf("<div class=\"pos-success__actions\">", StringComparison.Ordinal);
        Assert.True(start >= 0 && copy > start && copy < actions);
    }

    private static SalesPosCompletedSale CompletedSale()
    {
        var line = new FinalizedSaleLineView(
            1,
            ProductId.Create(1).Value,
            ProductReference.Create("PROD-001").Value,
            "Produit QA",
            "Boîte",
            Money.Create(31.40m).Value,
            SaleQuantity.One,
            Money.Create(31.40m).Value,
            []);
        var completedAt = DateTime.UtcNow;
        return new SalesPosCompletedSale(new FinalizedSaleView(
            1,
            "VTE-20260822-E296A4F56FEB4F8AB31DF4811CC62077",
            Guid.NewGuid(),
            completedAt,
            Money.Create(31.40m).Value,
            1,
            [line],
            new SalePaymentView(
                1,
                SalePaymentMethod.Cash,
                Money.Create(31.40m).Value,
                100m,
                68.60m,
                false,
                completedAt)));
    }

    private static string MigrationFile() => Directory.GetFiles(
        Path.Combine(RepositoryRoot, "src", "Pharmacy.Infrastructure", "Persistence", "Migrations"),
        "*_ReceiptPrintingConfigurationV01.cs").Single(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal));

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

    private sealed record FixedPolicy(bool ReceiptPrintingEnabled) : IReceiptPrintingPolicy;

    private sealed class RecordingPrinter : IReceiptPrinter
    {
        public List<ReceiptPrintRequest> Requests { get; } = [];
        public Task<Result<ReceiptPrintResult>> PrintAsync(ReceiptPrintRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Result<ReceiptPrintResult>.Success(new ReceiptPrintResult(true, "Printed", null, Requests.Count)));
        }
    }

    private sealed class ThrowingPrinter : IReceiptPrinter
    {
        public Task<Result<ReceiptPrintResult>> PrintAsync(ReceiptPrintRequest request, CancellationToken cancellationToken = default) =>
            throw new IOException("Synthetic printer outage.");
    }

    private sealed class MemorySettingsService : IReceiptPrintingSettingsService
    {
        private ReceiptPrintingSettingsView value = new(false, DateTime.UnixEpoch, [1]);

        public Task<Result<ReceiptPrintingSettingsView>> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReceiptPrintingSettingsView>.Success(value with { RowVersion = value.RowVersion.ToArray() }));

        public Task<Result<ReceiptPrintingSettingsView>> UpdateAsync(UpdateReceiptPrintingSettingsRequest request, CancellationToken cancellationToken = default)
        {
            value = new(request.ReceiptPrintingEnabled, DateTime.UtcNow, [(byte)(value.RowVersion[0] + 1)]);
            return Task.FromResult(Result<ReceiptPrintingSettingsView>.Success(value));
        }
    }
}
