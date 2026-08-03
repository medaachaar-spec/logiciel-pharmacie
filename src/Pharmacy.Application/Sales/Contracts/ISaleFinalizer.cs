using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Sales.Contracts;

public interface ISaleFinalizer
{
    Task<Result<FinalizeSaleResult>> FinalizeAsync(
        FinalizeSaleRequest request,
        CancellationToken cancellationToken = default);
}
