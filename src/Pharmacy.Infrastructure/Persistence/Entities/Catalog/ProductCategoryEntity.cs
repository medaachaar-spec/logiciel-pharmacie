namespace Pharmacy.Infrastructure.Persistence.Entities.Catalog;

internal sealed class ProductCategoryEntity
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool DefaultTracksLots { get; set; }

    public bool DefaultTracksExpiry { get; set; }

    public bool DefaultDiscountAllowed { get; set; }

    public int SortOrder { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ICollection<ProductLocalSettingsEntity> ProductSettings { get; } =
        new List<ProductLocalSettingsEntity>();
}
