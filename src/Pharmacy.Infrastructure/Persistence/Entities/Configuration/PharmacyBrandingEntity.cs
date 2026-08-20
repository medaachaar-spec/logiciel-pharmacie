namespace Pharmacy.Infrastructure.Persistence.Entities.Configuration;

internal sealed class PharmacyBrandingEntity
{
    public long Id { get; set; }
    public string PharmacyName { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = string.Empty;
    public byte[]? LogoContent { get; set; }
    public string? LogoMimeType { get; set; }
    public string? LogoFileName { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
