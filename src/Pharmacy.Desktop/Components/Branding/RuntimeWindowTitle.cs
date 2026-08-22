using Pharmacy.Application.Branding;

namespace Pharmacy.Desktop.Components.Branding;

public sealed class RuntimeWindowTitle
{
    public event Action? Changed;

    public string PharmacyName { get; private set; } = PharmacyBrandingDefaults.PharmacyName;

    public void SetPharmacyName(string pharmacyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pharmacyName);
        var normalized = pharmacyName.Trim();
        if (string.Equals(PharmacyName, normalized, StringComparison.Ordinal)) return;
        PharmacyName = normalized;
        Changed?.Invoke();
    }
}
