using Pharmacy.Desktop.Components.Ui;

namespace Pharmacy.Desktop.Components.Navigation;

public static class AppNavigationCatalog
{
    public static IReadOnlyList<AppNavigationItem> Items { get; } = CreateItems();

    public static AppNavigationItem Home => Items.Single(item => item.Id == "home");

    public static AppNavigationItem? FindByRoute(string route)
    {
        var normalizedRoute = NormalizeRoute(route);

        if (normalizedRoute == "/")
        {
            return Home;
        }

        return Items.FirstOrDefault(
            item => string.Equals(item.Route, normalizedRoute, StringComparison.OrdinalIgnoreCase));
    }

    public static AppNavigationItem? FindModule(string? moduleId)
    {
        return Items.FirstOrDefault(
            item => string.Equals(item.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase));
    }

    public static string GetGroupLabel(AppNavigationGroup group)
    {
        return group switch
        {
            AppNavigationGroup.Activity => "Activité",
            AppNavigationGroup.Management => "Gestion",
            AppNavigationGroup.Steering => "Pilotage",
            AppNavigationGroup.Configuration => "Configuration",
            AppNavigationGroup.Development => "Développement",
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, null)
        };
    }

    public static int GetGroupOrder(AppNavigationGroup group)
    {
        return group switch
        {
            AppNavigationGroup.Activity => 0,
            AppNavigationGroup.Management => 1,
            AppNavigationGroup.Steering => 2,
            AppNavigationGroup.Configuration => 3,
            AppNavigationGroup.Development => 4,
            _ => int.MaxValue
        };
    }

    private static IReadOnlyList<AppNavigationItem> CreateItems()
    {
        var items = new List<AppNavigationItem>
        {
            new(
                "home",
                "Accueil",
                "/home",
                "Vue d’ensemble locale et entièrement synthétique.",
                AppNavigationGroup.Activity,
                UiIconName.Home,
                0,
                AppNavigationAccessState.Available,
                false,
                AppNavigationKind.Business),
            new(
                "sales",
                "Ventes",
                "/module/sales",
                "POS de démonstration local, prévu pour un usage clavier et scanner.",
                AppNavigationGroup.Activity,
                UiIconName.Sales,
                1,
                AppNavigationAccessState.Available,
                false,
                AppNavigationKind.Business,
                "sales",
                new[] { "Nouvelle vente et recherche", "Suspension et reprise", "Paiement et résultat de l’opération" }),
            new(
                "catalog",
                "Catalogue",
                "/module/catalog",
                "Références commerciales, catégories, unités et codes-barres.",
                AppNavigationGroup.Management,
                UiIconName.Catalog,
                0,
                AppNavigationAccessState.Available,
                false,
                AppNavigationKind.Business,
                "catalog",
                new[] { "Recherche de références", "Fiche produit", "Prix et codes-barres" }),
            new(
                "inventory",
                "Stock",
                "/module/inventory",
                "Quantités, mouvements, lots, péremptions et inventaires.",
                AppNavigationGroup.Management,
                UiIconName.Inventory,
                1,
                AppNavigationAccessState.Soon,
                false,
                AppNavigationKind.Business,
                "inventory",
                new[] { "Vue des quantités", "Lots et péremptions", "Inventaires et mouvements" }),
            new(
                "purchasing",
                "Achats et réceptions",
                "/module/purchasing",
                "Commandes, réceptions partielles, fournisseurs et retours.",
                AppNavigationGroup.Management,
                UiIconName.Purchasing,
                2,
                AppNavigationAccessState.Soon,
                false,
                AppNavigationKind.Business,
                "purchasing",
                new[] { "Commandes fournisseurs", "Réceptions", "Retours et documents associés" }),
            new(
                "cash",
                "Caisse",
                "/module/cash",
                "Ouverture, mouvements, suivi théorique et clôture.",
                AppNavigationGroup.Management,
                UiIconName.Cash,
                3,
                AppNavigationAccessState.Soon,
                false,
                AppNavigationKind.Business,
                "cash",
                new[] { "Ouverture de session", "Mouvements de caisse", "Clôture et écart expliqué" }),
            new(
                "customers",
                "Clients et crédit",
                "/module/customers",
                "Clients minimaux et crédit simple, sans donnée clinique.",
                AppNavigationGroup.Management,
                UiIconName.Customers,
                4,
                AppNavigationAccessState.Soon,
                false,
                AppNavigationKind.Business,
                "customers",
                new[] { "Recherche de clients", "Écritures de crédit", "Soldes et règlements" }),
            new(
                "reporting",
                "Rapports",
                "/module/reporting",
                "Rapports descriptifs et alertes en lecture seule.",
                AppNavigationGroup.Steering,
                UiIconName.Reporting,
                0,
                AppNavigationAccessState.Soon,
                false,
                AppNavigationKind.Business,
                "reporting",
                new[] { "Rapports de ventes", "Rapports de stock et lots", "Alertes descriptives" }),
            new(
                "administration",
                "Administration",
                "/module/administration",
                "Configuration, diagnostic, imports, sauvegardes et audit.",
                AppNavigationGroup.Configuration,
                UiIconName.Administration,
                0,
                AppNavigationAccessState.Soon,
                false,
                AppNavigationKind.Business,
                "administration",
                new[] { "Configuration locale", "Périphériques et continuité", "Audit et support contrôlé" })
        };

#if DEBUG
        items.Add(
            new AppNavigationItem(
                "diagnostic",
                "Diagnostic technique",
                "/diagnostic",
                "Contrôle technique conservé depuis le Sprint 1.",
                AppNavigationGroup.Development,
                UiIconName.Diagnostic,
                0,
                AppNavigationAccessState.Available,
                true,
                AppNavigationKind.Technical));
        items.Add(
            new AppNavigationItem(
                "ui-lab",
                "UI Lab",
                "/ui-lab",
                "Référence exécutable des fondations visuelles V0.1.",
                AppNavigationGroup.Development,
                UiIconName.UiLab,
                1,
                AppNavigationAccessState.Available,
                true,
                AppNavigationKind.Technical));
#endif

        return items
            .OrderBy(item => GetGroupOrder(item.Group))
            .ThenBy(item => item.Order)
            .ToArray();
    }

    private static string NormalizeRoute(string route)
    {
        var path = route.Split('?', '#')[0];
        var normalized = $"/{path.Trim('/')}";
        return normalized.Length == 1 ? "/" : normalized;
    }
}
