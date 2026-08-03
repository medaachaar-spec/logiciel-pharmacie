using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Sales.Catalog.Queries;

internal sealed class SellableProductResolver
{
    private readonly IProductCatalogReader catalogReader;

    public SellableProductResolver(IProductCatalogReader catalogReader)
    {
        this.catalogReader = catalogReader ?? throw new ArgumentNullException(nameof(catalogReader));
    }

    public async Task<Result<SellableProductSnapshot>> ResolveAsync(
        SellableProductLookup lookup,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        var productResult = lookup.Kind switch
        {
            SellableProductLookupKind.ProductId => await catalogReader.FindByIdAsync(
                lookup.ProductId!,
                cancellationToken),
            SellableProductLookupKind.ExactCode => await catalogReader.FindByCodeAsync(
                lookup.ExactCode!,
                cancellationToken),
            _ => throw new InvalidOperationException("Le type de recherche produit est inconnu.")
        };

        if (productResult.IsFailure)
        {
            return Result<SellableProductSnapshot>.Failure(productResult.Error);
        }

        return productResult.Value.IsSellable
            ? productResult
            : Result<SellableProductSnapshot>.Failure(SalesApplicationErrors.ProductNotSellable);
    }
}
