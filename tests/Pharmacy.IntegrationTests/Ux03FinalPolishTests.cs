using Pharmacy.Application.Security;
using Pharmacy.Desktop.Components.Navigation;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class Ux03FinalPolishTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void UX0301_TopbarSeparatesDisplayNameAndHumanRole()
    {
        var topbar = DesktopText("Components", "Layout", "AppTopBar.razor");
        var css = DesktopText("wwwroot", "css", "shell.css");

        Assert.Contains("class=\"app-topbar__identity\"", topbar, StringComparison.Ordinal);
        Assert.Contains("<strong>@Session.User?.DisplayName</strong>", topbar, StringComparison.Ordinal);
        Assert.Contains("<small>@RoleLabel</small>", topbar, StringComparison.Ordinal);
        Assert.Contains(".app-topbar__identity", css, StringComparison.Ordinal);
        Assert.Contains("display: grid", Rule(css, ".app-topbar__identity"), StringComparison.Ordinal);
        Assert.Contains("Security.RoleLabel", topbar, StringComparison.Ordinal);
    }

    [Fact]
    public void UX0302_TechnicalLoginRemainsHiddenFromTopbar()
    {
        var topbar = DesktopText("Components", "Layout", "AppTopBar.razor");

        Assert.DoesNotContain("Session.User?.Login", topbar, StringComparison.Ordinal);
        Assert.DoesNotContain("Session.User.Login", topbar, StringComparison.Ordinal);
        Assert.Contains("Verrouiller", topbar, StringComparison.Ordinal);
        Assert.Contains("Déconnexion", topbar, StringComparison.Ordinal);
    }

    [Fact]
    public void UX0303_ReadOnlyCatalogSubtitleSaysConsultNotManage()
    {
        var text = Resolve("catalog", SecurityPermissions.CatalogView);

        Assert.Equal("Rechercher et consulter les produits.", text);
        Assert.DoesNotContain("gérer", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UX0304_CatalogManagerSubtitleMaySayManage()
    {
        Assert.Equal(
            "Rechercher et gérer les produits.",
            Resolve("catalog", SecurityPermissions.CatalogView, SecurityPermissions.CatalogManage));
    }

    [Fact]
    public void UX0305_CustomerSubtitleReflectsEffectivePermissions()
    {
        Assert.Equal(
            "Rechercher et consulter les clients.",
            Resolve("customers", SecurityPermissions.CustomersView));
        Assert.Equal(
            "Rechercher et gérer les clients.",
            Resolve("customers", SecurityPermissions.CustomersView, SecurityPermissions.CustomersManage));
        Assert.Equal(
            "Gérer les clients, crédits et règlements.",
            Resolve(
                "customers",
                SecurityPermissions.CustomersView,
                SecurityPermissions.CustomersManage,
                SecurityPermissions.CustomersManageCredit));
    }

    [Fact]
    public void UX0306_TechnicalPermissionCodesAreHiddenFromAdministrationPresentation()
    {
        var page = DesktopText("Components", "Pages", "AdministrationPage.razor");
        var permissionCodes = typeof(SecurityPermissions)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && !field.IsInitOnly)
            .Select(field => Assert.IsType<string>(field.GetRawConstantValue()))
            .ToArray();

        Assert.DoesNotContain("<small>@permission.Code</small>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("(@permission.Code)", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<code>@role.Code</code>", page, StringComparison.Ordinal);
        Assert.Contains("SecurityPermissionPresentation.PermissionLabel(permission.Code)", page, StringComparison.Ordinal);
        Assert.NotEmpty(permissionCodes);
        Assert.All(permissionCodes, code =>
            Assert.NotEqual(code, SecurityPermissionPresentation.PermissionLabel(code)));
    }

    [Fact]
    public void UX0307_PermissionCategoryLabelsAreFrench()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["audit"] = "Audit",
            ["backup"] = "Sauvegardes",
            ["cash"] = "Caisse",
            ["catalog"] = "Produits",
            ["customers"] = "Clients",
            ["devices"] = "Matériel",
            ["import"] = "Imports",
            ["inventory"] = "Stock",
            ["purchasing"] = "Achats",
            ["reports"] = "Rapports",
            ["sales"] = "Ventes",
            ["security"] = "Sécurité"
        };

        Assert.All(expected, item =>
            Assert.Equal(item.Value, SecurityPermissionPresentation.ModuleLabel(item.Key)));
    }

    [Fact]
    public void UX0308_SecurityPermissionsJargonIsAbsent()
    {
        var page = DesktopText("Components", "Pages", "AdministrationPage.razor");

        Assert.DoesNotContain("<code>security.Permissions</code>", page, StringComparison.Ordinal);
        Assert.Contains("Une réauthentification est requise pour enregistrer.", page, StringComparison.Ordinal);
    }

    [Fact]
    public void UX0309_TechnicalRoleCodesAreNotRenderedInNormalMatrix()
    {
        var page = DesktopText("Components", "Pages", "AdministrationPage.razor");

        Assert.DoesNotContain("<code>@role.Code</code>", page, StringComparison.Ordinal);
        Assert.Contains("@AuthorizationCount(role.Permissions.Count)", page, StringComparison.Ordinal);
        Assert.Contains("Rôles et autorisations", page, StringComparison.Ordinal);
    }

    [Fact]
    public void UX0310_KeyboardFocusRemainsVisibleWhilePointerHeadingFocusIsQuiet()
    {
        var root = DesktopText("Components", "RootComponent.razor");
        var baseCss = DesktopText("wwwroot", "css", "base.css");
        var shellCss = DesktopText("wwwroot", "css", "shell.css");

        Assert.Contains(":focus-visible", baseCss, StringComparison.Ordinal);
        Assert.Contains("input-modality-keyboard", root, StringComparison.Ordinal);
        Assert.Contains("@onkeydown=\"HandleKeyboardInput\"", root, StringComparison.Ordinal);
        Assert.Contains("@onpointerdown=\"HandlePointerInput\"", root, StringComparison.Ordinal);
        Assert.Contains(".input-modality-pointer .page-header h1:focus-visible", shellCss, StringComparison.Ordinal);
        Assert.DoesNotContain(".input-modality-keyboard .page-header h1:focus-visible", shellCss, StringComparison.Ordinal);
    }

    private static string Resolve(string pageId, params string[] permissions)
    {
        var effective = permissions.ToHashSet(StringComparer.Ordinal);
        return PermissionAwarePageDescription.Resolve(
            pageId,
            "Description par défaut.",
            effective.Contains);
    }

    private static string Rule(string css, string selector)
    {
        var start = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Sélecteur introuvable : {selector}");
        var end = css.IndexOf('}', start);
        Assert.True(end > start, $"Règle incomplète : {selector}");
        return css[start..(end + 1)];
    }

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
