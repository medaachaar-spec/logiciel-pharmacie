using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Infrastructure.Sales.InMemory;

public sealed class InMemorySaleDraftIdGenerator : ISaleDraftIdGenerator
{
    public SaleDraftId NewId()
    {
        return SaleDraftId.Create(Guid.NewGuid()).Value;
    }
}
