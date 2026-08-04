using System.Collections.Concurrent;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Infrastructure.Sales.InMemory;

public sealed class SyntheticCatalogStockAvailabilityReader : IStockAvailabilityReader
{
    private readonly ConcurrentDictionary<long, int> quantities = new();

    public Task<Result<StockAvailability>> GetAvailabilityAsync(
        ProductId productId,
        SaleQuantity requestedQuantity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productId);
        ArgumentNullException.ThrowIfNull(requestedQuantity);
        cancellationToken.ThrowIfCancellationRequested();

        var available = quantities.GetOrAdd(
            productId.Value,
            static value => checked(5 + (int)(value % 8)));
        return Task.FromResult(Result<StockAvailability>.Success(
            new StockAvailability(
                productId,
                available,
                available >= requestedQuantity.Value)));
    }
}
