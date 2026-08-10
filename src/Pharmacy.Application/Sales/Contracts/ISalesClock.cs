namespace Pharmacy.Application.Sales.Contracts;

public interface ISalesClock
{
    DateOnly CurrentDate { get; }

    DateTime UtcNow { get; }
}
