namespace Pharmacy.Application.Catalog.Commands;

public sealed record CatalogBarcodeInput(
    string Value,
    bool IsPrimary = false,
    string? BarcodeType = "Demo",
    bool IsActive = true);

public sealed record CreateCatalogProductCommand(
    string Name,
    string? Presentation,
    long CategoryId,
    string InternalCode,
    decimal? InitialSalePrice,
    IReadOnlyList<CatalogBarcodeInput>? Barcodes = null,
    bool IsActive = true,
    bool IsLocallyActive = true,
    bool IsSellable = true,
    bool IsPurchasable = true,
    bool TracksLots = false,
    bool TracksExpiry = false,
    bool DiscountAllowed = false,
    byte QuantityScale = 0,
    string? StorageLocation = null,
    decimal? MinimumStock = null,
    decimal? MaximumStock = null);

public sealed record UpdateCatalogProductCommand(
    long ProductId,
    string Name,
    string? Presentation,
    long CategoryId,
    string InternalCode,
    bool IsSellable,
    bool IsPurchasable,
    bool TracksLots,
    bool TracksExpiry,
    bool DiscountAllowed,
    byte QuantityScale,
    string? StorageLocation,
    decimal? MinimumStock,
    decimal? MaximumStock,
    string ProductRowVersion,
    string LocalSettingsRowVersion);

public sealed record SetCatalogProductActivationCommand(
    long ProductId,
    bool IsActive,
    bool IsLocallyActive,
    string ProductRowVersion,
    string LocalSettingsRowVersion);

public sealed record AddCatalogBarcodeCommand(
    long ProductId,
    string Value,
    bool IsPrimary,
    string? BarcodeType = "Demo",
    bool IsActive = true);

public sealed record UpdateCatalogBarcodeCommand(
    long ProductId,
    long BarcodeId,
    string Value,
    bool IsPrimary,
    bool IsActive,
    string RowVersion,
    string? BarcodeType = "Demo");

public sealed record ChangeCatalogProductPriceCommand(
    long ProductId,
    decimal NewPrice,
    string? Reason,
    string LocalSettingsRowVersion);

public sealed record CreateCatalogCategoryCommand(
    string Code,
    string Name,
    int SortOrder = 0,
    bool IsActive = true,
    bool DefaultTracksLots = false,
    bool DefaultTracksExpiry = false,
    bool DefaultDiscountAllowed = false);

public sealed record UpdateCatalogCategoryCommand(
    long CategoryId,
    string Code,
    string Name,
    int SortOrder,
    bool DefaultTracksLots,
    bool DefaultTracksExpiry,
    bool DefaultDiscountAllowed,
    string RowVersion);

public sealed record SetCatalogCategoryActivationCommand(
    long CategoryId,
    bool IsActive,
    string RowVersion);
