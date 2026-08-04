using Pharmacy.Domain.Inventory;

namespace Pharmacy.Application.Inventory.Models;

public enum StockAvailabilityFilter
{
    All,
    Available,
    OutOfStock,
    Expired
}

public sealed record StockSearchCriteria(
    string? Text = null,
    long? LocationId = null,
    StockAvailabilityFilter Availability = StockAvailabilityFilter.All,
    int Limit = 50);

public sealed record StockLocationView(
    long LocationId,
    string Code,
    string Name,
    bool IsActive,
    bool IsPrimary,
    DateTime CreatedAtUtc,
    string RowVersion);

public sealed record StockOverviewItem(
    long ProductId,
    string InternalCode,
    string ProductName,
    int TotalQuantity,
    int SellableQuantity,
    int ExpiredQuantity,
    int BatchCount,
    bool TracksLots,
    bool TracksExpiry);

public sealed record StockBatchView(
    long BalanceId,
    long ProductId,
    long LocationId,
    string LocationCode,
    string LocationName,
    long? BatchId,
    string? LotNumber,
    DateOnly? ExpiryDate,
    bool IsBatchActive,
    bool IsExpired,
    int AvailableQuantity,
    string BalanceRowVersion,
    string? BatchRowVersion);

public sealed class ProductStockDetails
{
    public ProductStockDetails(
        long productId,
        string internalCode,
        string productName,
        bool tracksLots,
        bool tracksExpiry,
        IEnumerable<StockBatchView> balances)
    {
        ArgumentNullException.ThrowIfNull(balances);
        ProductId = productId;
        InternalCode = internalCode;
        ProductName = productName;
        TracksLots = tracksLots;
        TracksExpiry = tracksExpiry;
        Balances = Array.AsReadOnly(balances.ToArray());
    }

    public long ProductId { get; }
    public string InternalCode { get; }
    public string ProductName { get; }
    public bool TracksLots { get; }
    public bool TracksExpiry { get; }
    public IReadOnlyList<StockBatchView> Balances { get; }
    public int TotalQuantity => Balances.Sum(item => item.AvailableQuantity);
    public int SellableQuantity => Balances.Where(item => !item.IsExpired && item.IsBatchActive).Sum(item => item.AvailableQuantity);
}

public sealed record StockMovementView(
    long MovementId,
    Guid OperationId,
    long ProductId,
    string InternalCode,
    string ProductName,
    long LocationId,
    string LocationCode,
    long? BatchId,
    string? LotNumber,
    StockMovementType MovementType,
    int Quantity,
    int QuantityBefore,
    int QuantityAfter,
    string Reason,
    string SourceType,
    string? SourceReference,
    DateTime OccurredAtUtc);

public sealed record FefoAllocationLineView(
    long BalanceId,
    long? BatchId,
    string? LotNumber,
    DateOnly? ExpiryDate,
    int Quantity);

public sealed class FefoSuggestionView
{
    public FefoSuggestionView(long productId, int requestedQuantity, IEnumerable<FefoAllocationLineView> allocations)
    {
        ArgumentNullException.ThrowIfNull(allocations);
        ProductId = productId;
        RequestedQuantity = requestedQuantity;
        Allocations = Array.AsReadOnly(allocations.ToArray());
    }

    public long ProductId { get; }
    public int RequestedQuantity { get; }
    public IReadOnlyList<FefoAllocationLineView> Allocations { get; }
    public int AllocatedQuantity => Allocations.Sum(item => item.Quantity);
}

public sealed record InventoryCountLineView(
    long CountLineId,
    long ProductId,
    string InternalCode,
    string ProductName,
    long? BatchId,
    string? LotNumber,
    int ExpectedQuantity,
    int? CountedQuantity,
    int? DifferenceQuantity,
    string RowVersion);

public sealed class InventorySessionView
{
    public InventorySessionView(
        long sessionId,
        string reference,
        long locationId,
        string locationCode,
        InventorySessionStatus status,
        DateTime startedAtUtc,
        DateTime? completedAtUtc,
        string? notes,
        string rowVersion,
        IEnumerable<InventoryCountLineView> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        SessionId = sessionId;
        Reference = reference;
        LocationId = locationId;
        LocationCode = locationCode;
        Status = status;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        Notes = notes;
        RowVersion = rowVersion;
        Lines = Array.AsReadOnly(lines.ToArray());
    }

    public long SessionId { get; }
    public string Reference { get; }
    public long LocationId { get; }
    public string LocationCode { get; }
    public InventorySessionStatus Status { get; }
    public DateTime StartedAtUtc { get; }
    public DateTime? CompletedAtUtc { get; }
    public string? Notes { get; }
    public string RowVersion { get; }
    public IReadOnlyList<InventoryCountLineView> Lines { get; }
}

public sealed record InventoryDiagnosticsView(
    bool IsConfigured,
    bool IsReachable,
    string DatabaseName,
    string MigrationStatus,
    int LocationCount,
    int BatchCount,
    int PositiveBalanceCount,
    int ExpiredDemoBatchCount,
    string? ErrorCode);
