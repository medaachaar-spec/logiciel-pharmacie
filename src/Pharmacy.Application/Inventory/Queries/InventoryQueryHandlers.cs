using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Inventory.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Inventory.Queries;

public sealed class InventoryQueryHandlers
{
    private readonly IInventoryReader reader;
    private readonly IInventoryDateProvider dateProvider;
    private readonly IPermissionAuthorizer? authorizer;

    public InventoryQueryHandlers(IInventoryReader reader, IInventoryDateProvider dateProvider)
        : this(reader, dateProvider, null) { }

    public InventoryQueryHandlers(
        IInventoryReader reader,
        IInventoryDateProvider dateProvider,
        IPermissionAuthorizer? authorizer)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.dateProvider = dateProvider ?? throw new ArgumentNullException(nameof(dateProvider));
        this.authorizer = authorizer;
    }

    public Task<Result<IReadOnlyList<StockLocationView>>> ListLocationsAsync(CancellationToken cancellationToken = default)
    {
        var denied = Denied<IReadOnlyList<StockLocationView>>();
        return denied is not null ? Task.FromResult(denied) : reader.ListLocationsAsync(cancellationToken);
    }

    public Task<Result<IReadOnlyList<StockOverviewItem>>> SearchStockAsync(
        StockSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var denied = Denied<IReadOnlyList<StockOverviewItem>>();
        if (denied is not null) return Task.FromResult(denied);
        return criteria.Limit is <= 0 or > 100 || criteria.LocationId <= 0
            ? Task.FromResult(Result<IReadOnlyList<StockOverviewItem>>.Failure(InventoryErrors.InvalidOperation))
            : reader.SearchStockAsync(criteria with { Text = criteria.Text?.Trim() }, dateProvider.CurrentDate, cancellationToken);
    }

    public Task<Result<ProductStockDetails>> GetProductDetailsAsync(long productId, long? locationId = null, CancellationToken cancellationToken = default)
    {
        var denied = Denied<ProductStockDetails>();
        return denied is not null
            ? Task.FromResult(denied)
            : productId <= 0 || locationId <= 0
                ? Task.FromResult(Result<ProductStockDetails>.Failure(InventoryErrors.InvalidOperation))
                : reader.GetProductDetailsAsync(productId, locationId, dateProvider.CurrentDate, cancellationToken);
    }

    public Task<Result<IReadOnlyList<StockMovementView>>> GetMovementsAsync(long? productId, long? locationId, int limit = 100, CancellationToken cancellationToken = default)
    {
        var denied = Denied<IReadOnlyList<StockMovementView>>();
        return denied is not null
            ? Task.FromResult(denied)
            : productId <= 0 || locationId <= 0 || limit is <= 0 or > 500
                ? Task.FromResult(Result<IReadOnlyList<StockMovementView>>.Failure(InventoryErrors.InvalidOperation))
                : reader.GetMovementsAsync(productId, locationId, limit, cancellationToken);
    }

    public Task<Result<IReadOnlyList<StockBatchView>>> GetExpiringBatchesAsync(int horizonDays, CancellationToken cancellationToken = default)
    {
        var denied = Denied<IReadOnlyList<StockBatchView>>();
        return denied is not null
            ? Task.FromResult(denied)
            : horizonDays < 0
                ? Task.FromResult(Result<IReadOnlyList<StockBatchView>>.Failure(InventoryErrors.InvalidOperation))
                : reader.GetExpiringBatchesAsync(dateProvider.CurrentDate, horizonDays, cancellationToken);
    }

    public Task<Result<IReadOnlyList<StockBatchView>>> GetExpiredBatchesAsync(CancellationToken cancellationToken = default)
    {
        var denied = Denied<IReadOnlyList<StockBatchView>>();
        return denied is not null
            ? Task.FromResult(denied)
            : reader.GetExpiredBatchesAsync(dateProvider.CurrentDate, cancellationToken);
    }

    public Task<Result<FefoSuggestionView>> SuggestFefoAsync(long productId, int quantity, CancellationToken cancellationToken = default)
    {
        var denied = Denied<FefoSuggestionView>();
        if (denied is not null) return Task.FromResult(denied);
        var id = ProductId.Create(productId);
        return id.IsFailure || quantity <= 0
            ? Task.FromResult(Result<FefoSuggestionView>.Failure(InventoryErrors.InvalidQuantity))
            : reader.SuggestFefoAsync(id.Value, quantity, dateProvider.CurrentDate, cancellationToken);
    }

    public Task<Result<IReadOnlyList<InventorySessionView>>> ListSessionsAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var denied = Denied<IReadOnlyList<InventorySessionView>>();
        return denied is not null
            ? Task.FromResult(denied)
            : limit is <= 0 or > 100
                ? Task.FromResult(Result<IReadOnlyList<InventorySessionView>>.Failure(InventoryErrors.InvalidOperation))
                : reader.ListSessionsAsync(limit, cancellationToken);
    }

    public Task<Result<InventorySessionView>> GetSessionAsync(long sessionId, CancellationToken cancellationToken = default)
    {
        var denied = Denied<InventorySessionView>();
        return denied is not null
            ? Task.FromResult(denied)
            : sessionId <= 0
                ? Task.FromResult(Result<InventorySessionView>.Failure(InventoryErrors.SessionNotFound))
                : reader.GetSessionAsync(sessionId, cancellationToken);
    }

    public Task<InventoryDiagnosticsView> GetDiagnosticsAsync(CancellationToken cancellationToken = default) =>
        reader.GetDiagnosticsAsync(dateProvider.CurrentDate, cancellationToken);

    private Result<T>? Denied<T>() =>
        SecurityCommandGuard.Denied<T>(authorizer, SecurityPermissions.InventoryView);
}
