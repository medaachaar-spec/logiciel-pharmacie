using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Inventory;

public sealed record StockLocationId
{
    private StockLocationId(long value) => Value = value;

    public long Value { get; }

    public static Result<StockLocationId> Create(long value) => value > 0
        ? Result<StockLocationId>.Success(new StockLocationId(value))
        : Result<StockLocationId>.Failure(InventoryErrors.InvalidLocationId);
}

public sealed record StockBatchId
{
    private StockBatchId(long value) => Value = value;

    public long Value { get; }

    public static Result<StockBatchId> Create(long value) => value > 0
        ? Result<StockBatchId>.Success(new StockBatchId(value))
        : Result<StockBatchId>.Failure(InventoryErrors.BatchNotFound);
}

public sealed record InventorySessionId
{
    private InventorySessionId(long value) => Value = value;

    public long Value { get; }

    public static Result<InventorySessionId> Create(long value) => value > 0
        ? Result<InventorySessionId>.Success(new InventorySessionId(value))
        : Result<InventorySessionId>.Failure(InventoryErrors.SessionNotFound);
}
