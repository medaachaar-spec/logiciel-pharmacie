using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Purchasing;

public static class PurchaseRules
{
    public static Result ValidateQuantity(int quantity) => quantity > 0
        ? Result.Success()
        : Result.Failure(PurchasingErrors.OrderQuantityInvalid);

    public static Result ValidateUnitPurchasePrice(decimal price) => price >= 0m
        ? Result.Success()
        : Result.Failure(PurchasingErrors.ValidationFailed);

    public static Result ValidateDraftMutation(PurchaseOrderStatus status) => status == PurchaseOrderStatus.Draft
        ? Result.Success()
        : Result.Failure(PurchasingErrors.OrderInvalidStatus);

    public static PurchaseOrderStatus CalculateReceiptStatus(IEnumerable<(int Ordered, int Received)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var snapshot = lines.ToArray();
        return snapshot.Length > 0 && snapshot.All(line => line.Received == line.Ordered)
            ? PurchaseOrderStatus.FullyReceived
            : PurchaseOrderStatus.PartiallyReceived;
    }
}
