using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Drafts.Models;

public sealed record SaleDraftLineView(
    ProductId ProductId,
    ProductReference Reference,
    string DisplayName,
    string? Presentation,
    Money UnitPrice,
    SaleQuantity Quantity,
    Money Subtotal);
