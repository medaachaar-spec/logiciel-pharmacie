using Pharmacy.Desktop.Components.Ui;
using Pharmacy.Application.Security;

namespace Pharmacy.Desktop.Components.Pages;

public sealed record HomeQuickAction(string Label, string Description, string Route, UiIconName Icon, string RequiredPermission);

public static class HomePresentationModel
{
    public static IReadOnlyList<HomeQuickAction> QuickActions { get; } =
        new[]
        {
            new HomeQuickAction("Nouvelle vente", "Créer une vente", "/module/sales", UiIconName.Sales, SecurityPermissions.SalesCreate),
            new HomeQuickAction("Produits", "Rechercher ou modifier un produit", "/module/catalog", UiIconName.Catalog, SecurityPermissions.CatalogView),
            new HomeQuickAction("Stock", "Consulter les quantités et les lots", "/module/inventory", UiIconName.Inventory, SecurityPermissions.InventoryView),
            new HomeQuickAction("Achats", "Suivre les commandes et les réceptions", "/module/purchasing", UiIconName.Purchasing, SecurityPermissions.PurchasingView)
        };
}
