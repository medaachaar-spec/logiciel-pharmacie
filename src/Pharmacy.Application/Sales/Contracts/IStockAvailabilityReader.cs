using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Contracts;

public interface IStockAvailabilityReader
{
    Task<Result<StockAvailability>> GetAvailabilityAsync(
        ProductId productId,
        SaleQuantity requestedQuantity,
        CancellationToken cancellationToken = default);
}
