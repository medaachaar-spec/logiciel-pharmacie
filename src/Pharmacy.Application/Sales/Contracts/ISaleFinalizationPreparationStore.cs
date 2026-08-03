using Pharmacy.Application.Sales.Finalization.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Sales.Contracts;

public interface ISaleFinalizationPreparationStore
{
    Task<Result<SaleFinalizationPreparation>> GetOrAddAsync(
        SaleFinalizationPreparation preparation,
        CancellationToken cancellationToken = default);
}
