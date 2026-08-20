using System.Text.RegularExpressions;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class AppShellTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Shell_IsSplitIntoFocusedLayoutComponents()
    {
        var expectedFiles = new[]
        {
            "AppShellLayout.razor",
            "AppSidebar.razor",
            "AppTopBar.razor",
            "AppStatusBar.razor",
            "DevelopmentAccessPreview.razor"
        };

        Assert.All(
            expectedFiles,
            file => Assert.True(
                File.Exists(DesktopPath("Components", "Layout", file)),
                $"Missing shell component: {file}"));
    }

    [Fact]
    public void Shell_ContainsSidebarTopbarMainStatusbarAndSkipLink()
    {
        var source = DesktopText("Components", "Layout", "AppShellLayout.razor");

        Assert.Contains("<AppSidebar", source, StringComparison.Ordinal);
        Assert.Contains("<AppTopBar", source, StringComparison.Ordinal);
        Assert.Contains("<main id=\"main-content\"", source, StringComparison.Ordinal);
        Assert.Contains("<AppStatusBar", source, StringComparison.Ordinal);
        Assert.Contains("class=\"skip-link\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NavigationCatalog_IsCentralAndTyped()
    {
        var itemSource = DesktopText("Components", "Navigation", "AppNavigationItem.cs");
        var catalogSource = DesktopText("Components", "Navigation", "AppNavigationCatalog.cs");

        Assert.Contains("sealed record AppNavigationItem", itemSource, StringComparison.Ordinal);
        Assert.Contains("string Id", itemSource, StringComparison.Ordinal);
        Assert.Contains("string Label", itemSource, StringComparison.Ordinal);
        Assert.Contains("string Route", itemSource, StringComparison.Ordinal);
        Assert.Contains("string Description", itemSource, StringComparison.Ordinal);
        Assert.Contains("AppNavigationGroup Group", itemSource, StringComparison.Ordinal);
        Assert.Contains("UiIconName Icon", itemSource, StringComparison.Ordinal);
        Assert.Contains("AppNavigationAccessState InitialAccessState", itemSource, StringComparison.Ordinal);
        Assert.Contains("AppNavigationKind Kind", itemSource, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<AppNavigationItem> Items", catalogSource, StringComparison.Ordinal);
    }

    [Fact]
    public void NavigationIdsAndRoutes_AreUnique()
    {
        var entries = GetCatalogEntries();

        Assert.Equal(entries.Length, entries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(entries.Length, entries.Select(entry => entry.Route).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void NavigationLabelsAndRoutes_AreNotEmptyAndRoutesAreLocal()
    {
        var entries = GetCatalogEntries();

        Assert.NotEmpty(entries);
        Assert.All(entries, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Id));
            Assert.False(string.IsNullOrWhiteSpace(entry.Label));
            Assert.StartsWith("/", entry.Route, StringComparison.Ordinal);
            Assert.DoesNotContain("://", entry.Route, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void BusinessNavigationEntries_AreDeclared()
    {
        var expectedIds = new[]
        {
            "home",
            "sales",
            "catalog",
            "inventory",
            "purchasing",
            "cash",
            "customers",
            "reporting",
            "administration",
            "operations",
            "audit"
        };
        var entries = GetCatalogEntries();

        Assert.Equal(13, entries.Length);
        Assert.All(expectedIds, id => Assert.Contains(entries, entry => entry.Id == id));
    }

    [Fact]
    public void RequiredNavigationGroups_AreDeclaredInFrench()
    {
        var source = DesktopText("Components", "Navigation", "AppNavigationCatalog.cs");
        var labels = new[] { "Activité", "Gestion", "Suivi", "Configuration", "Outils internes" };

        Assert.All(labels, label => Assert.Contains($"\"{label}\"", source, StringComparison.Ordinal));
    }

    [Fact]
    public void CatalogRoutes_HaveAnInitializableRazorRoute()
    {
        var routeSources = Directory
            .EnumerateFiles(DesktopPath("Components"), "*.razor", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();
        var declaredRoutes = routeSources
            .SelectMany(source => Regex.Matches(source, "@page\\s+\\\"(?<route>[^\\\"]+)\\\""))
            .Select(match => match.Groups["route"].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("/module/{ModuleId}", declaredRoutes);

        foreach (var entry in GetCatalogEntries())
        {
            if (entry.Route.StartsWith("/module/", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.Contains(entry.Route, declaredRoutes);
        }
    }

    [Fact]
    public void DevelopmentTools_AreTechnicalAndConditionallyCatalogued()
    {
        var source = DesktopText("Components", "Navigation", "AppNavigationCatalog.cs");

        Assert.Contains("#if DEBUG", source, StringComparison.Ordinal);
        Assert.Contains("\"diagnostic\"", source, StringComparison.Ordinal);
        Assert.Contains("\"ui-lab\"", source, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(source, "AppNavigationKind\\.Technical").Count);
    }

    [Fact]
    public void SidebarCollapseControl_ExposesItsStateAndTarget()
    {
        var source = DesktopText("Components", "Layout", "AppSidebar.razor");

        Assert.Contains("aria-controls=\"primary-navigation\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-expanded=\"@ExpandedValue\"", source, StringComparison.Ordinal);
        Assert.Contains("Déployer la barre latérale", source, StringComparison.Ordinal);
        Assert.Contains("Réduire la barre latérale", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LockedNavigation_IsRenderedAsADisabledButton()
    {
        var source = DesktopText("Components", "Layout", "AppSidebar.razor");

        Assert.Contains("state == AppNavigationAccessState.Locked", source, StringComparison.Ordinal);
        Assert.Contains("class=\"app-navigation__link app-navigation__link--locked\"", source, StringComparison.Ordinal);
        Assert.Contains("disabled", source, StringComparison.Ordinal);
        Assert.Contains("UiIconName.Lock", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Topbar_ShowsAuthenticatedAccountWithoutTechnicalContext()
    {
        var source = DesktopText("Components", "Layout", "AppTopBar.razor");

        Assert.Contains("@PageTitle", source, StringComparison.Ordinal);
        Assert.Contains("@PageDescription", source, StringComparison.Ordinal);
        Assert.Contains("Session.User?.DisplayName", source, StringComparison.Ordinal);
        Assert.Contains("Verrouiller", source, StringComparison.Ordinal);
        Assert.Contains("Déconnexion", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Mode local", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Espace de travail", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Statusbar_ShowsOnlyAnUnderstandableReadyState()
    {
        var source = DesktopText("Components", "Layout", "AppStatusBar.razor");

        Assert.Contains("Application prête", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Version", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SQL", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BuildConfiguration", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Home_IsAConciseOperationalEntryPoint()
    {
        var source = DesktopText("Components", "Pages", "Home.razor");

        Assert.Contains(">Accueil</h1>", source, StringComparison.Ordinal);
        Assert.Contains(">Accès directs</h2>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("démonstration", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sprint", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SQL", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HomePresentationModel_DoesNotContainSyntheticDashboardData()
    {
        var source = DesktopText("Components", "Pages", "HomePresentationModel.cs");

        Assert.DoesNotContain("DemoHomeMetric", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DemoRecentActivity", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DemoSystemState", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Valeur de démonstration", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HomeQuickActions_TargetTheFourPrimaryOperationalModules()
    {
        var source = DesktopText("Components", "Pages", "HomePresentationModel.cs");
        var expectedRoutes = new[]
        {
            "/module/sales",
            "/module/catalog",
            "/module/inventory",
            "/module/purchasing"
        };

        Assert.All(expectedRoutes, route => Assert.Contains($"\"{route}\"", source, StringComparison.Ordinal));
        Assert.Equal(4, Regex.Matches(source, "new DemoQuickAction\\(").Count);
    }

    [Fact]
    public void Home_DoesNotExposeSyntheticRecentActivity()
    {
        var page = DesktopText("Components", "Pages", "Home.razor");
        var source = DesktopText("Components", "Pages", "HomePresentationModel.cs");

        Assert.DoesNotContain("RecentActivity", page, StringComparison.Ordinal);
        Assert.DoesNotContain("RecentActivity", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DEMO-", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Home_DoesNotExposeTechnicalSystemState()
    {
        var page = DesktopText("Components", "Pages", "Home.razor");
        var source = DesktopText("Components", "Pages", "HomePresentationModel.cs");

        Assert.DoesNotContain("SystemStates", page, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemStates", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SQL", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Non configur", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModulePlaceholder_StatesUnavailabilityWithoutDevelopmentJargon()
    {
        var source = DesktopText("Components", "Pages", "ModulePlaceholder.razor");

        Assert.Contains("@page \"/module/{ModuleId}\"", source, StringComparison.Ordinal);
        Assert.Contains("FindModule(ModuleId)", source, StringComparison.Ordinal);
        Assert.Contains("Non disponible", source, StringComparison.Ordinal);
        Assert.Contains("Fonction non disponible", source, StringComparison.Ordinal);
        Assert.DoesNotContain("provisoire", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sprint", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AccessPreview_HasThreeNeutralProfilesAndTheRequiredWarning()
    {
        var source = DesktopText("Components", "Navigation", "DevelopmentAccessCatalog.cs");

        Assert.Contains("\"Accès complet\"", source, StringComparison.Ordinal);
        Assert.Contains("\"Opérations\"", source, StringComparison.Ordinal);
        Assert.Contains("\"Lecture\"", source, StringComparison.Ordinal);
        Assert.Contains(
            "Profils internes sans effet sur les permissions métier",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Démo", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("return AppNavigationAccessState.Locked", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Icons_AreLocalInlineDecorativeSvg()
    {
        var source = DesktopText("Components", "Ui", "UiIcon.razor");
        var requiredIcons = new[]
        {
            "Home", "Sales", "Catalog", "Inventory", "Purchasing", "Cash", "Customers",
            "Reporting", "Administration", "Diagnostic", "UiLab", "Chevron", "Menu", "Lock"
        };

        Assert.Contains("<svg", source, StringComparison.Ordinal);
        Assert.Contains("aria-hidden=\"true\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("http://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", source, StringComparison.OrdinalIgnoreCase);
        Assert.All(requiredIcons, icon => Assert.Contains($"UiIconName.{icon}", source, StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownRoute_RendersAClearNotFoundView()
    {
        var pageSource = DesktopText("Components", "Pages", "NotFound.razor");
        var routerSource = DesktopText("Components", "RootComponent.razor");

        Assert.Contains("Page introuvable", pageSource, StringComparison.Ordinal);
        Assert.Contains("Revenir à l’accueil", pageSource, StringComparison.Ordinal);
        Assert.Contains("<NotFound>", routerSource, StringComparison.Ordinal);
        Assert.Contains("Components.Pages.NotFound", routerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticAndUiLabRoutes_AreRetained()
    {
        var diagnostic = DesktopText("Components", "DiagnosticScreen.razor");
        var uiLab = DesktopText("Components", "Pages", "UiLab.razor");

        Assert.Contains("@page \"/diagnostic\"", diagnostic, StringComparison.Ordinal);
        Assert.Contains("Diagnostic technique — Catalogue, Stock et Purchasing SQL", diagnostic, StringComparison.Ordinal);
        Assert.Contains("@page \"/ui-lab\"", uiLab, StringComparison.Ordinal);
        Assert.Contains("UI Lab — Fondations visuelles V0.1", uiLab, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopSource_DoesNotIntroducePersistenceOrServerInfrastructure()
    {
        var prohibitedTokens = new[]
        {
            "DbContext",
            "EntityFrameworkCore",
            "SqlConnection",
            "UseSqlServer",
            "ConnectionString",
            "WebApplication.CreateBuilder",
            "Kestrel",
            "localhost"
        };
        var source = string.Join(
            Environment.NewLine,
            EnumerateDesktopSourceFiles().Select(File.ReadAllText));

        Assert.All(
            prohibitedTokens,
            token => Assert.DoesNotContain(token, source, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ShellCss_CoversCompactResponsiveFocusAndReducedMotionStates()
    {
        var source = DesktopText("wwwroot", "css", "shell.css");

        Assert.Contains(".app-shell--collapsed", source, StringComparison.Ordinal);
        Assert.Contains(".app-navigation__link--active", source, StringComparison.Ordinal);
        Assert.Contains(".app-navigation__link--locked", source, StringComparison.Ordinal);
        Assert.Contains("@media (max-width:", source, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", source, StringComparison.Ordinal);
        Assert.Contains("minmax(0, 1fr)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellCss_IsComposedByTheExistingLocalStylesheet()
    {
        var appCss = DesktopText("wwwroot", "css", "app.css");

        Assert.Contains("@import url(\"./shell.css\")", appCss, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", appCss, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", appCss, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestSources_DoNotContainMachineSpecificAbsolutePaths()
    {
        var testFiles = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path));
        var violations = testFiles
            .Where(path => Regex.IsMatch(File.ReadAllText(path), @"[A-Za-z]:\\Users\\"))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(violations);
    }

    private static CatalogEntry[] GetCatalogEntries()
    {
        var source = DesktopText("Components", "Navigation", "AppNavigationCatalog.cs");
        var pattern = new Regex(
            @"new(?:\s+AppNavigationItem)?\s*\(\s*""(?<id>[^""]+)""\s*,\s*""(?<label>[^""]+)""\s*,\s*""(?<route>[^""]+)""",
            RegexOptions.CultureInvariant);

        return pattern
            .Matches(source)
            .Select(match => new CatalogEntry(
                match.Groups["id"].Value,
                match.Groups["label"].Value,
                match.Groups["route"].Value))
            .ToArray();
    }

    private static IEnumerable<string> EnumerateDesktopSourceFiles()
    {
        return Directory
            .EnumerateFiles(DesktopPath(), "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Where(path => !IsGeneratedPath(path));
    }

    private static string DesktopText(params string[] segments)
    {
        return File.ReadAllText(DesktopPath(segments));
    }

    private static string DesktopPath(params string[] segments)
    {
        return Path.Combine(
            new[] { RepositoryRoot, "src", "Pharmacy.Desktop" }.Concat(segments).ToArray());
    }

    private static bool IsGeneratedPath(string path)
    {
        var relativePath = Path.GetRelativePath(RepositoryRoot, path);
        return relativePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("obj", StringComparison.OrdinalIgnoreCase));
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

    private sealed record CatalogEntry(string Id, string Label, string Route);
}
