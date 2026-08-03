using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Catalog.Queries;

public sealed class SearchSellableProductsHandler
{
    public const int MaximumResults = 25;

    private readonly IProductCatalogReader catalogReader;
    private readonly IStockAvailabilityReader availabilityReader;

    public SearchSellableProductsHandler(
        IProductCatalogReader catalogReader,
        IStockAvailabilityReader availabilityReader)
    {
        this.catalogReader = catalogReader ?? throw new ArgumentNullException(nameof(catalogReader));
        this.availabilityReader = availabilityReader ?? throw new ArgumentNullException(nameof(availabilityReader));
    }

    public async Task<Result<IReadOnlyList<SellableProductView>>> HandleAsync(
        ProductSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalizedText = query.Text?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return Result<IReadOnlyList<SellableProductView>>.Success(
                Array.Empty<SellableProductView>());
        }

        var productsResult = await catalogReader.SearchAsync(
            new ProductSearchQuery(normalizedText),
            cancellationToken);

        if (productsResult.IsFailure)
        {
            return Result<IReadOnlyList<SellableProductView>>.Failure(productsResult.Error);
        }

        var views = new List<SellableProductView>();

        foreach (var product in productsResult.Value.Where(product => product.IsSellable).Take(MaximumResults))
        {
            var availabilityResult = await availabilityReader.GetAvailabilityAsync(
                product.ProductId,
                SaleQuantity.One,
                cancellationToken);

            if (availabilityResult.IsFailure)
            {
                return Result<IReadOnlyList<SellableProductView>>.Failure(availabilityResult.Error);
            }

            views.Add(SellableProductViewFactory.Create(product, availabilityResult.Value));
        }

        return Result<IReadOnlyList<SellableProductView>>.Success(
            Array.AsReadOnly(views.ToArray()));
    }
}
