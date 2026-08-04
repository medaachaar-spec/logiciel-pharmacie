using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Inventory.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Inventory.Queries;

public sealed class InventoryQueryHandlers
{
    private readonly IInventoryReader reader;
    private readonly IInventoryDateProvider dateProvider;

    public InventoryQueryHandlers(IInventoryReader reader, IInventoryDateProvider dateProvider)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.dateProvider = dateProvider ?? throw new ArgumentNullException(nameof(dateProvider));
    }

    public Task<Result<IReadOnlyList<StockLocationView>>> ListLocationsAsync(CancellationToken cancellationToken = default) =>
        reader.ListLocationsAsync(cancellationToken);

    public Task<Result<IReadOnlyList<StockOverviewItem>>> SearchStockAsync(
        StockSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return criteria.Limit is <= 0 or > 100 || criteria.LocationId <= 0
            ? Task.FromResult(Result<IReadOnlyList<StockOverviewItem>>.Failure(InventoryErrors.InvalidOperation))
            : reader.SearchStockAsync(criteria with { Text = criteria.Text?.Trim() }, dateProvider.CurrentDate, cancellationToken);
    }

    public Task<Result<ProductStockDetails>> GetProductDetailsAsync(long productId, long? locationId = null, CancellationToken cancellationToken = default) =>
        productId <= 0 || locationId <= 0
            ? Task.FromResult(Result<ProductStockDetails>.Failure(InventoryErrors.InvalidOperation))
            : reader.GetProductDetailsAsync(productId, locationId, dateProvider.CurrentDate, cancellationToken);

    public Task<Result<IReadOnlyList<StockMovementView>>> GetMovementsAsync(long? productId, long? locationId, int limit = 100, CancellationToken cancellationToken = default) =>
        productId <= 0 || locationId <= 0 || limit is <= 0 or > 500
            ? Task.FromResult(Result<IReadOnlyList<StockMovementView>>.Failure(InventoryErrors.InvalidOperation))
            : reader.GetMovementsAsync(productId, locationId, limit, cancellationToken);

    public Task<Result<IReadOnlyList<StockBatchView>>> GetExpiringBatchesAsync(int horizonDays, CancellationToken cancellationToken = default) =>
        horizonDays < 0
            ? Task.FromResult(Result<IReadOnlyList<StockBatchView>>.Failure(InventoryErrors.InvalidOperation))
            : reader.GetExpiringBatchesAsync(dateProvider.CurrentDate, horizonDays, cancellationToken);

    public Task<Result<IReadOnlyList<StockBatchView>>> GetExpiredBatchesAsync(CancellationToken cancellationToken = default) =>
        reader.GetExpiredBatchesAsync(dateProvider.CurrentDate, cancellationToken);

    public Task<Result<FefoSuggestionView>> SuggestFefoAsync(long productId, int quantity, CancellationToken cancellationToken = default)
    {
        var id = ProductId.Create(productId);
        return id.IsFailure || quantity <= 0
            ? Task.FromResult(Result<FefoSuggestionView>.Failure(InventoryErrors.InvalidQuantity))
            : reader.SuggestFefoAsync(id.Value, quantity, dateProvider.CurrentDate, cancellationToken);
    }

    public Task<Result<IReadOnlyList<InventorySessionView>>> ListSessionsAsync(int limit = 50, CancellationToken cancellationToken = default) =>
        limit is <= 0 or > 100
            ? Task.FromResult(Result<IReadOnlyList<InventorySessionView>>.Failure(InventoryErrors.InvalidOperation))
            : reader.ListSessionsAsync(limit, cancellationToken);

    public Task<Result<InventorySessionView>> GetSessionAsync(long sessionId, CancellationToken cancellationToken = default) =>
        sessionId <= 0
            ? Task.FromResult(Result<InventorySessionView>.Failure(InventoryErrors.SessionNotFound))
            : reader.GetSessionAsync(sessionId, cancellationToken);

    public Task<InventoryDiagnosticsView> GetDiagnosticsAsync(CancellationToken cancellationToken = default) =>
        reader.GetDiagnosticsAsync(dateProvider.CurrentDate, cancellationToken);
}
