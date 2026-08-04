namespace Pharmacy.Infrastructure.Catalog;

internal static class BarcodeNormalizer
{
    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().ToUpperInvariant();
    }
}
