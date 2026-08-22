using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class AdministrationLayoutTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void AdministrationUsesIndependentNaturalHeightColumnsAndStandardHeading()
    {
        var page = DesktopText("Components", "Pages", "AdministrationPage.razor");
        var css = DesktopText("wwwroot", "css", "security.css");

        Assert.Contains("page-content catalog-page security-admin", page, StringComparison.Ordinal);
        Assert.Contains("page-header catalog-page-header", page, StringComparison.Ordinal);
        Assert.Contains("<h1 id=\"administration-title\">Administration</h1>", page, StringComparison.Ordinal);
        Assert.Equal(2, Count(page, "class=\"security-admin__column\""));
        Assert.Contains("grid-template-columns: minmax(0, 1fr) minmax(0, 1.1fr)", css, StringComparison.Ordinal);
        Assert.Contains("align-items: start", css, StringComparison.Ordinal);
        Assert.Contains(".security-admin__column { display: grid; min-width: 0; align-content: start; gap: var(--space-4); }", css, StringComparison.Ordinal);
        Assert.Contains(".security-admin .security-panel { padding: var(--space-4); }", css, StringComparison.Ordinal);
        Assert.Contains(".security-admin .security-panel form { gap: var(--space-3); margin-top: var(--space-4); }", css, StringComparison.Ordinal);
        Assert.DoesNotContain("security-panel--full", page, StringComparison.Ordinal);
        Assert.DoesNotContain("height: 100%", css, StringComparison.Ordinal);
        Assert.DoesNotContain("min-height:", AdministrationRules(css), StringComparison.Ordinal);
    }

    [Fact]
    public void AdministrationAccountsUseTheAvailableWidth()
    {
        var page = DesktopText("Components", "Pages", "AdministrationPage.razor");
        var css = DesktopText("wwwroot", "css", "security.css");

        Assert.Contains("security-panel security-panel--accounts", page, StringComparison.Ordinal);
        Assert.Contains("class=\"security-accounts-table\"", page, StringComparison.Ordinal);
        Assert.Contains("class=\"security-account-actions\"", page, StringComparison.Ordinal);
        Assert.Contains(".security-panel--accounts, .security-panel--permissions { width: 100%; }", css, StringComparison.Ordinal);
        Assert.Contains(".security-table-wrap { width: 100%; max-width: 100%; overflow-x: auto; }", css, StringComparison.Ordinal);
        Assert.Contains(".security-accounts-table { min-width: 78rem; table-layout: auto; }", css, StringComparison.Ordinal);
        Assert.Contains("overflow-x: auto", css, StringComparison.Ordinal);
    }

    [Fact]
    public void AdministrationPermissionsAndResponsiveLayoutRemainLocallyScoped()
    {
        var page = DesktopText("Components", "Pages", "AdministrationPage.razor");
        var css = DesktopText("wwwroot", "css", "security.css");

        Assert.Contains("security-panel security-panel--permissions", page, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(auto-fit, minmax(min(100%, 20rem), 1fr))", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 72rem)", css, StringComparison.Ordinal);
        Assert.Contains(".security-admin__grid { grid-template-columns: 1fr; }", css, StringComparison.Ordinal);
        Assert.DoesNotContain("overflow-x: hidden", css, StringComparison.Ordinal);
    }

    [Fact]
    public void AdministrationRoleEditorUsesPersistedPermissionsAndProtectsSystemRoles()
    {
        var page = DesktopText("Components", "Pages", "AdministrationPage.razor");
        var css = DesktopText("wwwroot", "css", "security.css");

        Assert.Contains("Security.Permissions.GroupBy", page, StringComparison.Ordinal);
        Assert.Contains("type=\"checkbox\"", page, StringComparison.Ordinal);
        Assert.Contains("Security.CreateRoleAsync", page, StringComparison.Ordinal);
        Assert.Contains("Security.UpdateRoleAsync", page, StringComparison.Ordinal);
        Assert.Contains("role.IsSystemRole", page, StringComparison.Ordinal);
        Assert.Contains("Système · lecture seule", page, StringComparison.Ordinal);
        Assert.Contains("security-permission-selector", css, StringComparison.Ordinal);
        Assert.DoesNotContain("ROLE-QA-LIMITE", page, StringComparison.Ordinal);
        Assert.DoesNotContain("sales.create", page, StringComparison.Ordinal);
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static string AdministrationRules(string css) =>
        string.Join('\n', css.Split('\n').Where(line => line.Contains("security-admin", StringComparison.Ordinal)));

    private static string DesktopText(params string[] segments) =>
        File.ReadAllText(Path.Combine(
            new[] { RepositoryRoot, "src", "Pharmacy.Desktop" }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Racine du dépôt introuvable.");
    }
}
