using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Catalog.Contracts;
using Pharmacy.Application.Catalog.Errors;
using Pharmacy.Application.Catalog.Models;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Catalog;

namespace Pharmacy.Infrastructure.Catalog;

public sealed class SqlProductCatalogReader : ICatalogProductReader, IProductCatalogReader
{
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;

    public SqlProductCatalogReader(IDbContextFactory<PharmacyDbContext> contextFactory)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<Result<IReadOnlyList<CatalogProductListItem>>> SearchAsync(
        CatalogProductSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.ProductLocalSettings
                .AsNoTracking()
                .Where(settings => settings.InternalCode != null);
            var text = criteria.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(text))
            {
                query = query.Where(settings =>
                    settings.Product.Name.Contains(text)
                    || (settings.Product.PackageDescription != null
                        && settings.Product.PackageDescription.Contains(text))
                    || settings.InternalCode!.Contains(text)
                    || settings.Product.Barcodes.Any(barcode =>
                        barcode.IsActive && barcode.NormalizedValue.Contains(text)));
            }

            if (criteria.CategoryId is not null)
            {
                query = query.Where(settings => settings.CategoryId == criteria.CategoryId.Value);
            }

            query = criteria.Status switch
            {
                CatalogProductStatusFilter.Active => query.Where(settings =>
                    settings.Product.IsActive && settings.IsActive),
                CatalogProductStatusFilter.Inactive => query.Where(settings =>
                    !settings.Product.IsActive || !settings.IsActive),
                _ => query
            };

            var rows = await query
                .OrderBy(settings => settings.Product.Name)
                .ThenBy(settings => settings.ProductId)
                .Take(criteria.Limit)
                .Select(settings => new CatalogProductListItem(
                    settings.ProductId,
                    settings.InternalCode!,
                    settings.Product.Name,
                    settings.Product.PackageDescription,
                    settings.CategoryId,
                    settings.Category.Name,
                    settings.CurrentSalePrice,
                    settings.CurrencyCode,
                    settings.Product.IsActive,
                    settings.IsActive,
                    settings.IsSellable,
                    settings.Product.Barcodes
                        .Where(barcode => barcode.IsActive)
                        .OrderByDescending(barcode => barcode.IsPrimary)
                        .ThenBy(barcode => barcode.Id)
                        .Select(barcode => barcode.Value)
                        .FirstOrDefault()))
                .ToArrayAsync(cancellationToken);

            return Result<IReadOnlyList<CatalogProductListItem>>.Success(Array.AsReadOnly(rows));
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<IReadOnlyList<CatalogProductListItem>>.Failure(
                CatalogApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<CatalogProductDetails>> GetByIdAsync(
        long productId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var product = await context.Products
                .AsNoTracking()
                .AsSplitQuery()
                .Include(candidate => candidate.LocalSettings!)
                    .ThenInclude(settings => settings.Category)
                .Include(candidate => candidate.Barcodes)
                .Include(candidate => candidate.PriceHistory)
                .SingleOrDefaultAsync(candidate => candidate.Id == productId, cancellationToken);

            return product?.LocalSettings is null
                ? Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.ProductNotFound)
                : Result<CatalogProductDetails>.Success(MapDetails(product));
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<SellableProductSnapshot>> FindByIdAsync(
        ProductId productId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productId);
        return await FindSellableAsync(productId.Value, null, cancellationToken);
    }

    public async Task<Result<SellableProductSnapshot>> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var trimmed = code?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return Result<SellableProductSnapshot>.Failure(SalesApplicationErrors.InvalidProductCode);
        }

        return await FindSellableAsync(null, trimmed, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SellableProductSnapshot>>> SearchAsync(
        ProductSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var text = query.Text?.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<IReadOnlyList<SellableProductSnapshot>>.Success(
                Array.Empty<SellableProductSnapshot>());
        }

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var normalizedCode = BarcodeNormalizer.Normalize(text);
            var rows = await SellableQuery(context)
                .Where(settings =>
                    settings.Product.Name.Contains(text)
                    || (settings.Product.PackageDescription != null
                        && settings.Product.PackageDescription.Contains(text))
                    || settings.InternalCode!.Contains(text)
                    || settings.Product.Barcodes.Any(barcode =>
                        barcode.IsActive && barcode.NormalizedValue.Contains(normalizedCode)))
                .OrderBy(settings => settings.ProductId)
                .Take(25)
                .Select(settings => new SellableRow(
                    settings.ProductId,
                    settings.InternalCode!,
                    settings.Product.Name,
                    settings.Product.PackageDescription,
                    settings.CurrentSalePrice))
                .ToArrayAsync(cancellationToken);

            var products = rows.Select(MapSellable).ToArray();
            return Result<IReadOnlyList<SellableProductSnapshot>>.Success(Array.AsReadOnly(products));
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<IReadOnlyList<SellableProductSnapshot>>.Failure(
                CatalogApplicationErrors.DatabaseUnavailable);
        }
    }

    internal static CatalogProductDetails MapDetails(ProductEntity product)
    {
        var settings = product.LocalSettings
            ?? throw new InvalidOperationException("Les paramètres locaux du produit sont absents.");
        return new CatalogProductDetails(
            product.Id,
            product.Name,
            product.PackageDescription,
            settings.CategoryId,
            settings.Category.Name,
            settings.InternalCode ?? string.Empty,
            product.IsActive,
            settings.IsActive,
            settings.IsSellable,
            settings.IsPurchasable,
            settings.TracksLots,
            settings.TracksExpiry,
            settings.DiscountAllowed,
            settings.QuantityScale,
            settings.StorageLocation,
            settings.MinimumStock,
            settings.MaximumStock,
            settings.CurrentSalePrice,
            settings.CurrencyCode,
            product.CreatedAtUtc,
            product.UpdatedAtUtc,
            Convert.ToBase64String(product.RowVersion),
            Convert.ToBase64String(settings.RowVersion),
            product.Barcodes
                .OrderByDescending(barcode => barcode.IsPrimary)
                .ThenBy(barcode => barcode.Id)
                .Select(barcode => new CatalogBarcodeView(
                    barcode.Id,
                    barcode.Value,
                    barcode.NormalizedValue,
                    barcode.IsPrimary,
                    barcode.BarcodeType,
                    barcode.IsActive,
                    barcode.CreatedAtUtc,
                    Convert.ToBase64String(barcode.RowVersion))),
            product.PriceHistory
                .OrderByDescending(history => history.EffectiveAtUtc)
                .ThenByDescending(history => history.Id)
                .Select(history => new CatalogPriceHistoryView(
                    history.Id,
                    history.OldPrice,
                    history.NewPrice,
                    history.CurrencyCode,
                    history.ChangeOrigin,
                    history.Reason,
                    history.EffectiveAtUtc)));
    }

    private async Task<Result<SellableProductSnapshot>> FindSellableAsync(
        long? productId,
        string? exactCode,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var query = SellableQuery(context);

            if (productId is not null)
            {
                query = query.Where(settings => settings.ProductId == productId.Value);
            }
            else
            {
                var normalizedCode = BarcodeNormalizer.Normalize(exactCode!);
                query = query.Where(settings =>
                    settings.InternalCode == exactCode
                    || settings.Product.Barcodes.Any(barcode =>
                        barcode.IsActive && barcode.NormalizedValue == normalizedCode));
            }

            var row = await query
                .Select(settings => new SellableRow(
                    settings.ProductId,
                    settings.InternalCode!,
                    settings.Product.Name,
                    settings.Product.PackageDescription,
                    settings.CurrentSalePrice))
                .SingleOrDefaultAsync(cancellationToken);

            return row is null
                ? Result<SellableProductSnapshot>.Failure(SalesApplicationErrors.ProductNotFound)
                : Result<SellableProductSnapshot>.Success(MapSellable(row));
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<SellableProductSnapshot>.Failure(CatalogApplicationErrors.DatabaseUnavailable);
        }
    }

    private static IQueryable<ProductLocalSettingsEntity> SellableQuery(PharmacyDbContext context) =>
        context.ProductLocalSettings
            .AsNoTracking()
            .Where(settings => settings.Product.IsActive
                && settings.IsActive
                && settings.IsSellable
                && settings.InternalCode != null
                && settings.CurrentSalePrice >= 0m);

    private static SellableProductSnapshot MapSellable(SellableRow row) =>
        new(
            ProductId.Create(row.ProductId).Value,
            ProductReference.Create(row.InternalCode).Value,
            row.Name,
            row.Presentation,
            Money.Create(row.CurrentSalePrice).Value,
            true);

    private sealed record SellableRow(
        long ProductId,
        string InternalCode,
        string Name,
        string? Presentation,
        decimal CurrentSalePrice);
}
