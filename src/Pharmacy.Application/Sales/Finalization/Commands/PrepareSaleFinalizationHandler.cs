using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Application.Sales.Finalization.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Finalization.Commands;

public sealed record PrepareSaleFinalizationCommand(
    Guid OperationId,
    SaleDraftId DraftId,
    long ExpectedVersion);

public sealed class PrepareSaleFinalizationHandler
{
    private readonly ISaleDraftStore draftStore;
    private readonly IStockAvailabilityReader availabilityReader;
    private readonly ISaleFinalizationPreparationStore preparationStore;

    public PrepareSaleFinalizationHandler(
        ISaleDraftStore draftStore,
        IStockAvailabilityReader availabilityReader,
        ISaleFinalizationPreparationStore preparationStore)
    {
        this.draftStore = draftStore ?? throw new ArgumentNullException(nameof(draftStore));
        this.availabilityReader = availabilityReader ?? throw new ArgumentNullException(nameof(availabilityReader));
        this.preparationStore = preparationStore ?? throw new ArgumentNullException(nameof(preparationStore));
    }

    public async Task<Result<SaleFinalizationPreparation>> HandleAsync(
        PrepareSaleFinalizationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.DraftId);

        if (command.OperationId == Guid.Empty)
        {
            return Result<SaleFinalizationPreparation>.Failure(
                SalesApplicationErrors.InvalidOperationId);
        }

        var loaded = await draftStore.GetAsync(command.DraftId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result<SaleFinalizationPreparation>.Failure(loaded.Error);
        }

        var versionCheck = DraftVersionGuard.Check(loaded.Value, command.ExpectedVersion);

        if (versionCheck.IsFailure)
        {
            return Result<SaleFinalizationPreparation>.Failure(versionCheck.Error);
        }

        if (loaded.Value.Draft.IsEmpty)
        {
            return Result<SaleFinalizationPreparation>.Failure(
                SalesApplicationErrors.DraftEmpty);
        }

        foreach (var line in loaded.Value.Draft.Lines)
        {
            var availabilityResult = await availabilityReader.GetAvailabilityAsync(
                line.ProductId,
                line.Quantity,
                cancellationToken);

            if (availabilityResult.IsFailure)
            {
                return Result<SaleFinalizationPreparation>.Failure(
                    availabilityResult.Error.Code.EndsWith("database.unavailable", StringComparison.Ordinal)
                        ? SalesApplicationErrors.DatabaseUnavailable
                        : availabilityResult.Error);
            }

            if (!availabilityResult.Value.IsAvailable)
            {
                return Result<SaleFinalizationPreparation>.Failure(
                    SalesApplicationErrors.StockInsufficient);
            }
        }

        var view = SaleDraftViewFactory.Create(loaded.Value);
        var preparation = SaleFinalizationPreparationFactory.Create(
            command.OperationId,
            view);

        return await preparationStore.GetOrAddAsync(
            preparation,
            cancellationToken);
    }
}
