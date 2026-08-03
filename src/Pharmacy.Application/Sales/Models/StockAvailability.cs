using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Models;

public sealed record StockAvailability(
    ProductId ProductId,
    int AvailableQuantity,
    bool IsAvailable);
