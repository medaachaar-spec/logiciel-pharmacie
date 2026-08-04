using Pharmacy.Application.Inventory.Contracts;

namespace Pharmacy.Infrastructure.Inventory;

public sealed class SystemInventoryDateProvider : IInventoryDateProvider
{
    public DateOnly CurrentDate => DateOnly.FromDateTime(DateTime.Now);
}
