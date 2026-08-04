using Pharmacy.Domain.Common;

namespace Pharmacy.Desktop.Components.Pos;

public sealed record SalesPosAvailabilityIncident(
    string ProductName,
    int RequestedQuantity,
    int AvailableQuantity);

public sealed record SalesPosErrorContext(
    string? ProductName = null,
    int? RequestedQuantity = null,
    int? AvailableQuantity = null);

public sealed record SalesPosErrorPresentation(
    string Message,
    SalesPosAvailabilityIncident? AvailabilityIncident = null);

public static class SalesPosErrorMapper
{
    public static SalesPosErrorPresentation Map(
        Error error,
        SalesPosErrorContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        context ??= new SalesPosErrorContext();

        if (string.Equals(error.Code, "sales.stock.insufficient_availability", StringComparison.Ordinal))
        {
            var incident = new SalesPosAvailabilityIncident(
                context.ProductName ?? "Produit synthétique",
                context.RequestedQuantity ?? 1,
                context.AvailableQuantity ?? 0);
            return new SalesPosErrorPresentation(
                $"Disponibilité insuffisante pour {incident.ProductName}. Le panier existant est conservé.",
                incident);
        }

        var message = error.Code switch
        {
            "sales.draft.not_found" => "Le brouillon courant est introuvable. Démarrez une nouvelle vente.",
            "sales.draft.concurrency_conflict" =>
                "Le brouillon a été modifié. Son état le plus récent a été rechargé.",
            "sales.product.not_found" => "Code ou produit inconnu. Aucun produit n’a été créé.",
            "sales.product.not_sellable" => "Ce produit n’est pas vendable et n’a pas été ajouté.",
            "sales.product_code.invalid" => "Saisissez un code produit exact avant de valider.",
            "catalog.database.unavailable" =>
                "Le Catalogue SQL est indisponible. Aucun catalogue mémoire de secours n’a été utilisé.",
            "sales.quantity.invalid" => "La quantité doit être un entier strictement positif. Utilisez Supprimer pour retirer la ligne.",
            "sales.line.not_found" => "La ligne demandée n’existe plus dans le brouillon.",
            _ => "L’opération Sales n’a pas pu être réalisée. Le panier existant est conservé."
        };

        return new SalesPosErrorPresentation(message);
    }
}
