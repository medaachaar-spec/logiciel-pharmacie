using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Branding.Errors;

public static class PharmacyBrandingErrors
{
    public static Error InvalidOperationId { get; } = new(
        "branding.operation.invalid",
        "L’opération de mise à jour est invalide.");

    public static Error InvalidName { get; } = new(
        "branding.name.invalid",
        $"Le nom de la pharmacie est requis et ne doit pas dépasser {PharmacyBrandingDefaults.MaxPharmacyNameLength} caractères.");

    public static Error InvalidPrimaryColor { get; } = new(
        "branding.color.invalid",
        "La couleur principale doit utiliser le format hexadécimal #RRGGBB.");

    public static Error InvalidLogo { get; } = new(
        "branding.logo.invalid",
        "Le logo doit être un fichier PNG, JPEG ou WEBP valide et non vide.");

    public static Error LogoTooLarge { get; } = new(
        "branding.logo.too_large",
        "Le logo ne doit pas dépasser 2 Mo.");

    public static Error ConcurrencyConflict { get; } = new(
        "branding.concurrency.conflict",
        "L’identité de la pharmacie a été modifiée ailleurs. Rechargez-la avant de recommencer.");

    public static Error DatabaseUnavailable { get; } = new(
        "branding.database.unavailable",
        "L’identité enregistrée est temporairement indisponible. Les valeurs par défaut sont affichées.");
}
