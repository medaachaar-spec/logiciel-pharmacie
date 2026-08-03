namespace Pharmacy.Infrastructure.Persistence.Entities.Catalog;

internal sealed class ProductPriceHistoryEntity
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public decimal? OldPrice { get; set; }

    public decimal NewPrice { get; set; }

    public string CurrencyCode { get; set; } = "MAD";

    public string ChangeOrigin { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public DateTime EffectiveAtUtc { get; set; }

    public ProductEntity Product { get; set; } = null!;
}
