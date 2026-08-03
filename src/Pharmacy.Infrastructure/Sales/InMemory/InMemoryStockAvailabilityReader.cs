using System.Collections.ObjectModel;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Infrastructure.Sales.InMemory;

public sealed class InMemoryStockAvailabilityReader : IStockAvailabilityReader
{
    private readonly IReadOnlyDictionary<ProductId, int> availability;

    public InMemoryStockAvailabilityReader()
    {
        availability = new ReadOnlyDictionary<ProductId, int>(
            new Dictionary<ProductId, int>
            {
                [ProductId.Create(1).Value] = 8,
                [ProductId.Create(2).Value] = 6,
                [ProductId.Create(3).Value] = 10,
                [ProductId.Create(4).Value] = 5,
                [ProductId.Create(5).Value] = 4,
                [ProductId.Create(6).Value] = 7,
                [ProductId.Create(7).Value] = 6,
                [ProductId.Create(8).Value] = 9,
                [ProductId.Create(9).Value] = 3,
                [ProductId.Create(10).Value] = 12
            });
    }

    public Task<Result<StockAvailability>> GetAvailabilityAsync(
        ProductId productId,
        SaleQuantity requestedQuantity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productId);
        ArgumentNullException.ThrowIfNull(requestedQuantity);
        cancellationToken.ThrowIfCancellationRequested();

        if (!availability.TryGetValue(productId, out var availableQuantity))
        {
            return Task.FromResult(
                Result<StockAvailability>.Failure(
                    SalesApplicationErrors.ProductNotFound));
        }

        return Task.FromResult(
            Result<StockAvailability>.Success(
                new StockAvailability(
                    productId,
                    availableQuantity,
                    availableQuantity >= requestedQuantity.Value)));
    }
}
