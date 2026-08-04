using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Inventory;

public static class InventoryTrackingPolicy
{
    public static Result Validate(
        bool tracksLots,
        bool tracksExpiry,
        LotNumber? lotNumber,
        ExpiryDate? expiryDate)
    {
        if (tracksLots && lotNumber is null)
        {
            return Result.Failure(InventoryErrors.LotRequired);
        }

        if (tracksExpiry && expiryDate is null)
        {
            return Result.Failure(InventoryErrors.ExpiryRequired);
        }

        return Result.Success();
    }
}
