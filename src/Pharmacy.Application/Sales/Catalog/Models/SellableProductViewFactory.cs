using Pharmacy.Application.Sales.Models;

namespace Pharmacy.Application.Sales.Catalog.Models;

internal static class SellableProductViewFactory
{
    public static SellableProductView Create(
        SellableProductSnapshot product,
        StockAvailability availability)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(availability);

        return new SellableProductView(
            product.ProductId,
            product.Reference,
            product.DisplayName,
            product.Presentation,
            product.UnitSalePrice,
            availability.AvailableQuantity,
            availability.IsAvailable,
            product.IsSellable);
    }
}
