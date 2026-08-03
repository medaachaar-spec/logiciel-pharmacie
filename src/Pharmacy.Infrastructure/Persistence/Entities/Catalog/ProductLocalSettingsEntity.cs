namespace Pharmacy.Infrastructure.Persistence.Entities.Catalog;

internal sealed class ProductLocalSettingsEntity
{
    public long ProductId { get; set; }

    public string? InternalCode { get; set; }

    public long CategoryId { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSellable { get; set; } = true;

    public bool IsPurchasable { get; set; } = true;

    public bool TracksLots { get; set; }

    public bool TracksExpiry { get; set; }

    public bool DiscountAllowed { get; set; }

    public byte QuantityScale { get; set; }

    public string? StorageLocation { get; set; }

    public decimal? MinimumStock { get; set; }

    public decimal? MaximumStock { get; set; }

    public decimal CurrentSalePrice { get; set; }

    public string CurrencyCode { get; set; } = "MAD";

    public byte[] RowVersion { get; set; } = [];

    public ProductEntity Product { get; set; } = null!;

    public ProductCategoryEntity Category { get; set; } = null!;
}
