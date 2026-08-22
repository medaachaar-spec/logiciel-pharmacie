using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class CustomUxCleanupTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    private static readonly string[][] OperationalRazorFiles =
    [
        ["Components", "RootComponent.razor"],
        ["Components", "Layout", "AppShellLayout.razor"],
        ["Components", "Layout", "AppSidebar.razor"],
        ["Components", "Layout", "AppStatusBar.razor"],
        ["Components", "Layout", "AppTopBar.razor"],
        ["Components", "Security", "SecurityEntry.razor"],
        ["Components", "Security", "SecurityLockScreen.razor"],
        ["Components", "Security", "SecurityPasswordChangeRequired.razor"],
        ["Components", "Pages", "Home.razor"],
        ["Components", "Pages", "CatalogPage.razor"],
        ["Components", "Pages", "InventoryPage.razor"],
        ["Components", "Pages", "PurchasingPage.razor"],
        ["Components", "Pages", "CustomersPage.razor"],
        ["Components", "Pages", "ReportingPage.razor"],
        ["Components", "Pages", "AdministrationPage.razor"],
        ["Components", "Pages", "AuditPage.razor"],
        ["Components", "Pages", "OperationsPage.razor"],
        ["Components", "Pages", "ModulePlaceholder.razor"],
        ["Components", "Pages", "NotFound.razor"],
        ["Components", "Pages", "DemoPosPage.razor"],
        ["Components", "Pos", "DemoCatalogPanel.razor"],
        ["Components", "Pos", "DemoCartPanel.razor"],
        ["Components", "Pos", "DemoPaymentPanel.razor"],
        ["Components", "Pos", "DemoSuspendedSalesPanel.razor"],
        ["Components", "Pos", "DemoPosSuccess.razor"],
        ["Components", "Pos", "SaleReturnPanel.razor"]
    ];

    [Fact]
    public void CUSTOM001_NoSprintTextIsVisibleInTheNormalUserShell()
    {
        AssertAbsent(NormalUserCopy(), @"\bSprint\b");
        AssertAbsent(DesktopText("Components", "Navigation", "AppNavigationCatalog.cs"), @"\bSprint\b");
    }

    [Fact]
    public void CUSTOM002_NoPrototypeTextIsVisible()
    {
        AssertAbsent(NormalUserCopy(), @"\bPrototype\b");
        AssertAbsent(DesktopText("Components", "Navigation", "AppNavigationCatalog.cs"), @"\bPrototype\b");
    }

    [Fact]
    public void CUSTOM003_NoDemonstrationTextIsVisibleInOperationalScreens()
    {
        var copy = NormalUserCopy();

        AssertAbsent(copy, @"\bDémo\b");
        AssertAbsent(copy, @"\bDemo\b");
        AssertAbsent(copy, @"\bDémonstration\b");
    }

    [Fact]
    public void CUSTOM004_NoProvisionalPageTextIsVisible()
    {
        AssertAbsent(NormalUserCopy(), @"\bpage\s+provisoire\b");
    }

    [Fact]
    public void CUSTOM005_LoginContainsNoTechnicalJargon()
    {
        var sources = new[]
        {
            DesktopText("Components", "RootComponent.razor"),
            DesktopText("Components", "Security", "SecurityEntry.razor"),
            DesktopText("Components", "Security", "SecurityLockScreen.razor"),
            DesktopText("Components", "Security", "SecurityPasswordChangeRequired.razor")
        };
        var source = string.Join(Environment.NewLine, sources);
        var copy = VisibleCopy(sources);

        Assert.Contains("Connexion", source, StringComparison.Ordinal);
        Assert.Contains("Identifiant", source, StringComparison.Ordinal);
        Assert.Contains("Mot de passe", source, StringComparison.Ordinal);
        Assert.Contains("Se connecter", source, StringComparison.Ordinal);
        AssertAbsent(copy, @"\b(local|SQL|Sprint|Prototype|Démo|Demo|environnement technique)\b");
    }

    [Fact]
    public void CUSTOM006_HomeContainsNoDevelopmentJargonOrSyntheticDashboard()
    {
        var source = string.Join(
            Environment.NewLine,
            DesktopText("Components", "Pages", "Home.razor"),
            DesktopText("Components", "Pages", "HomePresentationModel.cs"));

        Assert.Contains("@Greeting", source, StringComparison.Ordinal);
        Assert.Contains("Accès directs", source, StringComparison.Ordinal);
        AssertAbsent(source, @"\b(développement|placeholder|provisoire|Sprint|Prototype|démonstration|synthétique|SQL)\b");
        Assert.DoesNotContain("DemoHomeMetric", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DemoRecentActivity", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DemoSystemState", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CUSTOM007_PosRetainsItsPrincipalActions()
    {
        var source = PosSource();
        var requiredCopy = new[]
        {
            "Recherche", "Panier", "Quantité", "Paiement", "Espèces", "TPE", "Crédit",
            "Suspendre", "Confirmer et enregistrer la vente", "Retour", "Réimprimer"
        };

        Assert.All(requiredCopy, value => Assert.Contains(value, source, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CUSTOM008_AdministrationRetainsItsSecurityFunctions()
    {
        var source = DesktopText("Components", "Pages", "AdministrationPage.razor");
        var requiredCopy = new[]
        {
            "Réauthentification", "Changer mon mot de passe", "Créer un compte",
            "Désactiver", "Réactiver", "Régénérer les codes", "Créer un rôle",
            "Autorisations du rôle", "Rôles et autorisations"
        };

        Assert.All(requiredCopy, value => Assert.Contains(value, source, StringComparison.Ordinal));
        Assert.Contains("Security.SetUserActiveAsync", source, StringComparison.Ordinal);
        Assert.Contains("Security.GenerateRecoveryCodesAsync", source, StringComparison.Ordinal);
        Assert.Contains("Security.SetUserRoleAsync", source, StringComparison.Ordinal);
        Assert.Contains("Security.UpdateRoleAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CUSTOM009_DiagnosticRetainsNecessaryTechnicalInformation()
    {
        var source = DesktopText("Components", "DiagnosticScreen.razor");
        var requiredInformation = new[]
        {
            "Diagnostic technique", "SQL Server", "Connectivité", "Migrations",
            "WPF", "Blazor Hybrid", "WebView2", "Version .NET", "Architecture",
            "Système d’exploitation", "Environnement de build"
        };

        Assert.All(requiredInformation, value => Assert.Contains(value, source, StringComparison.Ordinal));
        Assert.Contains("Aucun secret de connexion affiché", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CUSTOM010_MainRoutesRemainPermissionControlledAndReachable()
    {
        var catalog = DesktopText("Components", "Navigation", "AppNavigationCatalog.cs");
        var shell = DesktopText("Components", "Layout", "AppShellLayout.razor");
        var requiredRoutes = new[]
        {
            "/home", "/module/sales", "/module/catalog", "/module/inventory",
            "/module/purchasing", "/module/cash", "/module/customers",
            "/module/reporting", "/module/administration", "/module/operations",
            "/module/audit"
        };
        var requiredPermissions = new[]
        {
            "SalesCreate", "CatalogView", "InventoryView", "PurchasingView", "CashView",
            "CustomersView", "ReportsView", "SecurityManageUsers", "BackupView", "AuditView"
        };

        Assert.All(requiredRoutes, route => Assert.Contains($"\"{route}\"", catalog, StringComparison.Ordinal));
        Assert.All(requiredPermissions, permission => Assert.Contains($"SecurityPermissions.{permission}", catalog, StringComparison.Ordinal));
        Assert.Contains("PermissionAuthorizer.Authorize(item.RequiredPermission)", shell, StringComparison.Ordinal);
        Assert.Contains("FindByRoute(currentPath)", shell, StringComparison.Ordinal);
        Assert.Contains("FindModule(currentPath.Split", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void CUSTOM011_NoBusinessHandlerOrServiceWasChangedForUxCopy()
    {
        var changedBusinessFiles = GitOutput(
            "diff", "--name-only", "v1.0.0-pre-customization", "--",
            "src/Pharmacy.Domain", "src/Pharmacy.Application", "src/Pharmacy.Infrastructure");

        var allowedBrandingFiles = new[]
        {
            "src/Pharmacy.Application/Branding/",
            "src/Pharmacy.Application/Security/",
            "src/Pharmacy.Infrastructure/Branding/",
            "src/Pharmacy.Infrastructure/Security/",
            "src/Pharmacy.Infrastructure/Persistence/ConfigurationSchema.cs",
            "src/Pharmacy.Infrastructure/Persistence/Configurations/Configuration/",
            "src/Pharmacy.Infrastructure/Persistence/Configurations/Security/",
            "src/Pharmacy.Infrastructure/Persistence/Entities/Configuration/",
            "src/Pharmacy.Infrastructure/Persistence/Entities/Security/",
            "src/Pharmacy.Infrastructure/Persistence/Migrations/",
            "src/Pharmacy.Infrastructure/Persistence/PharmacyDbContext.cs",
            "src/Pharmacy.Infrastructure/Operations/SqlBackupRecoveryService.cs",
            "src/Pharmacy.Infrastructure/Operations/DeviceServices.cs",
            "src/Pharmacy.Application/Operations/Models/BackupModels.cs",
            "src/Pharmacy.Application/Sales/Contracts/ISalesReader.cs",
            "src/Pharmacy.Application/Sales/Contracts/Sprint14SalesContracts.cs",
            "src/Pharmacy.Application/Sales/Finalization/Commands/FinalizeSaleHandler.cs",
            "src/Pharmacy.Application/Sales/Finalization/Commands/ReturnSaleHandler.cs",
            "src/Pharmacy.Application/Sales/Finalization/Queries/GetFinalizedSalesHandlers.cs",
            "src/Pharmacy.Application/Sales/Finalization/Queries/GetSaleReturnPreparationHandler.cs",
            "src/Pharmacy.Application/Sales/Models/FinalizeSaleRequest.cs",
            "src/Pharmacy.Application/Sales/Models/Sprint14SalesModels.cs",
            "src/Pharmacy.Application/Catalog/Commands/CatalogCommandHandlers.cs",
            "src/Pharmacy.Application/Catalog/Queries/CatalogQueryHandlers.cs",
            "src/Pharmacy.Application/Customers/Commands/CustomerCommandHandlers.cs",
            "src/Pharmacy.Application/Customers/Queries/CustomerQueryHandlers.cs",
            "src/Pharmacy.Infrastructure/Persistence/Configurations/Sales/",
            "src/Pharmacy.Infrastructure/Persistence/Entities/Sales/",
            "src/Pharmacy.Infrastructure/Sales/SqlCustomerCreditCashStore.cs",
            "src/Pharmacy.Infrastructure/Sales/SqlSaleFinalizer.cs",
            "src/Pharmacy.Infrastructure/Sales/SqlSalesReader.cs",
            "src/Pharmacy.Application/Dashboard/",
            "src/Pharmacy.Infrastructure/Reporting/SqlDashboardService.cs",
            "src/Pharmacy.Infrastructure/Reporting/ReportingServiceCollectionExtensions.cs"
        };
        var unexpected = changedBusinessFiles.Split(
                ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(path => !allowedBrandingFiles.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.True(
            unexpected.Length == 0,
            $"CUSTOM-01 must not modify unrelated business handlers or services:{Environment.NewLine}{string.Join(Environment.NewLine, unexpected)}");
    }

    [Fact]
    public void CUSTOM012_NavigationRetainsEveryExistingModule()
    {
        var catalog = DesktopText("Components", "Navigation", "AppNavigationCatalog.cs");
        var expectedIds = new[]
        {
            "home", "sales", "catalog", "inventory", "purchasing", "cash", "customers",
            "reporting", "administration", "operations", "audit", "diagnostic", "ui-lab"
        };

        Assert.All(expectedIds, id => Assert.Contains($"\"{id}\"", catalog, StringComparison.Ordinal));
        Assert.Equal(13, Regex.Matches(catalog, @"new(?:\s+AppNavigationItem)?\s*\(\s*""[^""]+""").Count);
    }

    private static string NormalUserCopy()
    {
        return VisibleCopy(OperationalRazorFiles.Select(DesktopText).ToArray());
    }

    private static string PosSource()
    {
        return string.Join(
            Environment.NewLine,
            OperationalRazorFiles
                .Where(path => path.Contains("Pos", StringComparer.Ordinal)
                    || path.Last().Equals("DemoPosPage.razor", StringComparison.Ordinal))
                .Select(DesktopText));
    }

    private static string VisibleCopy(params string[] sources)
    {
        var text = new List<string>();

        foreach (var source in sources)
        {
            text.AddRange(
                Regex.Matches(source, @">(?<value>[^<]+)<", RegexOptions.CultureInvariant)
                    .Select(match => match.Groups["value"].Value.Trim())
                    .Where(IsVisibleLiteral));
            text.AddRange(
                Regex.Matches(
                        source,
                        @"(?:Title|Text|Label|LoadingText|HelpText|Description|Placeholder|placeholder|aria-label)=""(?<value>[^""]+)""",
                        RegexOptions.CultureInvariant)
                    .Select(match => match.Groups["value"].Value.Trim())
                    .Where(IsVisibleLiteral));
        }

        return string.Join(Environment.NewLine, text);
    }

    private static bool IsVisibleLiteral(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !value.Contains('@', StringComparison.Ordinal)
            && !value.Contains('{', StringComparison.Ordinal)
            && !value.Contains('}', StringComparison.Ordinal);
    }

    private static void AssertAbsent(string source, string pattern)
    {
        Assert.False(
            Regex.IsMatch(source, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            $"Unexpected visible copy matching /{pattern}/.");
    }

    private static string GitOutput(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start Git for the CUSTOM-01 scope guard.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"Git scope guard failed: {error}");
        return output.Trim();
    }

    private static string DesktopText(params string[] segments)
    {
        return File.ReadAllText(Path.Combine(
            new[] { RepositoryRoot, "src", "Pharmacy.Desktop" }.Concat(segments).ToArray()));
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startPath);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("The repository root containing Pharmacy.slnx was not found.");
    }
}
