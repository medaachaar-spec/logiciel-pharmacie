using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Sales;

public static class SalesErrors
{
    public static Error InvalidMoney { get; } = new(
        "sales.money.invalid",
        "Le montant doit être supérieur ou égal à zéro.");

    public static Error InvalidProductId { get; } = new(
        "sales.product.invalid",
        "L’identifiant produit doit être strictement supérieur à zéro.");

    public static Error InvalidSaleDraftId { get; } = new(
        "sales.draft.invalid",
        "L’identifiant du brouillon ne peut pas être vide.");

    public static Error InvalidProductReference { get; } = new(
        "sales.product_reference.invalid",
        "La référence produit est obligatoire.");

    public static Error ProductReferenceTooLong { get; } = new(
        "sales.product_reference.too_long",
        $"La référence produit ne peut pas dépasser {ProductReference.MaxLength} caractères.");

    public static Error InvalidQuantity { get; } = new(
        "sales.quantity.invalid",
        "La quantité de vente doit être un entier strictement supérieur à zéro.");

    public static Error QuantityOverflow { get; } = new(
        "sales.quantity.overflow",
        "La quantité de vente dépasse la capacité prise en charge.");

    public static Error InvalidProductName { get; } = new(
        "sales.product_name.invalid",
        "Le nom d’affichage du produit est obligatoire.");

    public static Error LineNotFound(ProductId productId)
    {
        ArgumentNullException.ThrowIfNull(productId);
        return new Error(
            "sales.line.not_found",
            $"Aucune ligne ne correspond au produit {productId.Value}.");
    }
}
