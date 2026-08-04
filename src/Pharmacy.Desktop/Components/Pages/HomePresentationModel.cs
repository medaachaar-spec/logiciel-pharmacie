using Pharmacy.Desktop.Components.Ui;

namespace Pharmacy.Desktop.Components.Pages;

public sealed record DemoHomeMetric(string Label, string Value, string Detail, UiIconName Icon);

public sealed record DemoQuickAction(string Label, string Description, string Route, UiIconName Icon);

public sealed record DemoRecentActivity(string Reference, string Label, string Context);

public sealed record DemoSystemState(string Label, string Status, string Detail);

public static class HomePresentationModel
{
    public static IReadOnlyList<DemoHomeMetric> Metrics { get; } =
        new[]
        {
            new DemoHomeMetric("Activité du jour", "24 opérations", "Valeur de démonstration", UiIconName.Sales),
            new DemoHomeMetric("Alertes de stock", "7 alertes", "Valeur de démonstration", UiIconName.Inventory),
            new DemoHomeMetric("Lots à surveiller", "4 lots", "Valeur de démonstration", UiIconName.Catalog),
            new DemoHomeMetric("Opérations en attente", "3 actions", "Valeur de démonstration", UiIconName.Purchasing)
        };

    public static IReadOnlyList<DemoQuickAction> QuickActions { get; } =
        new[]
        {
            new DemoQuickAction("Nouvelle vente", "Ouvrir la page provisoire Ventes", "/module/sales", UiIconName.Sales),
            new DemoQuickAction("Consulter le stock", "Ouvrir la page provisoire Stock", "/module/inventory", UiIconName.Inventory),
            new DemoQuickAction("Enregistrer une réception", "Ouvrir la page provisoire Achats", "/module/purchasing", UiIconName.Purchasing),
            new DemoQuickAction("Ouvrir la caisse", "Ouvrir la page provisoire Caisse", "/module/cash", UiIconName.Cash)
        };

    public static IReadOnlyList<DemoRecentActivity> RecentActivity { get; } =
        new[]
        {
            new DemoRecentActivity("DEMO-VENTE-0001", "Vente de démonstration", "Aujourd’hui — exemple synthétique"),
            new DemoRecentActivity("DEMO-RECEPTION-0001", "Réception de démonstration", "Aujourd’hui — exemple synthétique"),
            new DemoRecentActivity("DEMO-INVENTAIRE-0001", "Inventaire de démonstration", "Hier — exemple synthétique")
        };

    public static IReadOnlyList<DemoSystemState> SystemStates { get; } =
        new[]
        {
            new DemoSystemState("Application", "Locale", "Exécution desktop sans service web"),
            new DemoSystemState("Catalogue SQL", "Voir diagnostic", "Seed manuel de dix références Demo"),
            new DemoSystemState("Périphériques", "Non configurés", "Scanner et imprimantes non testés"),
            new DemoSystemState("Sauvegarde", "Non configurée", "Planification volontairement différée")
        };
}
