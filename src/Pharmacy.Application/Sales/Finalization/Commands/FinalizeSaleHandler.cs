using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Finalization.Commands;

public sealed record FinalizeSaleCommand(
    Guid OperationId,
    SaleDraftId DraftId,
    long ExpectedVersion,
    SalePaymentMethod PaymentMethod,
    decimal? TenderedAmount,
    bool ExternalCardConfirmed,
    long? CustomerId = null,
    decimal CreditAmount = 0m);

public sealed class FinalizeSaleHandler
{
    private readonly PrepareSaleFinalizationHandler preparationHandler;
    private readonly ISaleFinalizer finalizer;

    public FinalizeSaleHandler(
        PrepareSaleFinalizationHandler preparationHandler,
        ISaleFinalizer finalizer)
    {
        this.preparationHandler = preparationHandler
            ?? throw new ArgumentNullException(nameof(preparationHandler));
        this.finalizer = finalizer ?? throw new ArgumentNullException(nameof(finalizer));
    }

    public async Task<Result<FinalizedSaleView>> HandleAsync(
        FinalizeSaleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.DraftId);

        if (command.OperationId == Guid.Empty)
        {
            return Result<FinalizedSaleView>.Failure(SalesApplicationErrors.InvalidOperationId);
        }

        var preparation = await preparationHandler.HandleAsync(
            new PrepareSaleFinalizationCommand(
                command.OperationId,
                command.DraftId,
                command.ExpectedVersion),
            cancellationToken);

        if (preparation.IsFailure)
        {
            return Result<FinalizedSaleView>.Failure(preparation.Error);
        }

        var payment = ValidatePayment(command, preparation.Value.Total);

        if (payment.IsFailure)
        {
            return Result<FinalizedSaleView>.Failure(payment.Error);
        }

        var creditError = ValidateCredit(command, preparation.Value.Total);

        if (creditError is not null)
        {
            return Result<FinalizedSaleView>.Failure(creditError);
        }

        var credit = command.CreditAmount == 0m
            ? null
            : new SaleCreditRequest(command.CustomerId!.Value, command.CreditAmount);
        return await finalizer.FinalizeAsync(
            new FinalizeSaleRequest(preparation.Value, payment.Value, credit),
            cancellationToken);
    }

    private static Result<SalePaymentRequest> ValidatePayment(
        FinalizeSaleCommand command,
        Money total)
    {
        var immediateAmount = total.Amount - command.CreditAmount;

        if (immediateAmount < 0m || command.CreditAmount < 0m)
        {
            return Result<SalePaymentRequest>.Failure(SalesApplicationErrors.PaymentInvalid);
        }

        if (command.PaymentMethod == SalePaymentMethod.ExternalCard)
        {
            return command.ExternalCardConfirmed
                ? Result<SalePaymentRequest>.Success(new SalePaymentRequest(
                    SalePaymentMethod.ExternalCard,
                    immediateAmount,
                    null,
                    0m,
                    true))
                : Result<SalePaymentRequest>.Failure(SalesApplicationErrors.CardUnconfirmed);
        }

        if (command.PaymentMethod != SalePaymentMethod.Cash
            || command.TenderedAmount is null
            || command.TenderedAmount.Value < immediateAmount
            || command.TenderedAmount.Value < 0m)
        {
            return Result<SalePaymentRequest>.Failure(SalesApplicationErrors.PaymentInvalid);
        }

        return Result<SalePaymentRequest>.Success(new SalePaymentRequest(
            SalePaymentMethod.Cash,
            immediateAmount,
            command.TenderedAmount,
            command.TenderedAmount.Value - immediateAmount,
            false));
    }

    private static Error? ValidateCredit(
        FinalizeSaleCommand command,
        Money total)
    {
        if (command.CreditAmount == 0m)
        {
            return null;
        }

        if (command.CreditAmount < 0m
            || command.CreditAmount > total.Amount
            || command.CustomerId is null
            || command.CustomerId.Value <= 0)
        {
            return SalesApplicationErrors.PaymentInvalid;
        }

        return null;
    }
}
