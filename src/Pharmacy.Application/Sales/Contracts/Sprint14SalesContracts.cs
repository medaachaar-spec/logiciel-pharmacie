using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Sales.Contracts;

public interface ISuspendedSaleStore
{
    Task<Result<SuspendedSaleSummaryView>> SuspendAsync(
        SuspendSaleRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SuspendedSaleSummaryView>>> GetOpenAsync(
        CancellationToken cancellationToken = default);

    Task<Result> AbandonAsync(
        Guid operationId,
        long suspendedSaleId,
        byte[] rowVersion,
        CancellationToken cancellationToken = default);
}

public interface ISaleReturnStore
{
    Task<Result<SaleReturnView>> ReturnAsync(
        ReturnSaleRequest request,
        CancellationToken cancellationToken = default);
}
