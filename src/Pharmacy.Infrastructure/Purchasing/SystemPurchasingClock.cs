using Pharmacy.Application.Purchasing.Contracts;

namespace Pharmacy.Infrastructure.Purchasing;

internal sealed class SystemPurchasingClock : IPurchasingClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly CurrentDate => DateOnly.FromDateTime(UtcNow);
}
