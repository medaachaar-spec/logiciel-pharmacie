using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Catalog.Queries;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Drafts.Commands;

public sealed record AddProductToSaleDraftCommand(
    SaleDraftId DraftId,
    SellableProductLookup Product,
    SaleQuantity Quantity,
    long ExpectedVersion);

public sealed class AddProductToSaleDraftHandler
{
    private readonly ISaleDraftStore draftStore;
    private readonly IStockAvailabilityReader availabilityReader;
    private readonly SellableProductResolver productResolver;

    public AddProductToSaleDraftHandler(
        ISaleDraftStore draftStore,
        IProductCatalogReader catalogReader,
        IStockAvailabilityReader availabilityReader)
    {
        this.draftStore = draftStore ?? throw new ArgumentNullException(nameof(draftStore));
        productResolver = new SellableProductResolver(
            catalogReader ?? throw new ArgumentNullException(nameof(catalogReader)));
        this.availabilityReader = availabilityReader ?? throw new ArgumentNullException(nameof(availabilityReader));
    }

    public async Task<Result<SaleDraftView>> HandleAsync(
        AddProductToSaleDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.DraftId);
        ArgumentNullException.ThrowIfNull(command.Product);
        ArgumentNullException.ThrowIfNull(command.Quantity);

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

        var productResult = await productResolver.ResolveAsync(command.Product, cancellationToken);

        if (productResult.IsFailure)
        {
            return Result<SaleDraftView>.Failure(productResult.Error);
        }

        var product = productResult.Value;
        var existingLine = loaded.Value.Draft.Lines.FirstOrDefault(
            line => line.ProductId == product.ProductId);
        var targetQuantityResult = existingLine is null
            ? Result<SaleQuantity>.Success(command.Quantity)
            : existingLine.Quantity.Add(command.Quantity);

        if (targetQuantityResult.IsFailure)
        {
            return Result<SaleDraftView>.Failure(targetQuantityResult.Error);
        }

        var availabilityResult = await availabilityReader.GetAvailabilityAsync(
            product.ProductId,
            targetQuantityResult.Value,
            cancellationToken);

        if (availabilityResult.IsFailure)
        {
            return Result<SaleDraftView>.Failure(availabilityResult.Error);
        }

        if (!availabilityResult.Value.IsAvailable)
        {
            return Result<SaleDraftView>.Failure(
                SalesApplicationErrors.InsufficientAvailability(
                    product.ProductId,
                    targetQuantityResult.Value.Value,
                    availabilityResult.Value.AvailableQuantity));
        }

        var snapshotResult = SaleProductSnapshot.Create(
            product.ProductId,
            product.Reference,
            product.DisplayName,
            product.Presentation,
            product.UnitSalePrice);

        if (snapshotResult.IsFailure)
        {
            return Result<SaleDraftView>.Failure(snapshotResult.Error);
        }

        var mutation = loaded.Value.Draft.AddItem(snapshotResult.Value, command.Quantity);

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
