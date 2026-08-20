using Pharmacy.Application.Branding.Models;

namespace Pharmacy.Application.Branding;

public static class PharmacyBrandingDefaults
{
    public const string PharmacyName = "Pharmacie Diar Douha";
    public const string PrimaryColor = "#0D9A48";
    public const int MaxPharmacyNameLength = 160;
    public const int MaxLogoBytes = 2 * 1024 * 1024;

    public static PharmacyBrandingView CreateView() => new(
        PharmacyName,
        PrimaryColor,
        null,
        null,
        null,
        DateTime.UnixEpoch,
        []);
}
