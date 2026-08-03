using System.Collections.ObjectModel;
using System.Diagnostics;
using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Catalog.Queries;
using Pharmacy.Application.Sales.Drafts.Commands;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Drafts.Queries;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Desktop.Components.Pos;

public sealed class SalesPosSuspendedDraft
{
    public SalesPosSuspendedDraft(
        string reference,
        DateTimeOffset suspendedAtLocal,
        SaleDraftView draft)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentNullException.ThrowIfNull(draft);

        Reference = reference;
        SuspendedAtLocal = suspendedAtLocal;
        Lines = Array.AsReadOnly(draft.Lines.ToArray());
        LineCount = draft.LineCount;
        TotalQuantity = draft.TotalQuantity;
        Total = draft.Total;
    }

    public string Reference { get; }

    public DateTimeOffset SuspendedAtLocal { get; }

    public IReadOnlyList<SaleDraftLineView> Lines { get; }

    public int LineCount { get; }

    public long TotalQuantity { get; }

    public Money Total { get; }
}

public sealed class SalesPosCompletedSale
{
    public SalesPosCompletedSale(
        string reference,
        SaleDraftView draft,
        DemoPaymentMethod paymentMethod,
        decimal change)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentNullException.ThrowIfNull(draft);

        Reference = reference;
        DraftId = draft.Id;
        DraftVersion = draft.Version;
        Lines = Array.AsReadOnly(draft.Lines.ToArray());
        Total = draft.Total;
        PaymentMethod = paymentMethod;
        Change = change;
        ItemCount = draft.TotalQuantity;
        LineCount = draft.LineCount;
    }

    public string Reference { get; }

    public SaleDraftId DraftId { get; }

    public long DraftVersion { get; }

    public IReadOnlyList<SaleDraftLineView> Lines { get; }

    public Money Total { get; }

    public DemoPaymentMethod PaymentMethod { get; }

    public decimal Change { get; }

    public long ItemCount { get; }

    public int LineCount { get; }
}

public sealed class SalesPosCoordinator
{
    public const string ConcurrencyReloadMessage =
        "Le brouillon a été modifié. Son état le plus récent a été rechargé.";

    private const string InitialCatalogQuery = "DEMO";
    private readonly CreateSaleDraftHandler createDraftHandler;
    private readonly SearchSellableProductsHandler searchProductsHandler;
    private readonly GetSellableProductByCodeHandler getProductByCodeHandler;
    private readonly AddProductToSaleDraftHandler addProductHandler;
    private readonly ChangeSaleItemQuantityHandler changeQuantityHandler;
    private readonly RemoveSaleItemHandler removeItemHandler;
    private readonly ClearSaleDraftHandler clearDraftHandler;
    private readonly GetSaleDraftHandler getDraftHandler;
    private readonly DemoTicketPrintService ticketPrintService;
    private readonly List<SalesPosSuspendedDraft> suspendedDrafts = new();
    private readonly ReadOnlyCollection<SalesPosSuspendedDraft> readOnlySuspendedDrafts;
    private readonly SemaphoreSlim continuityGate = new(1, 1);
    private IReadOnlyList<SellableProductView> searchResults = Array.Empty<SellableProductView>();
    private int cartMutationInProgress;
    private int searchSequence;
    private int suspensionCounter;
    private int completedSaleCounter;
    private bool initialized;
    private bool completionInProgress;

    public SalesPosCoordinator(
        CreateSaleDraftHandler createDraftHandler,
        SearchSellableProductsHandler searchProductsHandler,
        GetSellableProductByCodeHandler getProductByCodeHandler,
        AddProductToSaleDraftHandler addProductHandler,
        ChangeSaleItemQuantityHandler changeQuantityHandler,
        RemoveSaleItemHandler removeItemHandler,
        ClearSaleDraftHandler clearDraftHandler,
        GetSaleDraftHandler getDraftHandler,
        DemoTicketPrintService ticketPrintService)
    {
        this.createDraftHandler = createDraftHandler ?? throw new ArgumentNullException(nameof(createDraftHandler));
        this.searchProductsHandler = searchProductsHandler ?? throw new ArgumentNullException(nameof(searchProductsHandler));
        this.getProductByCodeHandler = getProductByCodeHandler ?? throw new ArgumentNullException(nameof(getProductByCodeHandler));
        this.addProductHandler = addProductHandler ?? throw new ArgumentNullException(nameof(addProductHandler));
        this.changeQuantityHandler = changeQuantityHandler ?? throw new ArgumentNullException(nameof(changeQuantityHandler));
        this.removeItemHandler = removeItemHandler ?? throw new ArgumentNullException(nameof(removeItemHandler));
        this.clearDraftHandler = clearDraftHandler ?? throw new ArgumentNullException(nameof(clearDraftHandler));
        this.getDraftHandler = getDraftHandler ?? throw new ArgumentNullException(nameof(getDraftHandler));
        this.ticketPrintService = ticketPrintService ?? throw new ArgumentNullException(nameof(ticketPrintService));
        readOnlySuspendedDrafts = suspendedDrafts.AsReadOnly();
    }

    public SaleDraftView? CurrentDraft { get; private set; }

    public IReadOnlyList<SellableProductView> SearchResults => searchResults;

    public IReadOnlyList<SalesPosSuspendedDraft> SuspendedDrafts => readOnlySuspendedDrafts;

    public SalesPosAvailabilityIncident? AvailabilityIncident { get; private set; }

    public string? LastErrorCode { get; private set; }

    public string? UserMessage { get; private set; }

    public ProductId? BusyProductId { get; private set; }

    public bool IsInitializing { get; private set; }

    public bool IsSearching { get; private set; }

    public bool IsCartMutationInProgress => Volatile.Read(ref cartMutationInProgress) == 1;

    public bool IsContinuityActionInProgress => continuityGate.CurrentCount == 0;

    public DemoPosStep Step { get; private set; } = DemoPosStep.Shopping;

    public DemoPaymentMethod PaymentMethod { get; private set; }

    public DemoCardPaymentStatus CardPaymentStatus { get; private set; }

    public int CardVerificationCount { get; private set; }

    public decimal? ReceivedAmount { get; private set; }

    public SalesPosCompletedSale? Result { get; private set; }

    public DemoPrintStatus PrintStatus => ticketPrintService.Status;

    public int PrintAttemptCount => ticketPrintService.AttemptCount;

    public DateTimeOffset? LastPrintAttemptAtLocal => ticketPrintService.LastAttemptAtLocal;

    public bool ContinuedWithoutTicket => ticketPrintService.ContinuedWithoutTicket;

    public decimal Total => CurrentDraft?.Total.Amount ?? 0m;

    public long TotalQuantity => CurrentDraft?.TotalQuantity ?? 0;

    public decimal Change => PaymentMethod == DemoPaymentMethod.Cash
        && ReceivedAmount is not null
        && ReceivedAmount.Value >= Total
            ? ReceivedAmount.Value - Total
            : 0m;

    public bool IsCompletionInProgress => completionInProgress;

    public bool HasDraft => Step != DemoPosStep.Success && CurrentDraft?.IsEmpty == false;

    public bool CanSuspend => HasDraft
        && !completionInProgress
        && !IsCartMutationInProgress
        && !IsContinuityActionInProgress;

    public bool CanAbandon => HasDraft
        && !completionInProgress
        && !IsCartMutationInProgress
        && !IsContinuityActionInProgress;

    public bool CanComplete => Step == DemoPosStep.Payment
        && CurrentDraft?.IsEmpty == false
        && !completionInProgress
        && !IsCartMutationInProgress
        && !IsContinuityActionInProgress
        && IsPaymentComplete();

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return true;
        }

        if (IsInitializing)
        {
            return false;
        }

        IsInitializing = true;
        ClearFeedback();

        try
        {
            if (CurrentDraft is null && !await StartNewDraftCoreAsync(cancellationToken))
            {
                return false;
            }

            if (!await SearchCoreAsync(InitialCatalogQuery, cancellationToken))
            {
                return false;
            }

            initialized = true;
            Trace.TraceInformation("Sales POS initialized with an in-memory Application draft.");
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HandleUnexpectedFailure("initialization", exception);
        }
        finally
        {
            IsInitializing = false;
        }
    }

    public async Task<IReadOnlyList<SellableProductView>> SearchAsync(
        string? query,
        CancellationToken cancellationToken = default)
    {
        var sequence = Interlocked.Increment(ref searchSequence);
        IsSearching = true;
        LastErrorCode = null;

        try
        {
            var normalizedQuery = string.IsNullOrWhiteSpace(query)
                ? InitialCatalogQuery
                : query.Trim();
            var result = await searchProductsHandler.HandleAsync(
                new ProductSearchQuery(normalizedQuery),
                cancellationToken);

            if (sequence != Volatile.Read(ref searchSequence))
            {
                return searchResults;
            }

            if (result.IsFailure)
            {
                ApplyFailure(result.Error);
                return searchResults;
            }

            searchResults = Array.AsReadOnly(result.Value.ToArray());
            return searchResults;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            HandleUnexpectedFailure("search", exception);
            return searchResults;
        }
        finally
        {
            if (sequence == Volatile.Read(ref searchSequence))
            {
                IsSearching = false;
            }
        }
    }

    public Task<bool> AddProductAsync(
        ProductId productId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productId);
        return AddProductCoreWithGateAsync(
            SellableProductLookup.ByProductId(productId),
            productId,
            cancellationToken);
    }

    public async Task<bool> AddExactCodeAsync(
        string? exactCode,
        CancellationToken cancellationToken = default)
    {
        if (!TryBeginCartMutation(null))
        {
            return false;
        }

        ClearFeedback();

        try
        {
            var product = await getProductByCodeHandler.HandleAsync(
                new GetSellableProductByCodeQuery(exactCode ?? string.Empty),
                cancellationToken);

            if (product.IsFailure)
            {
                return await HandleFailureAsync(product.Error, null, cancellationToken);
            }

            BusyProductId = product.Value.ProductId;
            return await AddProductCoreAsync(
                SellableProductLookup.ByProductId(product.Value.ProductId),
                product.Value.ProductId,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HandleUnexpectedFailure("exact-code-add", exception);
        }
        finally
        {
            EndCartMutation();
        }
    }

    public async Task<bool> ChangeQuantityAsync(
        ProductId productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productId);

        var quantityResult = SaleQuantity.Create(quantity);

        if (quantityResult.IsFailure)
        {
            ApplyFailure(quantityResult.Error);
            return false;
        }

        if (!TryBeginCartMutation(productId))
        {
            return false;
        }

        ClearFeedback();

        try
        {
            if (CurrentDraft is null)
            {
                return ApplyMissingDraft();
            }

            var result = await changeQuantityHandler.HandleAsync(
                new ChangeSaleItemQuantityCommand(
                    CurrentDraft.Id,
                    productId,
                    quantityResult.Value,
                    CurrentDraft.Version),
                cancellationToken);

            return result.IsFailure
                ? await HandleFailureAsync(
                    result.Error,
                    ProductName(productId),
                    cancellationToken,
                    quantity)
                : ApplyDraftMutation(result.Value, "Quantité mise à jour.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HandleUnexpectedFailure("quantity-change", exception);
        }
        finally
        {
            EndCartMutation();
        }
    }

    public async Task<bool> RemoveItemAsync(
        ProductId productId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productId);

        if (!TryBeginCartMutation(productId))
        {
            return false;
        }

        ClearFeedback();

        try
        {
            if (CurrentDraft is null)
            {
                return ApplyMissingDraft();
            }

            var result = await removeItemHandler.HandleAsync(
                new RemoveSaleItemCommand(CurrentDraft.Id, productId, CurrentDraft.Version),
                cancellationToken);

            return result.IsFailure
                ? await HandleFailureAsync(result.Error, ProductName(productId), cancellationToken)
                : ApplyDraftMutation(result.Value, "Ligne retirée du panier.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HandleUnexpectedFailure("line-removal", exception);
        }
        finally
        {
            EndCartMutation();
        }
    }

    public async Task<bool> ClearAsync(CancellationToken cancellationToken = default)
    {
        if (!TryBeginCartMutation(null))
        {
            return false;
        }

        ClearFeedback();

        try
        {
            if (CurrentDraft is null)
            {
                return ApplyMissingDraft();
            }

            var result = await clearDraftHandler.HandleAsync(
                new ClearSaleDraftCommand(CurrentDraft.Id, CurrentDraft.Version),
                cancellationToken);

            return result.IsFailure
                ? await HandleFailureAsync(result.Error, null, cancellationToken)
                : ApplyDraftMutation(result.Value, "Panier vidé. Le paiement incomplet a été réinitialisé.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HandleUnexpectedFailure("draft-clear", exception);
        }
        finally
        {
            EndCartMutation();
        }
    }

    public async Task<bool> ReloadDraftAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await ReloadDraftCoreAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HandleUnexpectedFailure("draft-reload", exception);
        }
    }

    public async Task<bool> SuspendCurrentDraftAsync(CancellationToken cancellationToken = default)
    {
        if (!await continuityGate.WaitAsync(0, cancellationToken))
        {
            ApplyBusyMessage();
            return false;
        }

        ClearFeedback();

        try
        {
            if (CurrentDraft is null || CurrentDraft.IsEmpty)
            {
                UserMessage = "Un panier vide ne peut pas être suspendu.";
                return false;
            }

            var snapshot = CurrentDraft;
            var nextDraft = await createDraftHandler.HandleAsync(cancellationToken);

            if (nextDraft.IsFailure)
            {
                return await HandleFailureAsync(nextDraft.Error, null, cancellationToken);
            }

            suspensionCounter++;
            var suspension = new SalesPosSuspendedDraft(
                $"DEMO-HOLD-{suspensionCounter:0000}",
                DateTimeOffset.Now,
                snapshot);
            suspendedDrafts.Insert(0, suspension);
            CurrentDraft = nextDraft.Value;
            ResetDraftPresentationState();
            UserMessage = $"Vente suspendue sous {suspension.Reference}. Temporaire en mémoire : elle disparaîtra à la fermeture.";
            Trace.TraceInformation("Sales POS draft suspended in desktop memory.");
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HandleUnexpectedFailure("draft-suspension", exception);
        }
        finally
        {
            continuityGate.Release();
        }
    }

    public async Task<bool> ResumeSuspendedDraftAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        if (!await continuityGate.WaitAsync(0, cancellationToken))
        {
            ApplyBusyMessage();
            return false;
        }

        ClearFeedback();

        try
        {
            if (CurrentDraft?.IsEmpty == false)
            {
                UserMessage = "Le panier courant doit être suspendu ou abandonné avant la reprise.";
                return false;
            }

            var suspension = suspendedDrafts.FirstOrDefault(candidate =>
                string.Equals(candidate.Reference, reference, StringComparison.Ordinal));

            if (suspension is null)
            {
                UserMessage = "La vente suspendue n’est plus disponible.";
                return false;
            }

            var created = await createDraftHandler.HandleAsync(cancellationToken);

            if (created.IsFailure)
            {
                return await HandleFailureAsync(created.Error, null, cancellationToken);
            }

            var restored = created.Value;

            foreach (var line in suspension.Lines)
            {
                var added = await addProductHandler.HandleAsync(
                    new AddProductToSaleDraftCommand(
                        restored.Id,
                        SellableProductLookup.ByProductId(line.ProductId),
                        line.Quantity,
                        restored.Version),
                    cancellationToken);

                if (added.IsFailure)
                {
                    Trace.TraceWarning("Sales POS suspension restoration failed; the desktop snapshot was retained.");
                    return await HandleFailureAsync(
                        added.Error,
                        line.DisplayName,
                        cancellationToken,
                        line.Quantity.Value);
                }

                restored = added.Value;
            }

            CurrentDraft = restored;
            suspendedDrafts.Remove(suspension);
            ResetDraftPresentationState();
            UserMessage = $"Vente suspendue {reference} reprise. Le paiement doit être choisi à nouveau.";
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HandleUnexpectedFailure("draft-restoration", exception);
        }
        finally
        {
            continuityGate.Release();
        }
    }

    public async Task<bool> AbandonCurrentDraftAsync(CancellationToken cancellationToken = default)
    {
        var success = await ClearAsync(cancellationToken);

        if (success)
        {
            UserMessage = "Brouillon abandonné. Aucune vente ni aucun paiement n’a été créé.";
        }

        return success;
    }

    public async Task<bool> StartNewDraftAsync(CancellationToken cancellationToken = default)
    {
        if (!await continuityGate.WaitAsync(0, cancellationToken))
        {
            ApplyBusyMessage();
            return false;
        }

        ClearFeedback();

        try
        {
            return await StartNewDraftCoreAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HandleUnexpectedFailure("new-draft", exception);
        }
        finally
        {
            continuityGate.Release();
        }
    }

    public bool DeleteSuspended(string reference)
    {
        if (IsContinuityActionInProgress)
        {
            return false;
        }

        var suspension = suspendedDrafts.FirstOrDefault(candidate =>
            string.Equals(candidate.Reference, reference, StringComparison.Ordinal));

        return suspension is not null && suspendedDrafts.Remove(suspension);
    }

    public bool BeginPayment()
    {
        if (CurrentDraft?.IsEmpty != false || IsContinuityActionInProgress || IsCartMutationInProgress)
        {
            return false;
        }

        Step = DemoPosStep.Payment;
        ResetPaymentState();
        return true;
    }

    public void ReturnToShopping()
    {
        if (Step != DemoPosStep.Success && !IsContinuityActionInProgress)
        {
            Step = DemoPosStep.Shopping;
            ResetPaymentState();
            completionInProgress = false;
        }
    }

    public void SelectPaymentMethod(DemoPaymentMethod method)
    {
        if (Step != DemoPosStep.Payment || method == DemoPaymentMethod.None)
        {
            return;
        }

        PaymentMethod = method;
        ReceivedAmount = null;
        CardPaymentStatus = DemoCardPaymentStatus.NotStarted;
        CardVerificationCount = 0;
    }

    public bool SetReceivedAmount(decimal? amount)
    {
        if (PaymentMethod != DemoPaymentMethod.Cash || amount < 0m)
        {
            ReceivedAmount = null;
            return false;
        }

        ReceivedAmount = amount;
        return true;
    }

    public bool StartExternalCardVerification()
    {
        if (PaymentMethod != DemoPaymentMethod.ExternalCard
            || CardPaymentStatus != DemoCardPaymentStatus.NotStarted)
        {
            return false;
        }

        CardPaymentStatus = DemoCardPaymentStatus.AwaitingConfirmation;
        CardVerificationCount++;
        return true;
    }

    public bool ReportExternalCardConfirmed()
    {
        return TransitionCardStatus(DemoCardPaymentStatus.AwaitingConfirmation, DemoCardPaymentStatus.Confirmed);
    }

    public bool ReportExternalCardNotCompleted()
    {
        return TransitionCardStatus(DemoCardPaymentStatus.AwaitingConfirmation, DemoCardPaymentStatus.NotCompleted);
    }

    public bool ReportExternalCardAmbiguous()
    {
        return TransitionCardStatus(DemoCardPaymentStatus.AwaitingConfirmation, DemoCardPaymentStatus.Ambiguous);
    }

    public bool ResolveAmbiguousCardAsConfirmed()
    {
        return TransitionCardStatus(DemoCardPaymentStatus.Ambiguous, DemoCardPaymentStatus.Confirmed);
    }

    public bool ResolveAmbiguousCardAsNotCompleted()
    {
        return TransitionCardStatus(DemoCardPaymentStatus.Ambiguous, DemoCardPaymentStatus.NotCompleted);
    }

    public bool ReturnToPaymentChoice()
    {
        if (Step != DemoPosStep.Payment || completionInProgress)
        {
            return false;
        }

        ResetPaymentState();
        return true;
    }

    public bool TryBeginCompletion()
    {
        if (!CanComplete)
        {
            return false;
        }

        completionInProgress = true;
        return true;
    }

    public SalesPosCompletedSale CompleteSale()
    {
        if (!completionInProgress || !IsPaymentComplete() || CurrentDraft is null)
        {
            throw new InvalidOperationException("La vente simulée ne peut pas être terminée dans cet état.");
        }

        completedSaleCounter++;
        Result = new SalesPosCompletedSale(
            $"DEMO-SALE-{completedSaleCounter:0000}",
            CurrentDraft,
            PaymentMethod,
            Change);
        Step = DemoPosStep.Success;
        completionInProgress = false;
        ticketPrintService.PrepareForSale(Result.Reference);
        return Result;
    }

    public void CancelCompletion()
    {
        completionInProgress = false;
    }

    public bool TryBeginTicketPrint()
    {
        return Result is not null && ticketPrintService.TryBegin(Result.Reference);
    }

    public bool CompleteTicketPrint(DemoPrintSimulationOutcome outcome)
    {
        return Result is not null && ticketPrintService.Complete(Result.Reference, outcome);
    }

    public bool ContinueWithoutTicket()
    {
        return Result is not null && ticketPrintService.ContinueWithoutTicket(Result.Reference);
    }

    public int? KnownAvailability(ProductId productId)
    {
        ArgumentNullException.ThrowIfNull(productId);
        return searchResults.FirstOrDefault(product => product.ProductId == productId)?.AvailableQuantity;
    }

    public void ClearFeedback()
    {
        LastErrorCode = null;
        UserMessage = null;
        AvailabilityIncident = null;
    }

    public void ReportUnexpectedFailure(string operation, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(exception);
        HandleUnexpectedFailure(operation, exception);
    }

    private async Task<bool> AddProductCoreWithGateAsync(
        SellableProductLookup lookup,
        ProductId productId,
        CancellationToken cancellationToken)
    {
        if (!TryBeginCartMutation(productId))
        {
            return false;
        }

        ClearFeedback();

        try
        {
            return await AddProductCoreAsync(lookup, productId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HandleUnexpectedFailure("product-add", exception);
        }
        finally
        {
            EndCartMutation();
        }
    }

    private async Task<bool> AddProductCoreAsync(
        SellableProductLookup lookup,
        ProductId productId,
        CancellationToken cancellationToken)
    {
        if (CurrentDraft is null)
        {
            return ApplyMissingDraft();
        }

        var wasPresent = CurrentDraft.Lines.Any(line => line.ProductId == productId);
        var requestedQuantity = CurrentDraft.Lines
            .FirstOrDefault(line => line.ProductId == productId)?.Quantity.Value + 1 ?? 1;
        var result = await addProductHandler.HandleAsync(
            new AddProductToSaleDraftCommand(
                CurrentDraft.Id,
                lookup,
                SaleQuantity.One,
                CurrentDraft.Version),
            cancellationToken);

        return result.IsFailure
            ? await HandleFailureAsync(
                result.Error,
                ProductName(productId),
                cancellationToken,
                requestedQuantity)
            : ApplyDraftMutation(
                result.Value,
                wasPresent ? "Article déjà présent : quantité augmentée." : "Article ajouté au panier.");
    }

    private async Task<bool> SearchCoreAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var result = await searchProductsHandler.HandleAsync(
            new ProductSearchQuery(query),
            cancellationToken);

        if (result.IsFailure)
        {
            ApplyFailure(result.Error);
            return false;
        }

        searchResults = Array.AsReadOnly(result.Value.ToArray());
        return true;
    }

    private async Task<bool> StartNewDraftCoreAsync(CancellationToken cancellationToken)
    {
        var created = await createDraftHandler.HandleAsync(cancellationToken);

        if (created.IsFailure)
        {
            ApplyFailure(created.Error);
            return false;
        }

        CurrentDraft = created.Value;
        ResetDraftPresentationState();
        Result = null;
        ticketPrintService.Reset();
        Trace.TraceInformation("Sales POS Application draft created.");
        return true;
    }

    private async Task<bool> ReloadDraftCoreAsync(CancellationToken cancellationToken)
    {
        if (CurrentDraft is null)
        {
            return ApplyMissingDraft();
        }

        var result = await getDraftHandler.HandleAsync(
            new GetSaleDraftQuery(CurrentDraft.Id),
            cancellationToken);

        if (result.IsFailure)
        {
            ApplyFailure(result.Error);
            return false;
        }

        CurrentDraft = result.Value;
        return true;
    }

    private async Task<bool> HandleFailureAsync(
        Error error,
        string? productName,
        CancellationToken cancellationToken,
        int? requestedQuantity = null)
    {
        if (string.Equals(error.Code, "sales.draft.concurrency_conflict", StringComparison.Ordinal))
        {
            Trace.TraceWarning("Sales POS optimistic concurrency conflict; reloading the draft.");
            var reloaded = await ReloadDraftCoreAsync(cancellationToken);
            LastErrorCode = error.Code;
            UserMessage = reloaded
                ? ConcurrencyReloadMessage
                : "Le brouillon a été modifié, mais son état récent n’a pas pu être rechargé.";
            return false;
        }

        ApplyFailure(error, productName, requestedQuantity);
        return false;
    }

    private void ApplyFailure(
        Error error,
        string? productName = null,
        int? requestedQuantity = null)
    {
        LastErrorCode = error.Code;

        if (string.Equals(error.Code, "sales.stock.insufficient_availability", StringComparison.Ordinal))
        {
            var currentLine = CurrentDraft?.Lines.FirstOrDefault(line =>
                string.Equals(line.DisplayName, productName, StringComparison.Ordinal));
            var product = searchResults.FirstOrDefault(candidate =>
                string.Equals(candidate.DisplayName, productName, StringComparison.Ordinal));
            var requested = requestedQuantity ?? currentLine?.Quantity.Value + 1 ?? 1;
            var available = product?.AvailableQuantity ?? currentLine?.Quantity.Value ?? 0;
            var availabilityPresentation = SalesPosErrorMapper.Map(
                error,
                new SalesPosErrorContext(productName, requested, available));
            AvailabilityIncident = availabilityPresentation.AvailabilityIncident;
            UserMessage = availabilityPresentation.Message;
        }
        else
        {
            UserMessage = SalesPosErrorMapper.Map(error).Message;
        }
    }

    private bool ApplyDraftMutation(SaleDraftView draft, string message)
    {
        CurrentDraft = draft;

        if (draft.IsEmpty)
        {
            Step = DemoPosStep.Shopping;
        }

        ResetPaymentState();
        completionInProgress = false;
        UserMessage = message;
        return true;
    }

    private bool ApplyMissingDraft()
    {
        LastErrorCode = "sales.draft.not_found";
        UserMessage = "Le brouillon courant est introuvable. Démarrez une nouvelle vente.";
        return false;
    }

    private bool TryBeginCartMutation(ProductId? productId)
    {
        if (Interlocked.CompareExchange(ref cartMutationInProgress, 1, 0) != 0)
        {
            ApplyBusyMessage();
            return false;
        }

        BusyProductId = productId;
        return true;
    }

    private void EndCartMutation()
    {
        BusyProductId = null;
        Volatile.Write(ref cartMutationInProgress, 0);
    }

    private void ApplyBusyMessage()
    {
        LastErrorCode = "sales.pos.busy";
        UserMessage = "Une action est déjà en cours. Attendez sa fin avant de recommencer.";
    }

    private bool HandleUnexpectedFailure(string operation, Exception exception)
    {
        Trace.TraceError(
            "Unexpected Sales POS failure during {0}: {1}.",
            operation,
            exception.GetType().Name);
        LastErrorCode = "sales.pos.unexpected";
        UserMessage = "Une erreur inattendue est survenue. Le panier affiché a été conservé.";
        return false;
    }

    private string? ProductName(ProductId productId)
    {
        return CurrentDraft?.Lines.FirstOrDefault(line => line.ProductId == productId)?.DisplayName
            ?? searchResults.FirstOrDefault(product => product.ProductId == productId)?.DisplayName;
    }

    private bool TransitionCardStatus(DemoCardPaymentStatus from, DemoCardPaymentStatus to)
    {
        if (PaymentMethod != DemoPaymentMethod.ExternalCard || CardPaymentStatus != from)
        {
            return false;
        }

        CardPaymentStatus = to;
        return true;
    }

    private bool IsPaymentComplete()
    {
        return PaymentMethod switch
        {
            DemoPaymentMethod.Cash => ReceivedAmount is not null && ReceivedAmount.Value >= Total,
            DemoPaymentMethod.ExternalCard => CardPaymentStatus == DemoCardPaymentStatus.Confirmed,
            _ => false
        };
    }

    private void ResetDraftPresentationState()
    {
        Step = DemoPosStep.Shopping;
        ResetPaymentState();
        completionInProgress = false;
    }

    private void ResetPaymentState()
    {
        PaymentMethod = DemoPaymentMethod.None;
        ReceivedAmount = null;
        CardPaymentStatus = DemoCardPaymentStatus.NotStarted;
        CardVerificationCount = 0;
    }
}
