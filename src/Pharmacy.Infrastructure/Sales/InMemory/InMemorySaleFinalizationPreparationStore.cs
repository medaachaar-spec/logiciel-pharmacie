using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Application.Sales.Finalization.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Infrastructure.Sales.InMemory;

public sealed class InMemorySaleFinalizationPreparationStore
    : ISaleFinalizationPreparationStore
{
    private readonly Dictionary<Guid, SaleFinalizationPreparation> preparations = new();
    private readonly object syncRoot = new();

    public Task<Result<SaleFinalizationPreparation>> GetOrAddAsync(
        SaleFinalizationPreparation preparation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        cancellationToken.ThrowIfCancellationRequested();

        if (preparation.OperationId == Guid.Empty)
        {
            return Task.FromResult(
                Result<SaleFinalizationPreparation>.Failure(
                    SalesApplicationErrors.InvalidOperationId));
        }

        lock (syncRoot)
        {
            if (!preparations.TryGetValue(preparation.OperationId, out var existing))
            {
                preparations.Add(preparation.OperationId, preparation);
                return Task.FromResult(
                    Result<SaleFinalizationPreparation>.Success(preparation));
            }

            return Task.FromResult(string.Equals(
                    existing.Fingerprint.Value,
                    preparation.Fingerprint.Value,
                    StringComparison.Ordinal)
                ? Result<SaleFinalizationPreparation>.Success(existing)
                : Result<SaleFinalizationPreparation>.Failure(
                    SalesApplicationErrors.OperationConflict));
        }
    }
}
