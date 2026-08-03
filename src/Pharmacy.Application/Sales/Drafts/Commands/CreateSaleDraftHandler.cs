using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Drafts.Commands;

public sealed class CreateSaleDraftHandler
{
    private readonly ISaleDraftIdGenerator idGenerator;
    private readonly ISaleDraftStore draftStore;

    public CreateSaleDraftHandler(
        ISaleDraftIdGenerator idGenerator,
        ISaleDraftStore draftStore)
    {
        this.idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        this.draftStore = draftStore ?? throw new ArgumentNullException(nameof(draftStore));
    }

    public async Task<Result<SaleDraftView>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var draftId = idGenerator.NewId();
        ArgumentNullException.ThrowIfNull(draftId);

        var created = await draftStore.CreateAsync(
            SaleDraft.Create(draftId),
            cancellationToken);

        return created.IsFailure
            ? Result<SaleDraftView>.Failure(created.Error)
            : Result<SaleDraftView>.Success(SaleDraftViewFactory.Create(created.Value));
    }
}
