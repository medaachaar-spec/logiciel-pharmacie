using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Purchasing;

public static class PurchasingErrors
{
    public static Error SupplierNotFound { get; } = new("purchasing.supplier.not_found", "Le fournisseur est introuvable.");
    public static Error SupplierCodeDuplicate { get; } = new("purchasing.supplier.code_duplicate", "Le code fournisseur existe déjà.");
    public static Error SupplierInactive { get; } = new("purchasing.supplier.inactive", "Le fournisseur est inactif.");
    public static Error SupplierConcurrencyConflict { get; } = new("purchasing.supplier.concurrency_conflict", "Le fournisseur a été modifié par un autre utilisateur.");
    public static Error OrderNotFound { get; } = new("purchasing.order.not_found", "La commande d’achat est introuvable.");
    public static Error OrderInvalidStatus { get; } = new("purchasing.order.invalid_status", "L’état de la commande ne permet pas cette opération.");
    public static Error OrderEmpty { get; } = new("purchasing.order.empty", "Une commande vide ne peut pas être confirmée.");
    public static Error OrderLineNotFound { get; } = new("purchasing.order.line_not_found", "La ligne de commande est introuvable.");
    public static Error OrderQuantityInvalid { get; } = new("purchasing.order.quantity_invalid", "La quantité commandée doit être un entier strictement positif.");
    public static Error OrderConcurrencyConflict { get; } = new("purchasing.order.concurrency_conflict", "La commande a été modifiée par un autre utilisateur.");
    public static Error ReceiptNotFound { get; } = new("purchasing.receipt.not_found", "La réception est introuvable.");
    public static Error DuplicateDeliveryNote { get; } = new("purchasing.receipt.duplicate_delivery_note", "Ce bon de livraison existe déjà pour ce fournisseur.");
    public static Error QuantityExceedsOrder { get; } = new("purchasing.receipt.quantity_exceeds_order", "La quantité reçue dépasse le reliquat commandé.");
    public static Error InvalidLot { get; } = new("purchasing.receipt.invalid_lot", "Le lot est absent ou incompatible avec le produit.");
    public static Error InvalidExpiry { get; } = new("purchasing.receipt.invalid_expiry", "La date de péremption est absente ou antérieure à la date de réception.");
    public static Error ProductNotFound { get; } = new("purchasing.receipt.product_not_found", "Le produit Catalogue est introuvable ou inactif.");
    public static Error ReceiptSupplierNotFound { get; } = new("purchasing.receipt.supplier_not_found", "Le fournisseur de la réception est introuvable.");
    public static Error ReceiptConcurrencyConflict { get; } = new("purchasing.receipt.concurrency_conflict", "Les données de réception ont changé. Rechargez puis recommencez.");
    public static Error ReceiptDatabaseUnavailable { get; } = new("purchasing.receipt.database_unavailable", "La base Purchasing SQL est indisponible.");
    public static Error ReceiptOperationConflict { get; } = new("purchasing.receipt.operation_conflict", "Cet identifiant d’opération désigne déjà une autre réception.");
    public static Error ValidationFailed { get; } = new("purchasing.validation.failed", "Les données Purchasing sont invalides.");
}
