using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Catalog.Models;

public sealed record SellableProductView(
    ProductId ProductId,
    ProductReference Reference,
    string DisplayName,
    string? Presentation,
    Money UnitSalePrice,
    int AvailableQuantity,
    bool IsAvailable,
    bool IsSellable);
