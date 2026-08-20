using System.Text.RegularExpressions;
using Pharmacy.Application.Branding.Errors;
using Pharmacy.Application.Branding.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Branding;

public static partial class PharmacyBrandingValidator
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly IReadOnlyDictionary<string, string[]> ExtensionsByMime =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/png"] = [".png"],
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/webp"] = [".webp"]
        };

    public static Result Validate(UpdatePharmacyBrandingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.OperationId == Guid.Empty)
            return Result.Failure(PharmacyBrandingErrors.InvalidOperationId);
        if (string.IsNullOrWhiteSpace(request.PharmacyName)
            || request.PharmacyName.Trim().Length > PharmacyBrandingDefaults.MaxPharmacyNameLength)
            return Result.Failure(PharmacyBrandingErrors.InvalidName);
        if (string.IsNullOrWhiteSpace(request.PrimaryColor)
            || !HexColorRegex().IsMatch(request.PrimaryColor.Trim()))
            return Result.Failure(PharmacyBrandingErrors.InvalidPrimaryColor);
        if (request.RowVersion is null || request.RowVersion.Length == 0)
            return Result.Failure(PharmacyBrandingErrors.ConcurrencyConflict);

        return ValidateLogo(request.LogoContent, request.LogoMimeType, request.LogoFileName);
    }

    public static Result ValidateLogo(byte[]? content, string? mimeType, string? fileName)
    {
        if (content is null) return Result.Success();
        if (content.Length == 0) return Result.Failure(PharmacyBrandingErrors.InvalidLogo);
        if (content.Length > PharmacyBrandingDefaults.MaxLogoBytes)
            return Result.Failure(PharmacyBrandingErrors.LogoTooLarge);
        var normalizedMimeType = mimeType?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedMimeType)
            || !ExtensionsByMime.TryGetValue(normalizedMimeType, out var allowedExtensions)
            || string.IsNullOrWhiteSpace(fileName)
            || fileName.Length > 255
            || !allowedExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase)
            || !HasExpectedSignature(content, normalizedMimeType))
            return Result.Failure(PharmacyBrandingErrors.InvalidLogo);

        return Result.Success();
    }

    private static bool HasExpectedSignature(ReadOnlySpan<byte> content, string mimeType) => mimeType switch
    {
        "image/png" => content.StartsWith(PngSignature),
        "image/jpeg" => content.Length >= 4
            && content[0] == 0xFF && content[1] == 0xD8
            && content[^2] == 0xFF && content[^1] == 0xD9,
        "image/webp" => content.Length >= 12
            && content[..4].SequenceEqual("RIFF"u8)
            && content.Slice(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorRegex();
}
