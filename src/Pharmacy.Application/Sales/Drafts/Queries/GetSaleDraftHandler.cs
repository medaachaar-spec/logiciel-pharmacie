using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Drafts.Queries;

public sealed record GetSaleDraftQuery(SaleDraftId DraftId);

public sealed class GetSaleDraftHandler
{
    private readonly ISaleDraftStore draftStore;

    public GetSaleDraftHandler(ISaleDraftStore draftStore)
    {
        this.draftStore = draftStore ?? throw new ArgumentNullException(nameof(draftStore));
    }

    public async Task<Result<SaleDraftView>> HandleAsync(
        GetSaleDraftQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.DraftId);

        var draftResult = await draftStore.GetAsync(query.DraftId, cancellationToken);

        return draftResult.IsFailure
            ? Result<SaleDraftView>.Failure(draftResult.Error)
            : Result<SaleDraftView>.Success(SaleDraftViewFactory.Create(draftResult.Value));
    }
}
