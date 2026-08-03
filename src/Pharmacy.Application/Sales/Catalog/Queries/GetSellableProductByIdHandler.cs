using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Catalog.Queries;

public sealed record GetSellableProductByIdQuery(ProductId ProductId);

public sealed class GetSellableProductByIdHandler
{
    private readonly SellableProductResolver productResolver;
    private readonly IStockAvailabilityReader availabilityReader;

    public GetSellableProductByIdHandler(
        IProductCatalogReader catalogReader,
        IStockAvailabilityReader availabilityReader)
    {
        productResolver = new SellableProductResolver(
            catalogReader ?? throw new ArgumentNullException(nameof(catalogReader)));
        this.availabilityReader = availabilityReader ?? throw new ArgumentNullException(nameof(availabilityReader));
    }

    public async Task<Result<SellableProductView>> HandleAsync(
        GetSellableProductByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.ProductId);

        var productResult = await productResolver.ResolveAsync(
            SellableProductLookup.ByProductId(query.ProductId),
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
