using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Inventory;

public sealed record StockBalanceState
{
    private StockBalanceState(StockQuantity quantity) => Quantity = quantity;

    public StockQuantity Quantity { get; }

    public static Result<StockBalanceState> Create(int quantity)
    {
        var value = StockQuantity.Create(quantity);
        return value.IsFailure
            ? Result<StockBalanceState>.Failure(value.Error)
            : Result<StockBalanceState>.Success(new StockBalanceState(value.Value));
    }

    public Result<StockBalanceState> Apply(
        StockMovementType movementType,
        StockMovementQuantity quantity)
    {
        ArgumentNullException.ThrowIfNull(quantity);

        var changed = movementType.IsIncrease()
            ? Quantity.Increase(quantity)
            : movementType.IsDecrease()
                ? Quantity.Decrease(quantity)
                : Result<StockQuantity>.Failure(InventoryErrors.InvalidOperation);

        return changed.IsFailure
            ? Result<StockBalanceState>.Failure(changed.Error)
            : Result<StockBalanceState>.Success(new StockBalanceState(changed.Value));
    }
}
