namespace Pharmacy.Infrastructure.Persistence.Entities.Catalog;

internal sealed class ProductBarcodeEntity
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public string Value { get; set; } = string.Empty;

    public string NormalizedValue { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public string? BarcodeType { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ProductEntity Product { get; set; } = null!;
}
