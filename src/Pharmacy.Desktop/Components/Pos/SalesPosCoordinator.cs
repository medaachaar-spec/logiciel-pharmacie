using System.Collections.ObjectModel;
using System.Diagnostics;
using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Catalog.Queries;
using Pharmacy.Application.Sales.Drafts.Commands;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Drafts.Queries;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Finalization.Queries;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Application.Sales.Suspensions;
using Pharmacy.Application.Configuration;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Desktop.Components.Pos;

public sealed class SalesPosSuspendedDraft
{
    public SalesPosSuspendedDraft(
        SuspendedSaleSummaryView suspension)
    {
        ArgumentNullException.ThrowIfNull(suspension);
        SuspendedSaleId = suspension.SuspendedSaleId;
        OperationId = suspension.OperationId;
        CustomerId = suspension.CustomerId;
        CustomerName = suspension.CustomerName;
        Reference = suspension.Reference;
        SuspendedAtLocal = new DateTimeOffset(
            DateTime.SpecifyKind(suspension.SuspendedAtUtc, DateTimeKind.Utc)).ToLocalTime();
        Lines = Array.AsReadOnly(suspension.Lines.Select(line => new SaleDraftLineView(
            line.ProductId,
            line.ProductReference,
            line.ProductName,
            line.Presentation,
            line.UnitPrice,
            line.Quantity,
            line.LineTotal)).ToArray());
        LineCount = suspension.LineCount;
        TotalQuantity = suspension.Lines.Sum(line => (long)line.Quantity.Value);
        Total = suspension.Total;
        RowVersion = suspension.RowVersion.ToArray();
    }

    public long SuspendedSaleId { get; }

    public Guid OperationId { get; }

    public long? CustomerId { get; }

    public string? CustomerName { get; }

    public string Reference { get; }

    public DateTimeOffset SuspendedAtLocal { get; }

    public IReadOnlyList<SaleDraftLineView> Lines { get; }

    public int LineCount { get; }

    public long TotalQuantity { get; }

    public Money Total { get; }

    public byte[] RowVersion { get; }
}

public sealed class SalesPosCompletedSale
{
    public SalesPosCompletedSale(FinalizedSaleView sale)
    {
        ArgumentNullException.ThrowIfNull(sale);
        SaleId = sale.SaleId;
        OperationId = sale.OperationId;
        Reference = sale.Reference;
        CompletedAtUtc = sale.CompletedAtUtc;
        Lines = sale.Lines;
        Total = sale.Total;
        PaymentMethod = sale.Payment.Method == SalePaymentMethod.Cash
            ? DemoPaymentMethod.Cash
            : DemoPaymentMethod.ExternalCard;
        Change = sale.Payment.ChangeGiven;
        ItemCount = sale.TotalQuantity;
        LineCount = sale.LineCount;
    }

    public long SaleId { get; }

    public Guid OperationId { get; }

    public string Reference { get; }

    public DateTime CompletedAtUtc { get; }

    public IReadOnlyList<FinalizedSaleLineView> Lines { get; }

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
    private readonly RestoreSuspendedSaleDraftHandler restoreSuspendedDraftHandler;
    private readonly FinalizeSaleHandler finalizeSaleHandler;
    private readonly GetRecentSalesHandler getRecentSalesHandler;
    private readonly DemoTicketPrintService ticketPrintService;
    private readonly SuspendedSaleWorkflow suspendedSaleWorkflow;
    private readonly IReceiptPrintingPolicy? receiptPrintingPolicy;
    private readonly List<SalesPosSuspendedDraft> suspendedDrafts = new();
    private readonly ReadOnlyCollection<SalesPosSuspendedDraft> readOnlySuspendedDrafts;
    private readonly SemaphoreSlim continuityGate = new(1, 1);
    private IReadOnlyList<SellableProductView> searchResults = Array.Empty<SellableProductView>();
    private IReadOnlyList<FinalizedSaleSummaryView> recentSales = Array.Empty<FinalizedSaleSummaryView>();
    private int cartMutationInProgress;
    private int searchSequence;
    private bool initialized;
    private bool completionInProgress;
    private bool hasPreparedNextDraft;
    private Guid pendingOperationId;
    private Guid pendingSuspensionOperationId;
    private SalesPosSuspendedDraft? activeSuspension;

    public SalesPosCoordinator(
        CreateSaleDraftHandler createDraftHandler,
        SearchSellableProductsHandler searchProductsHandler,
        GetSellableProductByCodeHandler getProductByCodeHandler,
        AddProductToSaleDraftHandler addProductHandler,
        ChangeSaleItemQuantityHandler changeQuantityHandler,
        RemoveSaleItemHandler removeItemHandler,
        ClearSaleDraftHandler clearDraftHandler,
        GetSaleDraftHandler getDraftHandler,
        RestoreSuspendedSaleDraftHandler restoreSuspendedDraftHandler,
        FinalizeSaleHandler finalizeSaleHandler,
        GetRecentSalesHandler getRecentSalesHandler,
        DemoTicketPrintService ticketPrintService,
        SuspendedSaleWorkflow suspendedSaleWorkflow,
        IReceiptPrintingPolicy? receiptPrintingPolicy = null)
    {
        this.createDraftHandler = createDraftHandler ?? throw new ArgumentNullException(nameof(createDraftHandler));
        this.searchProductsHandler = searchProductsHandler ?? throw new ArgumentNullException(nameof(searchProductsHandler));
        this.getProductByCodeHandler = getProductByCodeHandler ?? throw new ArgumentNullException(nameof(getProductByCodeHandler));
        this.addProductHandler = addProductHandler ?? throw new ArgumentNullException(nameof(addProductHandler));
        this.changeQuantityHandler = changeQuantityHandler ?? throw new ArgumentNullException(nameof(changeQuantityHandler));
        this.removeItemHandler = removeItemHandler ?? throw new ArgumentNullException(nameof(removeItemHandler));
        this.clearDraftHandler = clearDraftHandler ?? throw new ArgumentNullException(nameof(clearDraftHandler));
        this.getDraftHandler = getDraftHandler ?? throw new ArgumentNullException(nameof(getDraftHandler));
        this.restoreSuspendedDraftHandler = restoreSuspendedDraftHandler
            ?? throw new ArgumentNullException(nameof(restoreSuspendedDraftHandler));
        this.finalizeSaleHandler = finalizeSaleHandler ?? throw new ArgumentNullException(nameof(finalizeSaleHandler));
        this.getRecentSalesHandler = getRecentSalesHandler ?? throw new ArgumentNullException(nameof(getRecentSalesHandler));
        this.ticketPrintService = ticketPrintService ?? throw new ArgumentNullException(nameof(ticketPrintService));
        this.suspendedSaleWorkflow = suspendedSaleWorkflow
            ?? throw new ArgumentNullException(nameof(suspendedSaleWorkflow));
        this.receiptPrintingPolicy = receiptPrintingPolicy;
        readOnlySuspendedDrafts = suspendedDrafts.AsReadOnly();
    }

    public SaleDraftView? CurrentDraft { get; private set; }

    public IReadOnlyList<SellableProductView> SearchResults => searchResults;

    public IReadOnlyList<SalesPosSuspendedDraft> SuspendedDrafts => readOnlySuspendedDrafts;

    public IReadOnlyList<FinalizedSaleSummaryView> RecentSales => recentSales;

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

    public long? CreditCustomerId { get; private set; }

    public decimal CreditAmount { get; private set; }

    public SalesPosCompletedSale? Result { get; private set; }

    public DemoPrintStatus PrintStatus => ticketPrintService.Status;

    public int PrintAttemptCount => ticketPrintService.AttemptCount;

    public DateTimeOffset? LastPrintAttemptAtLocal => ticketPrintService.LastAttemptAtLocal;

    public bool ContinuedWithoutTicket => ticketPrintService.ContinuedWithoutTicket;

    public bool ReceiptPrintingEnabled =>
        receiptPrintingPolicy?.ReceiptPrintingEnabled ?? true;

    public decimal Total => CurrentDraft?.Total.Amount ?? 0m;

    public long TotalQuantity => CurrentDraft?.TotalQuantity ?? 0;

    public decimal Change => PaymentMethod == DemoPaymentMethod.Cash
        && ReceivedAmount is not null
        && ReceivedAmount.Value >= ImmediatePaymentDue
            ? ReceivedAmount.Value - ImmediatePaymentDue
            : 0m;

    public decimal ImmediatePaymentDue => Math.Max(0m, Total - CreditAmount);

    public bool IsCompletionInProgress => completionInProgress;

    public bool HasDraft => Step != DemoPosStep.Success && CurrentDraft?.IsEmpty == false;

    public bool CanSuspend => HasDraft
        && activeSuspension is null
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

            if (!await LoadRecentSalesCoreAsync(cancellationToken))
            {
                return false;
            }

            if (!await LoadSuspendedSalesCoreAsync(cancellationToken))
            {
                return false;
            }

            initialized = true;
            Trace.TraceInformation("Sales POS initialized with SQL catalog, inventory, sales history and suspended sales.");
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

            if (activeSuspension is not null)
            {
                UserMessage = "Cette vente est déjà persistée comme suspension. Finalisez-la ou abandonnez-la avant d’en reprendre une autre.";
                return false;
            }

            var snapshot = CurrentDraft;
            var nextDraft = await createDraftHandler.HandleAsync(cancellationToken);
            if (nextDraft.IsFailure)
            {
                return await HandleFailureAsync(nextDraft.Error, null, cancellationToken);
            }

            pendingSuspensionOperationId = pendingSuspensionOperationId == Guid.Empty
                ? Guid.NewGuid()
                : pendingSuspensionOperationId;
            var persisted = await suspendedSaleWorkflow.SuspendAsync(
                new SuspendSaleRequest(
                    pendingSuspensionOperationId,
                    CreditCustomerId,
                    snapshot.Lines.Select(line => new SuspendSaleLineRequest(
                        line.ProductId.Value,
                        line.Reference.Value,
                        line.DisplayName,
                        line.Presentation,
                        line.UnitPrice.Amount,
                        line.Quantity.Value)).ToArray()),
                cancellationToken);
            if (persisted.IsFailure)
            {
                ApplyFailure(persisted.Error);
                return false;
            }

            var suspension = new SalesPosSuspendedDraft(persisted.Value);
            suspendedDrafts.RemoveAll(candidate => candidate.SuspendedSaleId == suspension.SuspendedSaleId);
            suspendedDrafts.Insert(0, suspension);
            CurrentDraft = nextDraft.Value;
            activeSuspension = null;
            pendingSuspensionOperationId = Guid.Empty;
            ResetDraftPresentationState();
            UserMessage = $"Vente suspendue sous {suspension.Reference}. Elle restera disponible après redémarrage.";
            Trace.TraceInformation("Sales POS draft suspended in SQL persistence.");
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

            if (!await LoadSuspendedSalesCoreAsync(cancellationToken))
            {
                return false;
            }

            var suspension = suspendedDrafts.FirstOrDefault(candidate =>
                string.Equals(candidate.Reference, reference, StringComparison.Ordinal));

            if (suspension is null)
            {
                UserMessage = "La vente suspendue n’est plus disponible.";
                return false;
            }

            var restoredResult = await restoreSuspendedDraftHandler.HandleAsync(
                ToApplicationView(suspension),
                cancellationToken);
            if (restoredResult.IsFailure)
            {
                Trace.TraceWarning("Sales POS suspension restoration failed; the SQL suspension was retained.");
                return await HandleFailureAsync(restoredResult.Error, null, cancellationToken);
            }

            CurrentDraft = restoredResult.Value;
            activeSuspension = suspension;
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

        if (!success)
        {
            return false;
        }

        if (activeSuspension is not null)
        {
            var abandoned = await suspendedSaleWorkflow.AbandonAsync(
                Guid.NewGuid(),
                activeSuspension.SuspendedSaleId,
                activeSuspension.RowVersion,
                cancellationToken);
            if (abandoned.IsFailure)
            {
                ApplyFailure(abandoned.Error);
                await LoadSuspendedSalesCoreAsync(cancellationToken, preserveFeedback: true);
                return false;
            }

            activeSuspension = null;
            await LoadSuspendedSalesCoreAsync(cancellationToken, preserveFeedback: true);
        }

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

    public async Task<bool> AbandonSuspendedDraftAsync(
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
            if (!await LoadSuspendedSalesCoreAsync(cancellationToken))
            {
                return false;
            }

            var suspension = suspendedDrafts.FirstOrDefault(candidate =>
                string.Equals(candidate.Reference, reference, StringComparison.Ordinal));
            if (suspension is null)
            {
                UserMessage = "La vente suspendue n’est plus disponible.";
                return false;
            }

            var result = await suspendedSaleWorkflow.AbandonAsync(
                Guid.NewGuid(),
                suspension.SuspendedSaleId,
                suspension.RowVersion,
                cancellationToken);
            if (result.IsFailure)
            {
                ApplyFailure(result.Error);
                await LoadSuspendedSalesCoreAsync(cancellationToken, preserveFeedback: true);
                return false;
            }

            suspendedDrafts.Remove(suspension);
            UserMessage = $"Vente suspendue {reference} abandonnée. Aucune vente, aucun paiement et aucun mouvement de stock n’a été créé.";
            return true;
        }
        finally
        {
            continuityGate.Release();
        }
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
        pendingOperationId = Guid.Empty;
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

    public bool SetCredit(long? customerId, decimal amount)
    {
        if (amount < 0m
            || amount > Total
            || amount > 0m && (customerId is null || customerId.Value <= 0))
        {
            CreditCustomerId = null;
            CreditAmount = 0m;
            return false;
        }

        CreditCustomerId = amount == 0m ? null : customerId;
        CreditAmount = amount;
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

    public async Task<bool> FinalizeSaleAsync(CancellationToken cancellationToken = default)
    {
        if (!completionInProgress || !IsPaymentComplete() || CurrentDraft is null)
        {
            return false;
        }

        pendingOperationId = pendingOperationId == Guid.Empty
            ? Guid.NewGuid()
            : pendingOperationId;

        try
        {
            var finalized = await finalizeSaleHandler.HandleAsync(
                new FinalizeSaleCommand(
                    pendingOperationId,
                    CurrentDraft.Id,
                    CurrentDraft.Version,
                    PaymentMethod == DemoPaymentMethod.Cash
                        ? SalePaymentMethod.Cash
                        : SalePaymentMethod.ExternalCard,
                    ReceivedAmount,
                    CardPaymentStatus == DemoCardPaymentStatus.Confirmed,
                    CreditCustomerId,
                    CreditAmount,
                    activeSuspension is null
                        ? null
                        : new SuspendedSaleCompletionRequest(
                            activeSuspension.SuspendedSaleId,
                            activeSuspension.RowVersion)),
                cancellationToken);

            if (finalized.IsFailure)
            {
                ApplyFailure(finalized.Error);
                completionInProgress = false;
                return false;
            }

            Result = new SalesPosCompletedSale(finalized.Value);
            activeSuspension = null;
            Step = DemoPosStep.Success;
            completionInProgress = false;
            if (ReceiptPrintingEnabled)
            {
                ticketPrintService.PrepareForSale(Result.Reference);
            }
            else
            {
                ticketPrintService.Reset();
            }
            await PrepareNextDraftAsync(cancellationToken);
            await LoadRecentSalesCoreAsync(cancellationToken, preserveFeedback: true);
            await LoadSuspendedSalesCoreAsync(cancellationToken, preserveFeedback: true);
            pendingOperationId = Guid.Empty;
            return true;
        }
        catch (OperationCanceledException)
        {
            completionInProgress = false;
            throw;
        }
        catch (Exception exception)
        {
            completionInProgress = false;
            return HandleUnexpectedFailure("sale-finalization", exception);
        }
    }

    public void CancelCompletion()
    {
        completionInProgress = false;
    }

    public bool TryBeginTicketPrint()
    {
        return ReceiptPrintingEnabled
            && Result is not null
            && ticketPrintService.TryBegin(Result.Reference);
    }

    public bool CompleteTicketPrint(DemoPrintSimulationOutcome outcome)
    {
        return ReceiptPrintingEnabled
            && Result is not null
            && ticketPrintService.Complete(Result.Reference, outcome);
    }

    public Task<bool> PrintTicketAsync(bool isReprint, bool simulateFailure, CancellationToken cancellationToken = default)
    {
        return !ReceiptPrintingEnabled || Result is null
            ? Task.FromResult(false)
            : ticketPrintService.PrintPersistedAsync(Result, isReprint, simulateFailure, cancellationToken);
    }

    public bool ContinueWithoutTicket()
    {
        return ReceiptPrintingEnabled
            && Result is not null
            && ticketPrintService.ContinueWithoutTicket(Result.Reference);
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

    public async Task<bool> LoadSuspendedSalesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await LoadSuspendedSalesCoreAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HandleUnexpectedFailure("suspended-sales-load", exception);
        }
    }

    public async Task<bool> LoadRecentSalesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await LoadRecentSalesCoreAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HandleUnexpectedFailure("recent-sales-load", exception);
        }
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

    private async Task<bool> LoadRecentSalesCoreAsync(
        CancellationToken cancellationToken,
        bool preserveFeedback = false)
    {
        var result = await getRecentSalesHandler.HandleAsync(
            new GetRecentSalesQuery(10),
            cancellationToken);

        if (result.IsFailure)
        {
            if (!preserveFeedback)
            {
                ApplyFailure(result.Error);
            }

            return false;
        }

        recentSales = Array.AsReadOnly(result.Value.ToArray());
        return true;
    }

    private async Task<bool> LoadSuspendedSalesCoreAsync(
        CancellationToken cancellationToken,
        bool preserveFeedback = false)
    {
        var result = await suspendedSaleWorkflow.GetOpenAsync(cancellationToken);
        if (result.IsFailure)
        {
            if (!preserveFeedback)
            {
                ApplyFailure(result.Error);
            }

            return false;
        }

        suspendedDrafts.Clear();
        suspendedDrafts.AddRange(result.Value
            .Where(sale => activeSuspension is null
                || sale.SuspendedSaleId != activeSuspension.SuspendedSaleId)
            .Select(sale => new SalesPosSuspendedDraft(sale)));
        return true;
    }

    private static SuspendedSaleSummaryView ToApplicationView(SalesPosSuspendedDraft suspension)
    {
        return new SuspendedSaleSummaryView(
            suspension.SuspendedSaleId,
            suspension.OperationId,
            suspension.Reference,
            suspension.CustomerId,
            suspension.CustomerName,
            suspension.Total,
            suspension.LineCount,
            suspension.SuspendedAtLocal.UtcDateTime,
            "Open",
            suspension.RowVersion,
            Array.AsReadOnly(suspension.Lines.Select(line => new SuspendedSaleLineView(
                line.ProductId,
                line.Reference,
                line.DisplayName,
                line.Presentation,
                line.UnitPrice,
                line.Quantity,
                line.Subtotal)).ToArray()));
    }

    private async Task<bool> StartNewDraftCoreAsync(CancellationToken cancellationToken)
    {
        if (hasPreparedNextDraft && CurrentDraft is not null)
        {
            hasPreparedNextDraft = false;
            ResetDraftPresentationState();
            Result = null;
            ticketPrintService.Reset();
            Trace.TraceInformation("Sales POS activated the draft prepared after the persistent commit.");
            return true;
        }

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

    private async Task PrepareNextDraftAsync(CancellationToken cancellationToken)
    {
        var created = await createDraftHandler.HandleAsync(cancellationToken);

        if (created.IsSuccess)
        {
            CurrentDraft = created.Value;
            hasPreparedNextDraft = true;
        }
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
        pendingOperationId = Guid.Empty;
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
            DemoPaymentMethod.Cash => ReceivedAmount is not null && ReceivedAmount.Value >= ImmediatePaymentDue,
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
        CreditCustomerId = null;
        CreditAmount = 0m;
        CardPaymentStatus = DemoCardPaymentStatus.NotStarted;
        CardVerificationCount = 0;
        pendingOperationId = Guid.Empty;
    }
}
