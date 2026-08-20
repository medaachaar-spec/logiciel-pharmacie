namespace Pharmacy.Application.Branding.Models;

public sealed record PharmacyBrandingView(
    string PharmacyName,
    string PrimaryColor,
    byte[]? LogoContent,
    string? LogoMimeType,
    string? LogoFileName,
    DateTime UpdatedAtUtc,
    byte[] RowVersion);

public sealed record UpdatePharmacyBrandingRequest(
    Guid OperationId,
    string PharmacyName,
    string PrimaryColor,
    byte[]? LogoContent,
    string? LogoMimeType,
    string? LogoFileName,
    byte[] RowVersion);
