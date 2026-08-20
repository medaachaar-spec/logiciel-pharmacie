using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Errors;

public static class SalesApplicationErrors
{
    public static Error DraftNotFound(SaleDraftId draftId)
    {
        ArgumentNullException.ThrowIfNull(draftId);
        return new Error(
            "sales.draft.not_found",
            $"Le brouillon {draftId.Value} est introuvable.");
    }

    public static Error DraftAlreadyExists(SaleDraftId draftId)
    {
        ArgumentNullException.ThrowIfNull(draftId);
        return new Error(
            "sales.draft.already_exists",
            $"Le brouillon {draftId.Value} existe déjà.");
    }

    public static Error InvalidExpectedVersion { get; } = new(
        "sales.draft.version.invalid",
        "La version attendue doit être strictement supérieure à zéro.");

    public static Error ConcurrencyConflict(long expectedVersion, long actualVersion)
    {
        return new Error(
            "sales.draft.concurrency_conflict",
            $"Conflit de version : version attendue {expectedVersion}, version actuelle {actualVersion}.");
    }

    public static Error ProductNotFound { get; } = new(
        "sales.product.not_found",
        "Le produit demandé est introuvable.");

    public static Error ProductNotSellable { get; } = new(
        "sales.product.not_sellable",
        "Le produit demandé n’est pas vendable.");

    public static Error InvalidProductCode { get; } = new(
        "sales.product_code.invalid",
        "Le code produit exact est obligatoire.");

    public static Error InsufficientAvailability(
        ProductId productId,
        int requestedQuantity,
        int availableQuantity)
    {
        ArgumentNullException.ThrowIfNull(productId);
        return new Error(
            "sales.stock.insufficient_availability",
            $"Disponibilité insuffisante pour le produit {productId.Value} : demandée {requestedQuantity}, disponible {availableQuantity}.");
    }

    public static Error DraftEmpty { get; } = new(
        "sales.finalization.empty_draft",
        "Un brouillon vide ne peut pas être préparé pour finalisation.");

    public static Error InvalidOperationId { get; } = new(
        "sales.operation.invalid",
        "L’identifiant d’opération de finalisation ne peut pas être vide.");

    public static Error OperationConflict { get; } = new(
        "sales.operation.conflict",
        "Cet identifiant d’opération est déjà associé à une préparation différente.");

    public static Error StockInsufficient { get; } = new(
        "sales.stock.insufficient",
        "Le stock vendable est insuffisant pour finaliser cette vente.");

    public static Error StockConcurrencyConflict { get; } = new(
        "sales.stock.concurrency_conflict",
        "Le stock a changé pendant la finalisation. La vente n’a pas été enregistrée.");

    public static Error PriceChanged { get; } = new(
        "sales.price.changed",
        "Un prix a changé depuis l’ajout au panier. Actualisez le panier avant de réessayer.");

    public static Error PaymentInvalid { get; } = new(
        "sales.payment.invalid",
        "Le paiement ne couvre pas exactement les règles de cette vente.");

    public static Error CreditCustomerRequired { get; } = new(
        "sales.credit.customer_required",
        "Une vente à crédit exige un client.");

    public static Error CreditNotAllowed { get; } = new(
        "sales.credit.not_allowed",
        "Le crédit n’est pas autorisé pour ce client.");

    public static Error CardUnconfirmed { get; } = new(
        "sales.payment.card_unconfirmed",
        "Le paiement carte doit être confirmé sur le TPE externe avant la finalisation.");

    public static Error DatabaseUnavailable { get; } = new(
        "sales.database.unavailable",
        "Le service de données local est indisponible. Le panier a été conservé.");

    public static Error PersistenceFailed { get; } = new(
        "sales.persistence.failed",
        "La vente n’a pas pu être enregistrée. Aucune modification n’a été conservée.");

    public static Error SaleNotFound { get; } = new(
        "sales.sale.not_found",
        "La vente finalisée demandée est introuvable.");

    public static Error InvalidSaleQuery { get; } = new(
        "sales.sale.query.invalid",
        "La demande de consultation des ventes est invalide.");

    public static Error ReturnInvalid { get; } = new(
        "sales.return.invalid",
        "Le retour contient une ligne, une quantité ou une décision invalide.");

    public static Error ReturnQuantityExceeded { get; } = new(
        "sales.return.quantity_exceeded",
        "La quantité demandée dépasse le reliquat retournable de la vente.");

    public static Error ReturnConflict { get; } = new(
        "sales.return.concurrency_conflict",
        "La vente a été retournée par une autre opération. Rechargez son détail.");
}
