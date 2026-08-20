using Pharmacy.Application.Sales.Finalization.Models;

namespace Pharmacy.Application.Sales.Models;

public sealed record FinalizeSaleRequest(
    SaleFinalizationPreparation Preparation,
    SalePaymentRequest Payment,
    SaleCreditRequest? Credit = null,
    SuspendedSaleCompletionRequest? SuspendedSale = null);

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

public sealed record SaleCreditRequest(
    long CustomerId,
    decimal Amount);
