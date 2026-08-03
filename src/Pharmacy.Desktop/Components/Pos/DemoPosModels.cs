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
    {
        Item = item;
        Quantity = 1;
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

public enum DemoPaymentMethod
{
    None,
    Cash,
    ExternalCard
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
    private int completedSaleCounter;
    private bool completionInProgress;

    public DemoPosSession()
    {
        readOnlyLines = lines.AsReadOnly();
    }

    public IReadOnlyList<DemoCatalogItem> Catalog => DemoCatalog.Items;

    public IReadOnlyList<DemoCartLine> Lines => readOnlyLines;

    public DemoPosStep Step { get; private set; } = DemoPosStep.Shopping;

    public DemoPaymentMethod PaymentMethod { get; private set; }

    public decimal? ReceivedAmount { get; private set; }

    public bool IsExternalTerminalConfirmed { get; private set; }

    public DemoCompletedSale? Result { get; private set; }

    public decimal Total => lines.Sum(line => line.Subtotal);

    public int TotalQuantity => lines.Sum(line => line.Quantity);

    public decimal Change => PaymentMethod == DemoPaymentMethod.Cash
        && ReceivedAmount is not null
        && ReceivedAmount.Value >= Total
            ? ReceivedAmount.Value - Total
            : 0m;

    public bool IsCompletionInProgress => completionInProgress;

    public bool CanComplete => Step == DemoPosStep.Payment
        && lines.Count > 0
        && !completionInProgress
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

    public DemoCartMutationResult TryAddByExactCode(string? code)
    {
        var item = FindExactCode(code);
        return item is null ? DemoCartMutationResult.NotFound : AddItem(item.Id);
    }

    public DemoCartMutationResult AddItem(int itemId)
    {
        var item = Catalog.FirstOrDefault(candidate => candidate.Id == itemId && candidate.IsActive);

        if (item is null)
        {
            return DemoCartMutationResult.NotFound;
        }

        var line = lines.FirstOrDefault(candidate => candidate.Item.Id == itemId);

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

        if (line is null)
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

        if (line is null)
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

        if (line is null)
        {
            return DemoCartMutationResult.NotFound;
        }

        lines.Remove(line);
        ResetPaymentIfCartChanged();
        return DemoCartMutationResult.Removed;
    }

    public void ClearCart()
    {
        lines.Clear();
        ResetPaymentIfCartChanged();
    }

    public bool BeginPayment()
    {
        if (lines.Count == 0)
        {
            return false;
        }

        Step = DemoPosStep.Payment;
        PaymentMethod = DemoPaymentMethod.None;
        ReceivedAmount = null;
        IsExternalTerminalConfirmed = false;
        return true;
    }

    public void ReturnToShopping()
    {
        if (Step != DemoPosStep.Success)
        {
            Step = DemoPosStep.Shopping;
            PaymentMethod = DemoPaymentMethod.None;
            ReceivedAmount = null;
            IsExternalTerminalConfirmed = false;
            completionInProgress = false;
        }
    }

    public void SelectPaymentMethod(DemoPaymentMethod method)
    {
        if (Step != DemoPosStep.Payment
            || method is DemoPaymentMethod.None)
        {
            return;
        }

        PaymentMethod = method;
        ReceivedAmount = null;
        IsExternalTerminalConfirmed = false;
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

    public void ConfirmExternalTerminal(bool isConfirmed)
    {
        IsExternalTerminalConfirmed = PaymentMethod == DemoPaymentMethod.ExternalCard && isConfirmed;
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
        return Result;
    }

    public void CancelCompletion()
    {
        completionInProgress = false;
    }

    public void ResetForNewSale()
    {
        lines.Clear();
        Step = DemoPosStep.Shopping;
        PaymentMethod = DemoPaymentMethod.None;
        ReceivedAmount = null;
        IsExternalTerminalConfirmed = false;
        Result = null;
        completionInProgress = false;
    }

    private DemoCartLine? FindLine(int itemId)
    {
        return lines.FirstOrDefault(line => line.Item.Id == itemId);
    }

    private bool IsPaymentComplete()
    {
        return PaymentMethod switch
        {
            DemoPaymentMethod.Cash => ReceivedAmount is not null && ReceivedAmount.Value >= Total,
            DemoPaymentMethod.ExternalCard => IsExternalTerminalConfirmed,
            _ => false
        };
    }

    private void ResetPaymentIfCartChanged()
    {
        if (lines.Count == 0)
        {
            Step = DemoPosStep.Shopping;
        }

        PaymentMethod = DemoPaymentMethod.None;
        ReceivedAmount = null;
        IsExternalTerminalConfirmed = false;
        completionInProgress = false;
    }
}
