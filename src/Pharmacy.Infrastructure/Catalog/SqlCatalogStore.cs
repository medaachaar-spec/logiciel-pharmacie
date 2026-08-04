using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Catalog.Contracts;
using Pharmacy.Application.Catalog.Errors;
using Pharmacy.Application.Catalog.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Catalog;

namespace Pharmacy.Infrastructure.Catalog;

public sealed class SqlCatalogStore : ICatalogProductStore, ICatalogCategoryStore
{
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;

    public SqlCatalogStore(IDbContextFactory<PharmacyDbContext> contextFactory)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<Result<CatalogProductDetails>> CreateAsync(
        CreateCatalogProductCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var barcodes = (command.Barcodes ?? Array.Empty<CatalogBarcodeInput>())
            .Select(input => new NormalizedBarcodeInput(input, BarcodeNormalizer.Normalize(input.Value)))
            .ToArray();

        if (barcodes.Select(item => item.Normalized).Distinct(StringComparer.Ordinal).Count() != barcodes.Length
            || barcodes.Count(item => item.Input.IsPrimary && item.Input.IsActive) > 1)
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DuplicateBarcode);
        }

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            if (!await context.ProductCategories.AnyAsync(
                    category => category.Id == command.CategoryId,
                    cancellationToken))
            {
                return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.CategoryNotFound);
            }

            var internalCode = command.InternalCode.Trim();

            if (await context.ProductLocalSettings.AnyAsync(
                    settings => settings.InternalCode == internalCode,
                    cancellationToken))
            {
                return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DuplicateReference);
            }

            var normalizedValues = barcodes.Select(item => item.Normalized).ToArray();

            if (normalizedValues.Length > 0 && await context.ProductBarcodes.AnyAsync(
                    barcode => normalizedValues.Contains(barcode.NormalizedValue),
                    cancellationToken))
            {
                return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DuplicateBarcode);
            }

            var now = DateTime.UtcNow;
            var product = new ProductEntity
            {
                Name = command.Name.Trim(),
                PackageDescription = NormalizeOptional(command.Presentation),
                IsActive = command.IsActive,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            var settings = new ProductLocalSettingsEntity
            {
                Product = product,
                CategoryId = command.CategoryId,
                InternalCode = internalCode,
                IsActive = command.IsLocallyActive,
                IsSellable = command.IsSellable,
                IsPurchasable = command.IsPurchasable,
                TracksLots = command.TracksLots,
                TracksExpiry = command.TracksExpiry,
                DiscountAllowed = command.DiscountAllowed,
                QuantityScale = command.QuantityScale,
                StorageLocation = NormalizeOptional(command.StorageLocation),
                MinimumStock = command.MinimumStock,
                MaximumStock = command.MaximumStock,
                CurrentSalePrice = command.InitialSalePrice ?? 0m,
                CurrencyCode = "MAD"
            };
            product.LocalSettings = settings;

            foreach (var item in barcodes)
            {
                product.Barcodes.Add(new ProductBarcodeEntity
                {
                    Product = product,
                    Value = item.Input.Value.Trim(),
                    NormalizedValue = item.Normalized,
                    IsPrimary = item.Input.IsPrimary,
                    BarcodeType = NormalizeOptional(item.Input.BarcodeType),
                    IsActive = item.Input.IsActive,
                    CreatedAtUtc = now
                });
            }

            if (command.InitialSalePrice is not null)
            {
                product.PriceHistory.Add(new ProductPriceHistoryEntity
                {
                    Product = product,
                    OldPrice = null,
                    NewPrice = command.InitialSalePrice.Value,
                    CurrencyCode = "MAD",
                    ChangeOrigin = "InitialOnboarding",
                    Reason = "Création Catalogue Sprint 10",
                    EffectiveAtUtc = now
                });
            }

            context.Products.Add(product);
            await context.SaveChangesAsync(cancellationToken);
            return await ReadProductAsync(product.Id, cancellationToken);
        }
        catch (DbUpdateException exception) when (CatalogSqlFailure.IsUniqueViolation(exception))
        {
            return await ResolveProductUniqueViolationAsync(
                command.InternalCode,
                barcodes.Select(item => item.Normalized),
                cancellationToken);
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<CatalogProductDetails>> UpdateAsync(
        UpdateCatalogProductCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var product = await context.Products
                .Include(candidate => candidate.LocalSettings)
                .SingleOrDefaultAsync(candidate => candidate.Id == command.ProductId, cancellationToken);

            if (product?.LocalSettings is null)
            {
                return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.ProductNotFound);
            }

            if (!await context.ProductCategories.AnyAsync(
                    category => category.Id == command.CategoryId,
                    cancellationToken))
            {
                return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.CategoryNotFound);
            }

            var internalCode = command.InternalCode.Trim();

            if (await context.ProductLocalSettings.AnyAsync(
                    settings => settings.ProductId != command.ProductId
                        && settings.InternalCode == internalCode,
                    cancellationToken))
            {
                return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DuplicateReference);
            }

            SetOriginalRowVersion(context, product, command.ProductRowVersion);
            SetOriginalRowVersion(context, product.LocalSettings, command.LocalSettingsRowVersion);
            product.Name = command.Name.Trim();
            product.PackageDescription = NormalizeOptional(command.Presentation);
            product.UpdatedAtUtc = DateTime.UtcNow;
            product.LocalSettings.CategoryId = command.CategoryId;
            product.LocalSettings.InternalCode = internalCode;
            product.LocalSettings.IsSellable = command.IsSellable;
            product.LocalSettings.IsPurchasable = command.IsPurchasable;
            product.LocalSettings.TracksLots = command.TracksLots;
            product.LocalSettings.TracksExpiry = command.TracksExpiry;
            product.LocalSettings.DiscountAllowed = command.DiscountAllowed;
            product.LocalSettings.QuantityScale = command.QuantityScale;
            product.LocalSettings.StorageLocation = NormalizeOptional(command.StorageLocation);
            product.LocalSettings.MinimumStock = command.MinimumStock;
            product.LocalSettings.MaximumStock = command.MaximumStock;

            await context.SaveChangesAsync(cancellationToken);
            return await ReadProductAsync(product.Id, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.ProductConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (CatalogSqlFailure.IsUniqueViolation(exception))
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DuplicateReference);
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<CatalogProductDetails>> SetActivationAsync(
        SetCatalogProductActivationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var product = await context.Products
                .Include(candidate => candidate.LocalSettings)
                .SingleOrDefaultAsync(candidate => candidate.Id == command.ProductId, cancellationToken);

            if (product?.LocalSettings is null)
            {
                return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.ProductNotFound);
            }

            SetOriginalRowVersion(context, product, command.ProductRowVersion);
            SetOriginalRowVersion(context, product.LocalSettings, command.LocalSettingsRowVersion);
            product.IsActive = command.IsActive;
            product.LocalSettings.IsActive = command.IsLocallyActive;
            product.UpdatedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return await ReadProductAsync(product.Id, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.ProductConcurrencyConflict);
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<CatalogProductDetails>> AddBarcodeAsync(
        AddCatalogBarcodeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = BarcodeNormalizer.Normalize(command.Value);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var product = await context.Products
                .Include(candidate => candidate.Barcodes)
                .SingleOrDefaultAsync(candidate => candidate.Id == command.ProductId, cancellationToken);

            if (product is null)
            {
                return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.ProductNotFound);
            }

            if (await context.ProductBarcodes.AnyAsync(
                    barcode => barcode.NormalizedValue == normalized,
                    cancellationToken))
            {
                return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DuplicateBarcode);
            }

            if (command.IsPrimary && command.IsActive)
            {
                foreach (var currentPrimary in product.Barcodes.Where(barcode => barcode.IsPrimary && barcode.IsActive))
                {
                    currentPrimary.IsPrimary = false;
                }
            }

            product.Barcodes.Add(new ProductBarcodeEntity
            {
                Product = product,
                Value = command.Value.Trim(),
                NormalizedValue = normalized,
                IsPrimary = command.IsPrimary,
                BarcodeType = NormalizeOptional(command.BarcodeType),
                IsActive = command.IsActive,
                CreatedAtUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync(cancellationToken);
            return await ReadProductAsync(product.Id, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.ProductConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (CatalogSqlFailure.IsUniqueViolation(exception))
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DuplicateBarcode);
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<CatalogProductDetails>> UpdateBarcodeAsync(
        UpdateCatalogBarcodeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = BarcodeNormalizer.Normalize(command.Value);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var product = await context.Products
                .Include(candidate => candidate.Barcodes)
                .SingleOrDefaultAsync(candidate => candidate.Id == command.ProductId, cancellationToken);

            if (product is null)
            {
                return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.ProductNotFound);
            }

            var barcode = product.Barcodes.SingleOrDefault(candidate => candidate.Id == command.BarcodeId);

            if (barcode is null)
            {
                return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.BarcodeNotFound);
            }

            if (await context.ProductBarcodes.AnyAsync(
                    candidate => candidate.Id != command.BarcodeId
                        && candidate.NormalizedValue == normalized,
                    cancellationToken))
            {
                return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DuplicateBarcode);
            }

            SetOriginalRowVersion(context, barcode, command.RowVersion);

            if (command.IsPrimary && command.IsActive)
            {
                foreach (var currentPrimary in product.Barcodes.Where(candidate =>
                             candidate.Id != barcode.Id && candidate.IsPrimary && candidate.IsActive))
                {
                    currentPrimary.IsPrimary = false;
                }
            }

            barcode.Value = command.Value.Trim();
            barcode.NormalizedValue = normalized;
            barcode.IsPrimary = command.IsPrimary;
            barcode.IsActive = command.IsActive;
            barcode.BarcodeType = NormalizeOptional(command.BarcodeType);
            await context.SaveChangesAsync(cancellationToken);
            return await ReadProductAsync(product.Id, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.ProductConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (CatalogSqlFailure.IsUniqueViolation(exception))
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DuplicateBarcode);
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<CatalogProductDetails>> ChangePriceAsync(
        ChangeCatalogProductPriceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var settings = await context.ProductLocalSettings
                .SingleOrDefaultAsync(candidate => candidate.ProductId == command.ProductId, cancellationToken);

            if (settings is null)
            {
                return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.ProductNotFound);
            }

            SetOriginalRowVersion(context, settings, command.LocalSettingsRowVersion);
            var oldPrice = settings.CurrentSalePrice;
            settings.CurrentSalePrice = command.NewPrice;
            context.ProductPriceHistories.Add(new ProductPriceHistoryEntity
            {
                ProductId = command.ProductId,
                OldPrice = oldPrice,
                NewPrice = command.NewPrice,
                CurrencyCode = settings.CurrencyCode,
                ChangeOrigin = "Manual",
                Reason = NormalizeOptional(command.Reason),
                EffectiveAtUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync(cancellationToken);
            return await ReadProductAsync(command.ProductId, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.ProductConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (CatalogSqlFailure.IsConstraintViolation(exception))
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.InvalidPrice);
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<CatalogCategoryView>> CreateAsync(
        CreateCatalogCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var code = command.Code.Trim();

            if (await context.ProductCategories.AnyAsync(category => category.Code == code, cancellationToken))
            {
                return Result<CatalogCategoryView>.Failure(
                    CatalogApplicationErrors.ValidationFailed("Ce code de catégorie est déjà utilisé."));
            }

            var category = new ProductCategoryEntity
            {
                Code = code,
                Name = command.Name.Trim(),
                IsActive = command.IsActive,
                DefaultTracksLots = command.DefaultTracksLots,
                DefaultTracksExpiry = command.DefaultTracksExpiry,
                DefaultDiscountAllowed = command.DefaultDiscountAllowed,
                SortOrder = command.SortOrder
            };
            context.ProductCategories.Add(category);
            await context.SaveChangesAsync(cancellationToken);
            return Result<CatalogCategoryView>.Success(MapCategory(category));
        }
        catch (DbUpdateException exception) when (CatalogSqlFailure.IsUniqueViolation(exception))
        {
            return Result<CatalogCategoryView>.Failure(
                CatalogApplicationErrors.ValidationFailed("Ce code de catégorie est déjà utilisé."));
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<CatalogCategoryView>.Failure(CatalogApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<CatalogCategoryView>> UpdateAsync(
        UpdateCatalogCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var category = await context.ProductCategories.SingleOrDefaultAsync(
                candidate => candidate.Id == command.CategoryId,
                cancellationToken);

            if (category is null)
            {
                return Result<CatalogCategoryView>.Failure(CatalogApplicationErrors.CategoryNotFound);
            }

            var code = command.Code.Trim();

            if (await context.ProductCategories.AnyAsync(
                    candidate => candidate.Id != category.Id && candidate.Code == code,
                    cancellationToken))
            {
                return Result<CatalogCategoryView>.Failure(
                    CatalogApplicationErrors.ValidationFailed("Ce code de catégorie est déjà utilisé."));
            }

            SetOriginalRowVersion(context, category, command.RowVersion);
            category.Code = code;
            category.Name = command.Name.Trim();
            category.SortOrder = command.SortOrder;
            category.DefaultTracksLots = command.DefaultTracksLots;
            category.DefaultTracksExpiry = command.DefaultTracksExpiry;
            category.DefaultDiscountAllowed = command.DefaultDiscountAllowed;
            await context.SaveChangesAsync(cancellationToken);
            return Result<CatalogCategoryView>.Success(MapCategory(category));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<CatalogCategoryView>.Failure(CatalogApplicationErrors.CategoryConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (CatalogSqlFailure.IsUniqueViolation(exception))
        {
            return Result<CatalogCategoryView>.Failure(
                CatalogApplicationErrors.ValidationFailed("Ce code de catégorie est déjà utilisé."));
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<CatalogCategoryView>.Failure(CatalogApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<CatalogCategoryView>> SetActivationAsync(
        SetCatalogCategoryActivationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var category = await context.ProductCategories.SingleOrDefaultAsync(
                candidate => candidate.Id == command.CategoryId,
                cancellationToken);

            if (category is null)
            {
                return Result<CatalogCategoryView>.Failure(CatalogApplicationErrors.CategoryNotFound);
            }

            SetOriginalRowVersion(context, category, command.RowVersion);
            category.IsActive = command.IsActive;
            await context.SaveChangesAsync(cancellationToken);
            return Result<CatalogCategoryView>.Success(MapCategory(category));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<CatalogCategoryView>.Failure(CatalogApplicationErrors.CategoryConcurrencyConflict);
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<CatalogCategoryView>.Failure(CatalogApplicationErrors.DatabaseUnavailable);
        }
    }

    internal static CatalogCategoryView MapCategory(ProductCategoryEntity category) =>
        new(
            category.Id,
            category.Code,
            category.Name,
            category.IsActive,
            category.DefaultTracksLots,
            category.DefaultTracksExpiry,
            category.DefaultDiscountAllowed,
            category.SortOrder,
            Convert.ToBase64String(category.RowVersion));

    private async Task<Result<CatalogProductDetails>> ReadProductAsync(
        long productId,
        CancellationToken cancellationToken) =>
        await new SqlProductCatalogReader(contextFactory).GetByIdAsync(productId, cancellationToken);

    private async Task<Result<CatalogProductDetails>> ResolveProductUniqueViolationAsync(
        string internalCode,
        IEnumerable<string> normalizedBarcodes,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var values = normalizedBarcodes.ToArray();

            if (values.Length > 0 && await context.ProductBarcodes.AsNoTracking().AnyAsync(
                    barcode => values.Contains(barcode.NormalizedValue),
                    cancellationToken))
            {
                return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DuplicateBarcode);
            }

            return await context.ProductLocalSettings.AsNoTracking().AnyAsync(
                    settings => settings.InternalCode == internalCode.Trim(),
                    cancellationToken)
                ? Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DuplicateReference)
                : Result<CatalogProductDetails>.Failure(
                    CatalogApplicationErrors.ValidationFailed("Une contrainte d’unicité Catalogue a été refusée."));
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.DatabaseUnavailable);
        }
    }

    private static void SetOriginalRowVersion<TEntity>(
        PharmacyDbContext context,
        TEntity entity,
        string token)
        where TEntity : class
    {
        context.Entry(entity).Property("RowVersion").OriginalValue = Convert.FromBase64String(token);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record NormalizedBarcodeInput(CatalogBarcodeInput Input, string Normalized);
}
