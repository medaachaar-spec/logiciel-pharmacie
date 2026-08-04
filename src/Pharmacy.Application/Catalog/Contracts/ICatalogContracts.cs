using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Catalog.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Catalog.Contracts;

public interface ICatalogProductReader
{
    Task<Result<IReadOnlyList<CatalogProductListItem>>> SearchAsync(
        CatalogProductSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<Result<CatalogProductDetails>> GetByIdAsync(
        long productId,
        CancellationToken cancellationToken = default);
}

public interface ICatalogProductStore
{
    Task<Result<CatalogProductDetails>> CreateAsync(
        CreateCatalogProductCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<CatalogProductDetails>> UpdateAsync(
        UpdateCatalogProductCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<CatalogProductDetails>> SetActivationAsync(
        SetCatalogProductActivationCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<CatalogProductDetails>> AddBarcodeAsync(
        AddCatalogBarcodeCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<CatalogProductDetails>> UpdateBarcodeAsync(
        UpdateCatalogBarcodeCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<CatalogProductDetails>> ChangePriceAsync(
        ChangeCatalogProductPriceCommand command,
        CancellationToken cancellationToken = default);
}

public interface ICatalogCategoryReader
{
    Task<Result<IReadOnlyList<CatalogCategoryView>>> ListAsync(
        CancellationToken cancellationToken = default);
}

public interface ICatalogCategoryStore
{
    Task<Result<CatalogCategoryView>> CreateAsync(
        CreateCatalogCategoryCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<CatalogCategoryView>> UpdateAsync(
        UpdateCatalogCategoryCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<CatalogCategoryView>> SetActivationAsync(
        SetCatalogCategoryActivationCommand command,
        CancellationToken cancellationToken = default);
}

public interface ICatalogDiagnosticsReader
{
    Task<CatalogDiagnosticsView> GetAsync(CancellationToken cancellationToken = default);
}
