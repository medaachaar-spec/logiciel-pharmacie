using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Inventory.Contracts;

public interface IInventoryDateProvider
{
    DateOnly CurrentDate { get; }
}

public interface IInventoryReader
{
    Task<Result<IReadOnlyList<StockLocationView>>> ListLocationsAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<StockOverviewItem>>> SearchStockAsync(StockSearchCriteria criteria, DateOnly currentDate, CancellationToken cancellationToken = default);
    Task<Result<ProductStockDetails>> GetProductDetailsAsync(long productId, long? locationId, DateOnly currentDate, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<StockMovementView>>> GetMovementsAsync(long? productId, long? locationId, int limit, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<StockBatchView>>> GetExpiringBatchesAsync(DateOnly currentDate, int horizonDays, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<StockBatchView>>> GetExpiredBatchesAsync(DateOnly currentDate, CancellationToken cancellationToken = default);
    Task<Result<FefoSuggestionView>> SuggestFefoAsync(ProductId productId, int requestedQuantity, DateOnly currentDate, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<InventorySessionView>>> ListSessionsAsync(int limit, CancellationToken cancellationToken = default);
    Task<Result<InventorySessionView>> GetSessionAsync(long sessionId, CancellationToken cancellationToken = default);
    Task<InventoryDiagnosticsView> GetDiagnosticsAsync(DateOnly currentDate, CancellationToken cancellationToken = default);
}

public interface IInventoryStore
{
    Task<Result<StockLocationView>> CreateLocationAsync(CreateStockLocationCommand command, CancellationToken cancellationToken = default);
    Task<Result<StockLocationView>> SetLocationActivationAsync(SetStockLocationActivationCommand command, CancellationToken cancellationToken = default);
    Task<Result<StockBatchView>> CreateBatchAsync(CreateStockBatchCommand command, DateOnly currentDate, CancellationToken cancellationToken = default);
    Task<Result<StockMovementView>> RecordMovementAsync(RecordStockMovementCommand command, DateOnly currentDate, CancellationToken cancellationToken = default);
    Task<Result<InventorySessionView>> CreateSessionAsync(CreateInventorySessionCommand command, CancellationToken cancellationToken = default);
    Task<Result<InventorySessionView>> StartSessionAsync(StartInventorySessionCommand command, CancellationToken cancellationToken = default);
    Task<Result<InventorySessionView>> SaveCountLineAsync(SaveInventoryCountLineCommand command, CancellationToken cancellationToken = default);
    Task<Result<InventorySessionView>> FinalizeSessionAsync(FinalizeInventorySessionCommand command, CancellationToken cancellationToken = default);
    Task<Result<InventorySessionView>> CancelSessionAsync(CancelInventorySessionCommand command, CancellationToken cancellationToken = default);
}
