namespace Pharmacy.Application.Catalog.Models;

public enum CatalogProductStatusFilter
{
    All,
    Active,
    Inactive
}

public sealed record CatalogProductSearchCriteria(
    string? Text = null,
    CatalogProductStatusFilter Status = CatalogProductStatusFilter.All,
    long? CategoryId = null,
    int Limit = 50);

public sealed record CatalogProductListItem(
    long ProductId,
    string InternalCode,
    string Name,
    string? Presentation,
    long CategoryId,
    string CategoryName,
    decimal CurrentSalePrice,
    string CurrencyCode,
    bool IsActive,
    bool IsLocallyActive,
    bool IsSellable,
    string? PrimaryBarcode);

public sealed record CatalogCategoryView(
    long CategoryId,
    string Code,
    string Name,
    bool IsActive,
    bool DefaultTracksLots,
    bool DefaultTracksExpiry,
    bool DefaultDiscountAllowed,
    int SortOrder,
    string RowVersion);

public sealed record CatalogBarcodeView(
    long BarcodeId,
    string Value,
    string NormalizedValue,
    bool IsPrimary,
    string? BarcodeType,
    bool IsActive,
    DateTime CreatedAtUtc,
    string RowVersion);

public sealed record CatalogPriceHistoryView(
    long PriceHistoryId,
    decimal? OldPrice,
    decimal NewPrice,
    string CurrencyCode,
    string ChangeOrigin,
    string? Reason,
    DateTime EffectiveAtUtc);

public sealed class CatalogProductDetails
{
    public CatalogProductDetails(
        long productId,
        string name,
        string? presentation,
        long categoryId,
        string categoryName,
        string internalCode,
        bool isActive,
        bool isLocallyActive,
        bool isSellable,
        bool isPurchasable,
        bool tracksLots,
        bool tracksExpiry,
        bool discountAllowed,
        byte quantityScale,
        string? storageLocation,
        decimal? minimumStock,
        decimal? maximumStock,
        decimal currentSalePrice,
        string currencyCode,
        DateTime createdAtUtc,
        DateTime updatedAtUtc,
        string productRowVersion,
        string localSettingsRowVersion,
        IEnumerable<CatalogBarcodeView> barcodes,
        IEnumerable<CatalogPriceHistoryView> priceHistory)
    {
        ArgumentNullException.ThrowIfNull(barcodes);
        ArgumentNullException.ThrowIfNull(priceHistory);

        ProductId = productId;
        Name = name;
        Presentation = presentation;
        CategoryId = categoryId;
        CategoryName = categoryName;
        InternalCode = internalCode;
        IsActive = isActive;
        IsLocallyActive = isLocallyActive;
        IsSellable = isSellable;
        IsPurchasable = isPurchasable;
        TracksLots = tracksLots;
        TracksExpiry = tracksExpiry;
        DiscountAllowed = discountAllowed;
        QuantityScale = quantityScale;
        StorageLocation = storageLocation;
        MinimumStock = minimumStock;
        MaximumStock = maximumStock;
        CurrentSalePrice = currentSalePrice;
        CurrencyCode = currencyCode;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        ProductRowVersion = productRowVersion;
        LocalSettingsRowVersion = localSettingsRowVersion;
        Barcodes = Array.AsReadOnly(barcodes.ToArray());
        PriceHistory = Array.AsReadOnly(priceHistory.ToArray());
    }

    public long ProductId { get; }
    public string Name { get; }
    public string? Presentation { get; }
    public long CategoryId { get; }
    public string CategoryName { get; }
    public string InternalCode { get; }
    public bool IsActive { get; }
    public bool IsLocallyActive { get; }
    public bool IsSellable { get; }
    public bool IsPurchasable { get; }
    public bool TracksLots { get; }
    public bool TracksExpiry { get; }
    public bool DiscountAllowed { get; }
    public byte QuantityScale { get; }
    public string? StorageLocation { get; }
    public decimal? MinimumStock { get; }
    public decimal? MaximumStock { get; }
    public decimal CurrentSalePrice { get; }
    public string CurrencyCode { get; }
    public DateTime CreatedAtUtc { get; }
    public DateTime UpdatedAtUtc { get; }
    public string ProductRowVersion { get; }
    public string LocalSettingsRowVersion { get; }
    public IReadOnlyList<CatalogBarcodeView> Barcodes { get; }
    public IReadOnlyList<CatalogPriceHistoryView> PriceHistory { get; }
}

public sealed record CatalogDiagnosticsView(
    string Provider,
    string DatabaseName,
    bool IsConfigured,
    bool IsReachable,
    string MigrationStatus,
    int ActiveDemoProductCount,
    string? ErrorCode);
