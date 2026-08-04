using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Catalog.Errors;

public static class CatalogApplicationErrors
{
    public static Error ProductNotFound { get; } = new(
        "catalog.product.not_found",
        "Le produit demandé est introuvable.");

    public static Error CategoryNotFound { get; } = new(
        "catalog.category.not_found",
        "La catégorie demandée est introuvable.");

    public static Error DuplicateReference { get; } = new(
        "catalog.reference.duplicate",
        "Cette référence interne est déjà utilisée.");

    public static Error DuplicateBarcode { get; } = new(
        "catalog.barcode.duplicate",
        "Ce code-barres est déjà associé à un produit.");

    public static Error BarcodeNotFound { get; } = new(
        "catalog.barcode.not_found",
        "Le code-barres demandé est introuvable.");

    public static Error InvalidPrice { get; } = new(
        "catalog.price.invalid",
        "Le prix doit être supérieur ou égal à zéro.");

    public static Error ProductConcurrencyConflict { get; } = new(
        "catalog.product.concurrency_conflict",
        "Le produit a été modifié depuis son ouverture. Rechargez les données avant de recommencer.");

    public static Error CategoryConcurrencyConflict { get; } = new(
        "catalog.category.concurrency_conflict",
        "La catégorie a été modifiée depuis son ouverture. Rechargez les données avant de recommencer.");

    public static Error ValidationFailed(string detail)
    {
        var normalized = string.IsNullOrWhiteSpace(detail)
            ? "Les informations du Catalogue sont invalides."
            : detail.Trim();
        return new Error("catalog.validation.failed", normalized);
    }

    public static Error DatabaseUnavailable { get; } = new(
        "catalog.database.unavailable",
        "Le Catalogue SQL est indisponible. Vérifiez la configuration et la connectivité SQL Server.");
}
