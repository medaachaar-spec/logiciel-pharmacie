using System.Collections.ObjectModel;
using System.Diagnostics;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Finalization.Queries;
using Pharmacy.Application.Sales.Models;

namespace Pharmacy.Desktop.Components.Pos;

public sealed class SalesReturnLineSelection
{
    public SalesReturnLineSelection(SaleReturnableLineView line)
    {
        Line = line ?? throw new ArgumentNullException(nameof(line));
        Quantity = line.ReturnableQuantity > 0 ? 1 : 0;
    }

    public SaleReturnableLineView Line { get; }

    public bool IsSelected { get; private set; }

    public int Quantity { get; private set; }

    public ReturnStockDestination StockDestination { get; private set; } =
        ReturnStockDestination.RestockApproved;

    public void SetSelected(bool selected)
    {
        IsSelected = selected && Line.ReturnableQuantity > 0;
        if (IsSelected && Quantity <= 0)
        {
            Quantity = 1;
        }
    }

    public void SetQuantity(int quantity) => Quantity = quantity;

    public void SetStockDestination(ReturnStockDestination destination) =>
        StockDestination = destination;
}

public sealed class SalesReturnCoordinator
{
    private readonly GetSaleReturnPreparationHandler preparationHandler;
    private readonly ReturnSaleHandler returnHandler;
    private readonly List<SalesReturnLineSelection> selections = new();
    private readonly ReadOnlyCollection<SalesReturnLineSelection> readOnlySelections;
    private int loadInProgress;
    private int submissionInProgress;
    private Guid pendingOperationId;

    public SalesReturnCoordinator(
        GetSaleReturnPreparationHandler preparationHandler,
        ReturnSaleHandler returnHandler)
    {
        this.preparationHandler = preparationHandler
            ?? throw new ArgumentNullException(nameof(preparationHandler));
        this.returnHandler = returnHandler ?? throw new ArgumentNullException(nameof(returnHandler));
        readOnlySelections = selections.AsReadOnly();
    }

    public SaleReturnPreparationView? Preparation { get; private set; }

    public IReadOnlyList<SalesReturnLineSelection> Selections => readOnlySelections;

    public SaleReturnView? LastResult { get; private set; }

    public ReturnRefundMethod RefundMethod { get; private set; }

    public string? LastErrorCode { get; private set; }

    public string? UserMessage { get; private set; }

    public bool IsLoading => Volatile.Read(ref loadInProgress) == 1;

    public bool IsSubmitting => Volatile.Read(ref submissionInProgress) == 1;

    public bool IsConfirmationVisible { get; private set; }

    public decimal EstimatedRefund => selections
        .Where(selection => selection.IsSelected)
        .Sum(selection => selection.Line.UnitPrice.Amount * selection.Quantity);

    public bool HasReturnableLines => selections.Any(selection => selection.Line.ReturnableQuantity > 0);

    public IReadOnlyList<ReturnRefundMethod> AvailableRefundMethods
    {
        get
        {
            if (Preparation is null)
            {
                return Array.Empty<ReturnRefundMethod>();
            }

            var methods = new List<ReturnRefundMethod>();
            if (Preparation.PaymentMethod == SalePaymentMethod.Cash)
            {
                methods.Add(ReturnRefundMethod.Cash);
            }
            else
            {
                methods.Add(ReturnRefundMethod.ExternalCardPending);
            }

            if (Preparation.CustomerId is not null && Preparation.CreditAmount.Amount > 0m)
            {
                methods.Add(ReturnRefundMethod.CreditReduction);
            }

            return methods.AsReadOnly();
        }
    }

    public Task<bool> LoadAsync(long saleId, CancellationToken cancellationToken = default) =>
        LoadCoreAsync(saleId, preserveFeedback: false, cancellationToken);

    public void SetSelected(long saleLineId, bool selected)
    {
        selections.Single(selection => selection.Line.SaleLineId == saleLineId).SetSelected(selected);
        ResetConfirmation();
    }

    public void SetQuantity(long saleLineId, int quantity)
    {
        selections.Single(selection => selection.Line.SaleLineId == saleLineId).SetQuantity(quantity);
        ResetConfirmation();
    }

    public void SetStockDestination(long saleLineId, ReturnStockDestination destination)
    {
        selections.Single(selection => selection.Line.SaleLineId == saleLineId)
            .SetStockDestination(destination);
        ResetConfirmation();
    }

    public void SetRefundMethod(ReturnRefundMethod method)
    {
        RefundMethod = method;
        ResetConfirmation();
    }

    public bool BeginConfirmation()
    {
        ClearFeedback();
        var selected = selections.Where(selection => selection.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            ApplyValidation("Sélectionnez au moins une ligne à retourner.");
            return false;
        }

        if (selected.Any(selection => selection.Quantity <= 0
            || selection.Quantity > selection.Line.ReturnableQuantity))
        {
            ApplyValidation("Chaque quantité doit être comprise entre 1 et le reliquat retournable.");
            return false;
        }

        if (!AvailableRefundMethods.Contains(RefundMethod))
        {
            ApplyValidation("Choisissez un mode de remboursement compatible avec la vente.");
            return false;
        }

        pendingOperationId = Guid.NewGuid();
        IsConfirmationVisible = true;
        return true;
    }

    public void CancelConfirmation() => ResetConfirmation();

    public async Task<bool> ConfirmAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfirmationVisible
            || Preparation is null
            || Interlocked.CompareExchange(ref submissionInProgress, 1, 0) != 0)
        {
            return false;
        }

        try
        {
            var request = new ReturnSaleRequest(
                pendingOperationId,
                Preparation.SaleId,
                selections
                    .Where(selection => selection.IsSelected)
                    .Select(selection => new ReturnSaleLineRequest(
                        selection.Line.SaleLineId,
                        selection.Quantity,
                        selection.StockDestination))
                    .ToArray(),
                RefundMethod);
            var result = await returnHandler.HandleAsync(request, cancellationToken);
            if (result.IsFailure)
            {
                ApplyFailure(result.Error.Code);
                return false;
            }

            LastResult = result.Value;
            IsConfirmationVisible = false;
            pendingOperationId = Guid.Empty;
            UserMessage = $"Retour {result.Value.Reference} enregistré — remboursement {DemoMoney.Format(result.Value.RefundAmount.Amount)}.";
            await LoadCoreAsync(Preparation.SaleId, preserveFeedback: true, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Trace.TraceError("Unexpected sale return failure: {0}.", exception.GetType().Name);
            LastErrorCode = "sales.return.unexpected";
            UserMessage = "Le retour n’a pas pu être confirmé. La vente originale reste inchangée.";
            return false;
        }
        finally
        {
            Volatile.Write(ref submissionInProgress, 0);
        }
    }

    public void ClearFeedback()
    {
        LastErrorCode = null;
        UserMessage = null;
    }

    private async Task<bool> LoadCoreAsync(
        long saleId,
        bool preserveFeedback,
        CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref loadInProgress, 1, 0) != 0)
        {
            return false;
        }

        if (!preserveFeedback)
        {
            ClearFeedback();
            LastResult = null;
        }

        try
        {
            var result = await preparationHandler.HandleAsync(
                new GetSaleReturnPreparationQuery(saleId),
                cancellationToken);
            if (result.IsFailure)
            {
                ApplyFailure(result.Error.Code);
                return false;
            }

            Preparation = result.Value;
            selections.Clear();
            selections.AddRange(result.Value.Lines.Select(line => new SalesReturnLineSelection(line)));
            RefundMethod = result.Value.PaymentMethod == SalePaymentMethod.Cash
                ? ReturnRefundMethod.Cash
                : ReturnRefundMethod.ExternalCardPending;
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Trace.TraceError("Unexpected sale return load failure: {0}.", exception.GetType().Name);
            LastErrorCode = "sales.return.unexpected";
            UserMessage = "Le détail de la vente n’a pas pu être chargé.";
            return false;
        }
        finally
        {
            Volatile.Write(ref loadInProgress, 0);
        }
    }

    private void ApplyValidation(string message)
    {
        LastErrorCode = "sales.return.validation";
        UserMessage = message;
    }

    private void ApplyFailure(string code)
    {
        LastErrorCode = code;
        UserMessage = code switch
        {
            "sales.return.quantity_exceeded" =>
                "La quantité retournable a changé. Rechargez la vente avant de confirmer.",
            "sales.return.concurrency_conflict" =>
                "Un autre retour a modifié cette vente. Rechargez son détail.",
            "sales.return.invalid" => "Le retour contient une sélection invalide.",
            "security.authorization.denied" => "Vous n’avez pas la permission d’effectuer ce retour.",
            "security.authorization.reauthentication_required" =>
                "Réauthentifiez-vous avant de confirmer ce retour sensible.",
            "security.authorization.authentication_required" => "Authentifiez-vous avant de continuer.",
            "sales.database.unavailable" =>
                "SQL Server est indisponible. Aucun retour, remboursement ou mouvement de stock n’a été créé.",
            "sales.operation.conflict" =>
                "Cette tentative de retour ne correspond plus à la demande initiale.",
            _ => "Le retour n’a pas pu être réalisé. La vente originale reste inchangée."
        };
    }

    private void ResetConfirmation()
    {
        IsConfirmationVisible = false;
        pendingOperationId = Guid.Empty;
    }
}
