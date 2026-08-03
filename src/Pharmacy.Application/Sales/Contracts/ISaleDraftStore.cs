using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Contracts;

public interface ISaleDraftStore
{
    Task<Result<VersionedSaleDraft>> CreateAsync(
        SaleDraft draft,
        CancellationToken cancellationToken = default);

    Task<Result<VersionedSaleDraft>> GetAsync(
        SaleDraftId draftId,
        CancellationToken cancellationToken = default);

    Task<Result<VersionedSaleDraft>> SaveAsync(
        SaleDraft draft,
        long expectedVersion,
        CancellationToken cancellationToken = default);
}
