using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Sales.Contracts;

public interface ISalesReader
{
    Task<Result<FinalizedSaleView>> GetByIdAsync(
        long saleId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<FinalizedSaleSummaryView>>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<Result<FinalizedSaleView>> GetByIdForUserAsync(
        long saleId,
        long userAccountId,
        CancellationToken cancellationToken = default) => GetByIdAsync(saleId, cancellationToken);

    Task<Result<IReadOnlyList<FinalizedSaleSummaryView>>> GetRecentForUserAsync(
        int limit,
        long userAccountId,
        CancellationToken cancellationToken = default) => GetRecentAsync(limit, cancellationToken);
}
