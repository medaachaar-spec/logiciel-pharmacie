using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Contracts;

public interface ISaleDraftIdGenerator
{
    SaleDraftId NewId();
}
