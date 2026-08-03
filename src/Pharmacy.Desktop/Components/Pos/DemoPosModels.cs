using System.Collections.ObjectModel;
using System.Globalization;

namespace Pharmacy.Desktop.Components.Pos;

public sealed record DemoCatalogItem(
    int Id,
    string Code,
    string Name,
    string Presentation,
    string Category,
    decimal UnitPrice,
    int AvailableQuantity,
    bool IsActive);

public sealed class DemoCartLine
{
    public DemoCartLine(DemoCatalogItem item)
        : this(item, 1)
    {
    }

    internal DemoCartLine(DemoCatalogItem item, int quantity)
    {
        Item = item;
        Quantity = quantity;
    }

    public DemoCatalogItem Item { get; }

    public int Quantity { get; private set; }

    public decimal Subtotal => Item.UnitPrice * Quantity;

    internal void Increase()
    {
        Quantity++;
    }

    internal void Decrease()
    {
        Quantity--;
    }
}

public sealed record DemoCartLineSnapshot(DemoCatalogItem Item, int Quantity)
{
    public decimal Subtotal => Item.UnitPrice * Quantity;
}

public sealed record DemoAvailabilityIncident(string ItemName, int MaximumQuantity);

public enum DemoPaymentMethod
{
    None,
    Cash,
    ExternalCard
}

public enum DemoCardPaymentStatus
{
    NotStarted,
    AwaitingConfirmation,
    Confirmed,
    NotCompleted,
    Ambiguous
}

public enum DemoPosStep
{
    Shopping,
    Payment,
    Success
}

public enum DemoCartMutationResult
{
    Added,
    Merged,
    Increased,
    Decreased,
    Removed,
    MinimumReached,
    AvailabilityReached,
    NotFound
}

public enum DemoResumeResult
{
    Resumed,
    CurrentCartNotEmpty,
    NotFound,
    Rejected
}

public sealed record DemoCompletedSale(
    string Reference,
    decimal Total,
    DemoPaymentMethod PaymentMethod,
    decimal Change,
    int ItemCount,
    int LineCount);

public static class DemoMoney
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("fr-MA");

    public static string Format(decimal amount)
    {
        return $"{amount.ToString("0.00", DisplayCulture)} MAD";
    }
}

public static class DemoCatalog
{
    public static IReadOnlyList<DemoCatalogItem> Items { get; } =
        new ReadOnlyCollection<DemoCatalogItem>(
            new[]
            {
                new DemoCatalogItem(1, "DEMO-000001", "Produit Démo A", "Boîte 20 unités", "Démonstration générale", 12.50m, 8, true),
                new DemoCatalogItem(2, "DEMO-000002", "Produit Démo B", "Flacon 100 ml", "Démonstration générale", 18.90m, 6, true),
                new DemoCatalogItem(3, "DEMO-000003", "Produit Démo C", "Tube 50 g", "Démonstration générale", 24.00m, 10, true),
                new DemoCatalogItem(4, "DEMO-000004", "Produit Démo D", "Sachet 12 unités", "Démonstration générale", 34.75m, 5, true),
                new DemoCatalogItem(5, "DEMO-000005", "Produit Démo E", "Boîte 30 unités", "Démonstration générale", 49.90m, 4, true),
                new DemoCatalogItem(6, "DEMO-000006", "Article Démo Hygiène A", "Flacon 250 ml", "Hygiène générique", 27.50m, 7, true),
                new DemoCatalogItem(7, "DEMO-000007", "Article Démo Parapharmacie A", "Tube 75 ml", "Parapharmacie générique", 39.90m, 6, true),
                new DemoCatalogItem(8, "DEMO-000008", "Produit Démo F", "Boîte 10 unités", "Démonstration générale", 15.25m, 9, true),
                new DemoCatalogItem(9, "DEMO-000009", "Article Démo Soin A", "Flacon 50 ml", "Soin générique", 55.00m, 3, true),
                new DemoCatalogItem(10, "DEMO-000010", "Article Démo Accessoire A", "Unité", "Accessoire générique", 8.75m, 12, true)
            });
}

public sealed class DemoPosSession
{
    private readonly List<DemoCartLine> lines = new();
    private readonly ReadOnlyCollection<DemoCartLine> readOnlyLines;
    private readonly DemoSuspendedSaleStore suspendedSaleStore;
    private readonly DemoTicketPrintService ticketPrintService;
    private readonly object continuityLock = new();
    private int completedSaleCounter;
    private bool completionInProgress;
    private bool continuityActionInProgress;

    public DemoPosSession()
        : this(new DemoSuspendedSaleStore(), new DemoTicketPrintService())
    {
    }

    public DemoPosSession(
        DemoSuspendedSaleStore suspendedSaleStore,
        DemoTicketPrintService ticketPrintService)
    {
        this.suspendedSaleStore = suspendedSaleStore;
        this.ticketPrintService = ticketPrintService;
        readOnlyLines = lines.AsReadOnly();
    }

    public IReadOnlyList<DemoCatalogItem> Catalog => DemoCatalog.Items;

    public IReadOnlyList<DemoCartLine> Lines => readOnlyLines;

    public IReadOnlyList<DemoSuspendedSale> SuspendedSales => suspendedSaleStore.Sales;

    public DemoPosStep Step { get; private set; } = DemoPosStep.Shopping;

    public DemoPaymentMethod PaymentMethod { get; private set; }

    public DemoCardPaymentStatus CardPaymentStatus { get; private set; }

    public int CardVerificationCount { get; private set; }

    public decimal? ReceivedAmount { get; private set; }

    public bool IsExternalTerminalConfirmed => CardPaymentStatus == DemoCardPaymentStatus.Confirmed;

    public DemoCompletedSale? Result { get; private set; }

    public DemoPrintStatus PrintStatus => ticketPrintService.Status;

    public int PrintAttemptCount => ticketPrintService.AttemptCount;

    public DateTimeOffset? LastPrintAttemptAtLocal => ticketPrintService.LastAttemptAtLocal;

    public bool ContinuedWithoutTicket => ticketPrintService.ContinuedWithoutTicket;

    public decimal Total => lines.Sum(line => line.Subtotal);

    public int TotalQuantity => lines.Sum(line => line.Quantity);

    public decimal Change => PaymentMethod == DemoPaymentMethod.Cash
        && ReceivedAmount is not null
        && ReceivedAmount.Value >= Total
            ? ReceivedAmount.Value - Total
            : 0m;

    public bool IsCompletionInProgress => completionInProgress;

    public bool IsContinuityActionInProgress => continuityActionInProgress;

    public bool HasDraft => Step != DemoPosStep.Success && lines.Count > 0;

    public bool CanSuspend => HasDraft
        && !completionInProgress
        && !continuityActionInProgress;

    public bool CanAbandon => HasDraft
        && !completionInProgress
        && !continuityActionInProgress;

    public bool CanComplete => Step == DemoPosStep.Payment
        && lines.Count > 0
        && !completionInProgress
        && !continuityActionInProgress
        && IsPaymentComplete();

    public IReadOnlyList<DemoCatalogItem> Search(string? query)
    {
        var normalizedQuery = query?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Catalog.Where(item => item.IsActive).ToArray();
        }

        return Catalog
            .Where(item => item.IsActive)
            .Where(item => item.Code.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || item.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || item.Presentation.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || item.Category.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public DemoCatalogItem? FindExactCode(string? code)
    {
        return Catalog.FirstOrDefault(
            item => item.IsActive
                && string.Equals(item.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public DemoCartLine? FindLine(int itemId)
    {
        return lines.FirstOrDefault(line => line.Item.Id == itemId);
    }

    public DemoCartMutationResult TryAddByExactCode(string? code)
    {
        var item = FindExactCode(code);
        return item is null ? DemoCartMutationResult.NotFound : AddItem(item.Id);
    }

    public DemoCartMutationResult AddItem(int itemId)
    {
        if (Step != DemoPosStep.Shopping || continuityActionInProgress)
        {
            return DemoCartMutationResult.NotFound;
        }

        var item = Catalog.FirstOrDefault(candidate => candidate.Id == itemId && candidate.IsActive);

        if (item is null)
        {
            return DemoCartMutationResult.NotFound;
        }

        var line = FindLine(itemId);

        if (line is null)
        {
            lines.Add(new DemoCartLine(item));
            return DemoCartMutationResult.Added;
        }

        if (line.Quantity >= item.AvailableQuantity)
        {
            return DemoCartMutationResult.AvailabilityReached;
        }

        line.Increase();
        return DemoCartMutationResult.Merged;
    }

    public DemoCartMutationResult Increase(int itemId)
    {
        var line = FindLine(itemId);

        if (line is null || Step != DemoPosStep.Shopping || continuityActionInProgress)
        {
            return DemoCartMutationResult.NotFound;
        }

        if (line.Quantity >= line.Item.AvailableQuantity)
        {
            return DemoCartMutationResult.AvailabilityReached;
        }

        line.Increase();
        return DemoCartMutationResult.Increased;
    }

    public DemoCartMutationResult Decrease(int itemId)
    {
        var line = FindLine(itemId);

        if (line is null || Step != DemoPosStep.Shopping || continuityActionInProgress)
        {
            return DemoCartMutationResult.NotFound;
        }

        if (line.Quantity == 1)
        {
            return DemoCartMutationResult.MinimumReached;
        }

        line.Decrease();
        return DemoCartMutationResult.Decreased;
    }

    public DemoCartMutationResult Remove(int itemId)
    {
        var line = FindLine(itemId);

        if (line is null || Step != DemoPosStep.Shopping || continuityActionInProgress)
        {
            return DemoCartMutationResult.NotFound;
        }

        lines.Remove(line);
        ResetPaymentIfCartChanged();
        return DemoCartMutationResult.Removed;
    }

    public void ClearCart()
    {
        if (Step == DemoPosStep.Success || continuityActionInProgress)
        {
            return;
        }

        lines.Clear();
        ResetDraftState();
    }

    public bool BeginPayment()
    {
        if (lines.Count == 0 || continuityActionInProgress)
        {
            return false;
        }

        Step = DemoPosStep.Payment;
        ResetPaymentState();
        return true;
    }

    public void ReturnToShopping()
    {
        if (Step != DemoPosStep.Success && !continuityActionInProgress)
        {
            Step = DemoPosStep.Shopping;
            ResetPaymentState();
            completionInProgress = false;
        }
    }

    public void SelectPaymentMethod(DemoPaymentMethod method)
    {
        if (Step != DemoPosStep.Payment
            || method is DemoPaymentMethod.None
            || continuityActionInProgress)
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
        return TransitionCardStatus(
            DemoCardPaymentStatus.AwaitingConfirmation,
            DemoCardPaymentStatus.Confirmed);
    }

    public bool ReportExternalCardNotCompleted()
    {
        return TransitionCardStatus(
            DemoCardPaymentStatus.AwaitingConfirmation,
            DemoCardPaymentStatus.NotCompleted);
    }

    public bool ReportExternalCardAmbiguous()
    {
        return TransitionCardStatus(
            DemoCardPaymentStatus.AwaitingConfirmation,
            DemoCardPaymentStatus.Ambiguous);
    }

    public bool ResolveAmbiguousCardAsConfirmed()
    {
        return TransitionCardStatus(
            DemoCardPaymentStatus.Ambiguous,
            DemoCardPaymentStatus.Confirmed);
    }

    public bool ResolveAmbiguousCardAsNotCompleted()
    {
        return TransitionCardStatus(
            DemoCardPaymentStatus.Ambiguous,
            DemoCardPaymentStatus.NotCompleted);
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

    public void ConfirmExternalTerminal(bool isConfirmed)
    {
        if (PaymentMethod != DemoPaymentMethod.ExternalCard)
        {
            return;
        }

        CardPaymentStatus = isConfirmed
            ? DemoCardPaymentStatus.Confirmed
            : DemoCardPaymentStatus.NotCompleted;
    }

    public DemoSuspendedSale? SuspendCurrent()
    {
        lock (continuityLock)
        {
            if (!CanSuspend)
            {
                return null;
            }

            continuityActionInProgress = true;

            try
            {
                var suspended = suspendedSaleStore.Suspend(lines);
                lines.Clear();
                ResetDraftState();
                return suspended;
            }
            finally
            {
                continuityActionInProgress = false;
            }
        }
    }

    public DemoResumeResult ResumeSuspended(string reference)
    {
        lock (continuityLock)
        {
            if (continuityActionInProgress || Step == DemoPosStep.Success)
            {
                return DemoResumeResult.Rejected;
            }

            if (lines.Count > 0)
            {
                return DemoResumeResult.CurrentCartNotEmpty;
            }

            continuityActionInProgress = true;

            try
            {
                if (!suspendedSaleStore.TryTake(reference, out var suspended))
                {
                    return DemoResumeResult.NotFound;
                }

                Restore(suspended);
                return DemoResumeResult.Resumed;
            }
            finally
            {
                continuityActionInProgress = false;
            }
        }
    }

    public DemoResumeResult SuspendCurrentThenResume(string reference)
    {
        lock (continuityLock)
        {
            if (!CanSuspend || !suspendedSaleStore.Contains(reference))
            {
                return DemoResumeResult.Rejected;
            }

            continuityActionInProgress = true;

            try
            {
                suspendedSaleStore.Suspend(lines);
                lines.Clear();

                if (!suspendedSaleStore.TryTake(reference, out var suspended))
                {
                    ResetDraftState();
                    return DemoResumeResult.NotFound;
                }

                Restore(suspended);
                return DemoResumeResult.Resumed;
            }
            finally
            {
                continuityActionInProgress = false;
            }
        }
    }

    public DemoResumeResult AbandonCurrentThenResume(string reference)
    {
        lock (continuityLock)
        {
            if (continuityActionInProgress
                || Step == DemoPosStep.Success
                || !suspendedSaleStore.Contains(reference))
            {
                return DemoResumeResult.Rejected;
            }

            continuityActionInProgress = true;

            try
            {
                lines.Clear();
                ResetDraftState();

                if (!suspendedSaleStore.TryTake(reference, out var suspended))
                {
                    return DemoResumeResult.NotFound;
                }

                Restore(suspended);
                return DemoResumeResult.Resumed;
            }
            finally
            {
                continuityActionInProgress = false;
            }
        }
    }

    public bool DeleteSuspended(string reference)
    {
        return !continuityActionInProgress && suspendedSaleStore.TryDelete(reference);
    }

    public bool AbandonDraft()
    {
        lock (continuityLock)
        {
            if (!CanAbandon)
            {
                return false;
            }

            continuityActionInProgress = true;

            try
            {
                lines.Clear();
                ResetDraftState();
                return true;
            }
            finally
            {
                continuityActionInProgress = false;
            }
        }
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

    public DemoCompletedSale CompleteSale()
    {
        if (!completionInProgress || !IsPaymentComplete())
        {
            throw new InvalidOperationException("La vente de démonstration ne peut pas être terminée dans cet état.");
        }

        completedSaleCounter++;
        Result = new DemoCompletedSale(
            $"DEMO-SALE-{completedSaleCounter:0000}",
            Total,
            PaymentMethod,
            Change,
            TotalQuantity,
            lines.Count);
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

    public void ResetForNewSale()
    {
        lines.Clear();
        ResetDraftState();
        Result = null;
        ticketPrintService.Reset();
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

    private void Restore(DemoSuspendedSale suspended)
    {
        lines.Clear();
        lines.AddRange(suspended.Lines.Select(line => new DemoCartLine(line.Item, line.Quantity)));
        Result = null;
        Step = DemoPosStep.Shopping;
        ResetPaymentState();
        completionInProgress = false;
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

    private void ResetPaymentIfCartChanged()
    {
        if (lines.Count == 0)
        {
            Step = DemoPosStep.Shopping;
        }

        ResetPaymentState();
        completionInProgress = false;
    }

    private void ResetDraftState()
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
