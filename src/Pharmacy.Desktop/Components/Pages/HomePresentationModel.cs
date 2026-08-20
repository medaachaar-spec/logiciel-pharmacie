using Pharmacy.Desktop.Components.Ui;

namespace Pharmacy.Desktop.Components.Pages;

public sealed record DemoQuickAction(string Label, string Description, string Route, UiIconName Icon);

public static class HomePresentationModel
{
    public static IReadOnlyList<DemoQuickAction> QuickActions { get; } =
        new[]
        {
            new DemoQuickAction("Nouvelle vente", "Créer une vente", "/module/sales", UiIconName.Sales),
            new DemoQuickAction("Produits", "Rechercher ou modifier un produit", "/module/catalog", UiIconName.Catalog),
            new DemoQuickAction("Stock", "Consulter les quantités et les lots", "/module/inventory", UiIconName.Inventory),
            new DemoQuickAction("Achats", "Suivre les commandes et les réceptions", "/module/purchasing", UiIconName.Purchasing)
        };
}
