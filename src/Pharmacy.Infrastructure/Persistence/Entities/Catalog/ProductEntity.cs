namespace Pharmacy.Infrastructure.Persistence.Entities.Catalog;

internal sealed class ProductEntity
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? GenericName { get; set; }

    public string? Dosage { get; set; }

    public string? Form { get; set; }

    public string? PackageDescription { get; set; }

    public string? BrandName { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ProductLocalSettingsEntity? LocalSettings { get; set; }

    public ICollection<ProductBarcodeEntity> Barcodes { get; } =
        new List<ProductBarcodeEntity>();

    public ICollection<ProductPriceHistoryEntity> PriceHistory { get; } =
        new List<ProductPriceHistoryEntity>();
}
