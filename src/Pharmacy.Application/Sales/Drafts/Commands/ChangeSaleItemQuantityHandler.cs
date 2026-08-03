using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Drafts.Commands;

public sealed record ChangeSaleItemQuantityCommand(
    SaleDraftId DraftId,
    ProductId ProductId,
    SaleQuantity NewQuantity,
    long ExpectedVersion);

public sealed class ChangeSaleItemQuantityHandler
{
    private readonly ISaleDraftStore draftStore;
    private readonly IStockAvailabilityReader availabilityReader;

    public ChangeSaleItemQuantityHandler(
        ISaleDraftStore draftStore,
        IStockAvailabilityReader availabilityReader)
    {
        this.draftStore = draftStore ?? throw new ArgumentNullException(nameof(draftStore));
        this.availabilityReader = availabilityReader ?? throw new ArgumentNullException(nameof(availabilityReader));
    }

    public async Task<Result<SaleDraftView>> HandleAsync(
        ChangeSaleItemQuantityCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.DraftId);
        ArgumentNullException.ThrowIfNull(command.ProductId);
        ArgumentNullException.ThrowIfNull(command.NewQuantity);

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

        if (!loaded.Value.Draft.Lines.Any(line => line.ProductId == command.ProductId))
        {
            return Result<SaleDraftView>.Failure(SalesErrors.LineNotFound(command.ProductId));
        }

        var availabilityResult = await availabilityReader.GetAvailabilityAsync(
            command.ProductId,
            command.NewQuantity,
            cancellationToken);

        if (availabilityResult.IsFailure)
        {
            return Result<SaleDraftView>.Failure(availabilityResult.Error);
        }

        if (!availabilityResult.Value.IsAvailable)
        {
            return Result<SaleDraftView>.Failure(
                SalesApplicationErrors.InsufficientAvailability(
                    command.ProductId,
                    command.NewQuantity.Value,
                    availabilityResult.Value.AvailableQuantity));
        }

        var mutation = loaded.Value.Draft.ChangeQuantity(
            command.ProductId,
            command.NewQuantity);

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
