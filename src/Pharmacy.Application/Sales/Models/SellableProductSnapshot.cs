using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Models;

public sealed record SellableProductSnapshot(
    ProductId ProductId,
    ProductReference Reference,
    string DisplayName,
    string? Presentation,
    Money UnitSalePrice,
    bool IsSellable);
