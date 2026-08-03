using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Models;

public sealed record FinalizeSaleRequest(
    SaleDraft Draft,
    Guid OperationId);
