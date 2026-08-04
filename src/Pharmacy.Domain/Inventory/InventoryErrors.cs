using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Inventory;

public static class InventoryErrors
{
    public static Error InvalidLocationId { get; } = new(
        "inventory.location.not_found",
        "L’emplacement de stock demandé est introuvable.");

    public static Error LocationInactive { get; } = new(
        "inventory.location.inactive",
        "L’emplacement de stock est inactif.");

    public static Error ProductNotFound { get; } = new(
        "inventory.product.not_found",
        "Le produit demandé est introuvable.");

    public static Error BatchNotFound { get; } = new(
        "inventory.batch.not_found",
        "Le lot demandé est introuvable.");

    public static Error BatchExpired { get; } = new(
        "inventory.batch.expired",
        "Le lot est expiré et n’est pas disponible à la vente.");

    public static Error LotRequired { get; } = new(
        "inventory.lot.required",
        "Un lot est requis pour ce produit.");

    public static Error ExpiryRequired { get; } = new(
        "inventory.expiry.required",
        "Une date de péremption est requise pour ce produit.");

    public static Error InvalidQuantity { get; } = new(
        "inventory.quantity.invalid",
        "La quantité de stock est invalide.");

    public static Error InsufficientStock { get; } = new(
        "inventory.stock.insufficient",
        "Le stock disponible est insuffisant pour cette opération.");

    public static Error InvalidOperation { get; } = new(
        "inventory.operation.invalid",
        "L’opération de stock est invalide ou incomplète.");

    public static Error OperationConflict { get; } = new(
        "inventory.operation.conflict",
        "Cet identifiant d’opération est déjà associé à une opération différente.");

    public static Error ConcurrencyConflict { get; } = new(
        "inventory.concurrency_conflict",
        "Le stock a changé depuis son ouverture. Rechargez les données avant de recommencer.");

    public static Error SessionNotFound { get; } = new(
        "inventory.session.not_found",
        "La session d’inventaire demandée est introuvable.");

    public static Error SessionInvalidStatus { get; } = new(
        "inventory.session.invalid_status",
        "Le statut de la session ne permet pas cette opération.");

    public static Error SessionAlreadyCompleted { get; } = new(
        "inventory.session.already_completed",
        "La session d’inventaire est déjà finalisée.");

    public static Error DatabaseUnavailable { get; } = new(
        "inventory.database.unavailable",
        "Le Stock SQL est indisponible. Vérifiez la configuration et la connectivité SQL Server.");
}
