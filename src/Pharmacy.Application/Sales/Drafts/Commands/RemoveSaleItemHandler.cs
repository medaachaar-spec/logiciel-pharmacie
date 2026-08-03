using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Drafts.Commands;

public sealed record RemoveSaleItemCommand(
    SaleDraftId DraftId,
    ProductId ProductId,
    long ExpectedVersion);

public sealed class RemoveSaleItemHandler
{
    private readonly ISaleDraftStore draftStore;

    public RemoveSaleItemHandler(ISaleDraftStore draftStore)
    {
        this.draftStore = draftStore ?? throw new ArgumentNullException(nameof(draftStore));
    }

    public async Task<Result<SaleDraftView>> HandleAsync(
        RemoveSaleItemCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.DraftId);
        ArgumentNullException.ThrowIfNull(command.ProductId);

        var loaded = await draftStore.GetAsync(command.DraftId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result<SaleDraftView>.Failure(loaded.Error);
        }

        var versionCheck = DraftVersionGuard.Check(loaded.Value, command.ExpectedVersion);

        if (versionCheck.IsFailure)
        {
            return Result<SaleDraftView>.Failure(versionCheck.Error);
        }

        var mutation = loaded.Value.Draft.RemoveItem(command.ProductId);

        if (mutation.IsFailure)
        {
            return Result<SaleDraftView>.Failure(mutation.Error);
        }

        var saved = await draftStore.SaveAsync(
            loaded.Value.Draft,
            command.ExpectedVersion,
            cancellationToken);

        return saved.IsFailure
            ? Result<SaleDraftView>.Failure(saved.Error)
            : Result<SaleDraftView>.Success(SaleDraftViewFactory.Create(saved.Value));
    }
}
