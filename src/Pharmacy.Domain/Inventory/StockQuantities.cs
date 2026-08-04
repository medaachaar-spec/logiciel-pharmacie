using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Inventory;

public sealed record StockQuantity : IComparable<StockQuantity>
{
    public static StockQuantity Zero { get; } = new(0);

    private StockQuantity(int value) => Value = value;

    public int Value { get; }

    public static Result<StockQuantity> Create(int value) => value >= 0
        ? Result<StockQuantity>.Success(new StockQuantity(value))
        : Result<StockQuantity>.Failure(InventoryErrors.InvalidQuantity);

    public Result<StockQuantity> Increase(StockMovementQuantity quantity)
    {
        ArgumentNullException.ThrowIfNull(quantity);

        try
        {
            return Result<StockQuantity>.Success(new StockQuantity(checked(Value + quantity.Value)));
        }
        catch (OverflowException)
        {
            return Result<StockQuantity>.Failure(InventoryErrors.InvalidQuantity);
        }
    }

    public Result<StockQuantity> Decrease(StockMovementQuantity quantity)
    {
        ArgumentNullException.ThrowIfNull(quantity);
        return quantity.Value > Value
            ? Result<StockQuantity>.Failure(InventoryErrors.InsufficientStock)
            : Result<StockQuantity>.Success(new StockQuantity(Value - quantity.Value));
    }

    public int CompareTo(StockQuantity? other) => other is null ? 1 : Value.CompareTo(other.Value);
}

public sealed record StockMovementQuantity
{
    private StockMovementQuantity(int value) => Value = value;

    public int Value { get; }

    public static Result<StockMovementQuantity> Create(int value) => value > 0
        ? Result<StockMovementQuantity>.Success(new StockMovementQuantity(value))
        : Result<StockMovementQuantity>.Failure(InventoryErrors.InvalidQuantity);
}
