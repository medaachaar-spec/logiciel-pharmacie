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
        "sales.draft.empty",
        "Un brouillon vide ne peut pas être préparé pour finalisation.");

    public static Error InvalidOperationId { get; } = new(
        "sales.finalization.operation_id.invalid",
        "L’identifiant d’opération de finalisation ne peut pas être vide.");

    public static Error OperationConflict { get; } = new(
        "sales.finalization.operation_conflict",
        "Cet identifiant d’opération est déjà associé à une préparation différente.");
}
