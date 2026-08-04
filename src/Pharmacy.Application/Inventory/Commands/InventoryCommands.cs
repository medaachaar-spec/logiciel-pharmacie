using Pharmacy.Domain.Inventory;

namespace Pharmacy.Application.Inventory.Commands;

public sealed record CreateStockLocationCommand(
    Guid OperationId,
    string Code,
    string Name,
    bool IsPrimary = false);

public sealed record SetStockLocationActivationCommand(
    Guid OperationId,
    long LocationId,
    bool IsActive,
    string RowVersion);

public sealed record CreateStockBatchCommand(
    Guid OperationId,
    long ProductId,
    string? LotNumber,
    DateOnly? ExpiryDate,
    bool IsActive = true);

public sealed record RecordStockMovementCommand(
    Guid OperationId,
    StockMovementType MovementType,
    long ProductId,
    long LocationId,
    long? BatchId,
    int Quantity,
    string Reason,
    string SourceType,
    string? SourceReference = null,
    string? ExpectedBalanceRowVersion = null);

public sealed record CreateInventorySessionCommand(
    Guid OperationId,
    string Reference,
    long LocationId,
    string? Notes = null);

public sealed record StartInventorySessionCommand(
    Guid OperationId,
    long SessionId,
    string SessionRowVersion);

public sealed record SaveInventoryCountLineCommand(
    Guid OperationId,
    long SessionId,
    long CountLineId,
    int CountedQuantity,
    string SessionRowVersion,
    string LineRowVersion);

public sealed record FinalizeInventorySessionCommand(
    Guid OperationId,
    long SessionId,
    string SessionRowVersion);

public sealed record CancelInventorySessionCommand(
    Guid OperationId,
    long SessionId,
    string SessionRowVersion);
