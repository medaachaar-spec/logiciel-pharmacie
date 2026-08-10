using Pharmacy.Application.Sales.Contracts;

namespace Pharmacy.Infrastructure.Sales;

internal sealed class SystemSalesClock : ISalesClock
{
    public DateOnly CurrentDate => DateOnly.FromDateTime(DateTime.Now);

    public DateTime UtcNow => DateTime.UtcNow;
}
