namespace Pharmacy.Application.Security;

public static class SecurityPermissionPresentation
{
    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [SecurityPermissions.SalesCreate] = "Créer et finaliser des ventes",
        [SecurityPermissions.SalesViewHistory] = "Consulter l’historique des ventes",
        [SecurityPermissions.SalesReturn] = "Enregistrer des retours",
        [SecurityPermissions.SalesRefund] = "Rembourser des ventes",
        [SecurityPermissions.InventoryView] = "Consulter le stock",
        [SecurityPermissions.InventoryAdjust] = "Ajuster le stock",
        [SecurityPermissions.InventoryFinalizeCount] = "Finaliser un inventaire",
        [SecurityPermissions.PurchasingView] = "Consulter achats et fournisseurs",
        [SecurityPermissions.PurchasingReceive] = "Réceptionner des achats",
        [SecurityPermissions.CatalogView] = "Consulter les produits",
        [SecurityPermissions.CatalogManage] = "Gérer les produits",
        [SecurityPermissions.CatalogChangePrice] = "Modifier les prix",
        [SecurityPermissions.CustomersView] = "Consulter les clients",
        [SecurityPermissions.CustomersManage] = "Gérer les clients",
        [SecurityPermissions.CustomersManageCredit] = "Gérer les crédits clients",
        [SecurityPermissions.CashView] = "Consulter la caisse",
        [SecurityPermissions.SecurityManageUsers] = "Gérer les employés et comptes",
        [SecurityPermissions.SecurityManageRoles] = "Gérer les rôles et permissions",
        [SecurityPermissions.AuditView] = "Consulter le journal d’activité",
        [SecurityPermissions.ReportsView] = "Consulter les rapports",
        [SecurityPermissions.ReportsExport] = "Exporter les rapports",
        [SecurityPermissions.ReportsManageSettings] = "Configurer les alertes et rapports",
        [SecurityPermissions.BackupView] = "Consulter les sauvegardes",
        [SecurityPermissions.BackupCreate] = "Créer une sauvegarde",
        [SecurityPermissions.BackupRestore] = "Restaurer une sauvegarde",
        [SecurityPermissions.ImportView] = "Consulter les imports",
        [SecurityPermissions.ImportExecute] = "Exécuter un import",
        [SecurityPermissions.DevicesConfigure] = "Configurer les périphériques"
    };

    public static string PermissionLabel(string code) => Labels.TryGetValue(code, out var label) ? label : code;

    public static string ModuleLabel(string module) => module switch
    {
        "sales" => "Ventes",
        "inventory" => "Stock",
        "purchasing" => "Achats et fournisseurs",
        "catalog" => "Produits",
        "customers" => "Clients",
        "cash" => "Caisse",
        "reports" => "Rapports",
        "security" => "Employés et administration",
        "audit" => "Journal d’activité",
        "backup" => "Sauvegardes",
        "import" => "Imports",
        "devices" => "Périphériques",
        _ => module
    };
}
