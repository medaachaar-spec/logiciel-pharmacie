using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Drafts.Commands;

public sealed class RestoreSuspendedSaleDraftHandler
{
    private readonly ISaleDraftIdGenerator idGenerator;
    private readonly ISaleDraftStore draftStore;

    public RestoreSuspendedSaleDraftHandler(
        ISaleDraftIdGenerator idGenerator,
        ISaleDraftStore draftStore)
    {
        this.idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        this.draftStore = draftStore ?? throw new ArgumentNullException(nameof(draftStore));
    }

    public async Task<Result<SaleDraftView>> HandleAsync(
        SuspendedSaleSummaryView suspension,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(suspension);
        if (suspension.Lines.Count == 0)
        {
            return Result<SaleDraftView>.Failure(SalesApplicationErrors.DraftEmpty);
        }

        var draft = SaleDraft.Create(idGenerator.NewId());
        foreach (var line in suspension.Lines)
        {
            var snapshot = SaleProductSnapshot.Create(
                line.ProductId,
                line.ProductReference,
                line.ProductName,
                line.Presentation,
                line.UnitPrice);
            if (snapshot.IsFailure)
            {
                return Result<SaleDraftView>.Failure(snapshot.Error);
            }

            var mutation = draft.AddItem(snapshot.Value, line.Quantity);
            if (mutation.IsFailure)
            {
                return Result<SaleDraftView>.Failure(mutation.Error);
            }
        }

        var created = await draftStore.CreateAsync(draft, cancellationToken);
        return created.IsFailure
            ? Result<SaleDraftView>.Failure(created.Error)
            : Result<SaleDraftView>.Success(SaleDraftViewFactory.Create(created.Value));
    }
}
