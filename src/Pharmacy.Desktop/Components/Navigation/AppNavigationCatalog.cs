using Pharmacy.Desktop.Components.Ui;
using Pharmacy.Application.Security;

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
            AppNavigationGroup.Steering => "Suivi",
            AppNavigationGroup.Configuration => "Configuration",
            AppNavigationGroup.Development => "Outils internes",
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
                "Accès aux principales opérations.",
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
                "Créer, suspendre et finaliser une vente.",
                AppNavigationGroup.Activity,
                UiIconName.Sales,
                1,
                AppNavigationAccessState.Available,
                false,
                AppNavigationKind.Business,
                "sales",
                new[] { "Nouvelle vente et recherche", "Suspension et reprise", "Paiement et résultat de l’opération" },
                SecurityPermissions.SalesCreate),
            new(
                "catalog",
                "Produits",
                "/module/catalog",
                "Rechercher et gérer les produits.",
                AppNavigationGroup.Management,
                UiIconName.Catalog,
                0,
                AppNavigationAccessState.Available,
                false,
                AppNavigationKind.Business,
                "catalog",
                new[] { "Recherche de références", "Fiche produit", "Prix et codes-barres" },
                SecurityPermissions.CatalogView),
            new(
                "inventory",
                "Stock",
                "/module/inventory",
                "Consulter les quantités, lots et mouvements.",
                AppNavigationGroup.Management,
                UiIconName.Inventory,
                1,
                AppNavigationAccessState.Available,
                false,
                AppNavigationKind.Business,
                "inventory",
                new[] { "Vue des quantités", "Lots et péremptions", "Inventaires et mouvements" },
                SecurityPermissions.InventoryView),
            new(
                "purchasing",
                "Achats",
                "/module/purchasing",
                "Gérer les fournisseurs, commandes et réceptions.",
                AppNavigationGroup.Management,
                UiIconName.Purchasing,
                2,
                AppNavigationAccessState.Available,
                false,
                AppNavigationKind.Business,
                "purchasing",
                new[] { "Fournisseurs", "Commandes d’achat", "Réceptions partielles et directes" },
                SecurityPermissions.PurchasingView),
            new(
                "cash",
                "Caisse",
                "/module/cash",
                "Consulter les opérations de caisse.",
                AppNavigationGroup.Management,
                UiIconName.Cash,
                3,
                AppNavigationAccessState.Soon,
                false,
                AppNavigationKind.Business,
                "cash",
                new[] { "Ventes espèces", "Règlements de crédit", "Remboursements" },
                SecurityPermissions.CashView),
            new(
                "customers",
                "Clients",
                "/module/customers",
                "Gérer les clients, crédits et règlements.",
                AppNavigationGroup.Management,
                UiIconName.Customers,
                4,
                AppNavigationAccessState.Available,
                false,
                AppNavigationKind.Business,
                "customers",
                new[] { "Recherche de clients", "Écritures de crédit", "Soldes et règlements" },
                SecurityPermissions.CustomersView),
            new(
                "reporting",
                "Rapports",
                "/module/reporting",
                "Consulter les ventes, stocks et alertes.",
                AppNavigationGroup.Steering,
                UiIconName.Reporting,
                0,
                AppNavigationAccessState.Available,
                false,
                AppNavigationKind.Business,
                "reporting",
                new[] { "Tableau de bord descriptif", "Rapports SQL détaillés", "Alertes configurables et exports CSV" },
                SecurityPermissions.ReportsView),
            new(
                "administration",
                "Administration",
                "/module/administration",
                "Gérer les comptes, rôles et accès.",
                AppNavigationGroup.Configuration,
                UiIconName.Administration,
                0,
                AppNavigationAccessState.Available,
                false,
                AppNavigationKind.Business,
                "administration",
                new[] { "Comptes locaux", "Rôles et permissions", "Réauthentification sensible" },
                SecurityPermissions.SecurityManageUsers),
            new(
                "operations",
                "Sauvegardes et outils",
                "/module/operations",
                "Gérer les sauvegardes, imports et périphériques.",
                AppNavigationGroup.Configuration,
                UiIconName.Diagnostic,
                1,
                AppNavigationAccessState.Available,
                false,
                AppNavigationKind.Business,
                "operations",
                new[] { "Sauvegarde et test de restauration", "Imports CSV contrôlés", "Scanner, imprimante, TPE et onduleur" },
                SecurityPermissions.BackupView),
            new(
                "audit",
                "Journal d’audit",
                "/module/audit",
                "Consulter les événements d’activité et de sécurité.",
                AppNavigationGroup.Configuration,
                UiIconName.Administration,
                2,
                AppNavigationAccessState.Available,
                false,
                AppNavigationKind.Business,
                "audit",
                new[] { "Filtres temporels", "Acteur et action", "Résultat immuable" },
                SecurityPermissions.AuditView)
        };

#if DEBUG
        items.Add(
            new AppNavigationItem(
                "diagnostic",
                "Diagnostic technique",
                "/diagnostic",
                "Vérifier l’environnement et les périphériques.",
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
                "Référence interne des composants visuels.",
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
