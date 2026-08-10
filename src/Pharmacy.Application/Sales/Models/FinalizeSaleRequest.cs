using Pharmacy.Application.Sales.Finalization.Models;

namespace Pharmacy.Application.Sales.Models;

public sealed record FinalizeSaleRequest(
    SaleFinalizationPreparation Preparation,
    SalePaymentRequest Payment);

public sealed record SalePaymentRequest(
    SalePaymentMethod Method,
    decimal Amount,
    decimal? TenderedAmount,
    decimal ChangeGiven,
    bool ExternalCardConfirmed);

public enum SalePaymentMethod
{
    Cash,
    ExternalCard
}
