namespace Pharmacy.Domain.Inventory;

public enum StockMovementType
{
    Opening,
    ManualIncrease,
    ManualDecrease,
    InventoryIncrease,
    InventoryDecrease,
    PurchaseReceipt,
    Sale
}

public static class StockMovementTypeExtensions
{
    public static bool IsIncrease(this StockMovementType type) => type is
        StockMovementType.Opening or StockMovementType.ManualIncrease or StockMovementType.InventoryIncrease or StockMovementType.PurchaseReceipt;

    public static bool IsDecrease(this StockMovementType type) => type is
        StockMovementType.ManualDecrease or StockMovementType.InventoryDecrease or StockMovementType.Sale;
}
