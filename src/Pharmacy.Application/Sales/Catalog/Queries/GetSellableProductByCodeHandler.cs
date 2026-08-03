using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Catalog.Queries;

public sealed record GetSellableProductByCodeQuery(string ExactCode);

public sealed class GetSellableProductByCodeHandler
{
    private readonly SellableProductResolver productResolver;
    private readonly IStockAvailabilityReader availabilityReader;

    public GetSellableProductByCodeHandler(
        IProductCatalogReader catalogReader,
        IStockAvailabilityReader availabilityReader)
    {
        productResolver = new SellableProductResolver(
            catalogReader ?? throw new ArgumentNullException(nameof(catalogReader)));
        this.availabilityReader = availabilityReader ?? throw new ArgumentNullException(nameof(availabilityReader));
    }

    public async Task<Result<SellableProductView>> HandleAsync(
        GetSellableProductByCodeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var lookupResult = SellableProductLookup.ByExactCode(query.ExactCode);

        if (lookupResult.IsFailure)
        {
            return Result<SellableProductView>.Failure(SalesApplicationErrors.InvalidProductCode);
        }

        var productResult = await productResolver.ResolveAsync(
            lookupResult.Value,
            cancellationToken);

        if (productResult.IsFailure)
        {
            return Result<SellableProductView>.Failure(productResult.Error);
        }

        var availabilityResult = await availabilityReader.GetAvailabilityAsync(
            productResult.Value.ProductId,
            SaleQuantity.One,
            cancellationToken);

        return availabilityResult.IsFailure
            ? Result<SellableProductView>.Failure(availabilityResult.Error)
            : Result<SellableProductView>.Success(
                SellableProductViewFactory.Create(productResult.Value, availabilityResult.Value));
    }
}
