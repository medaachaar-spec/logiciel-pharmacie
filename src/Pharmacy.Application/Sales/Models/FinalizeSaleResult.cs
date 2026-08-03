using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Models;

public sealed record FinalizeSaleResult(
    SaleDraftId DraftId,
    Guid OperationId);
