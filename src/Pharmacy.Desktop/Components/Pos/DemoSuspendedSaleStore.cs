using System.Collections.ObjectModel;

namespace Pharmacy.Desktop.Components.Pos;

public sealed record DemoSuspendedSale(
    string Reference,
    DateTimeOffset SuspendedAtLocal,
    IReadOnlyList<DemoCartLineSnapshot> Lines)
{
    public int LineCount => Lines.Count;

    public int TotalQuantity => Lines.Sum(line => line.Quantity);

    public decimal Total => Lines.Sum(line => line.Subtotal);
}

public sealed class DemoSuspendedSaleStore
{
    private readonly List<DemoSuspendedSale> sales = new();
    private readonly ReadOnlyCollection<DemoSuspendedSale> readOnlySales;
    private readonly object syncRoot = new();
    private int referenceCounter;

    public DemoSuspendedSaleStore()
    {
        readOnlySales = sales.AsReadOnly();
    }

    public IReadOnlyList<DemoSuspendedSale> Sales => readOnlySales;

    public DemoSuspendedSale Suspend(IEnumerable<DemoCartLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var snapshots = lines
            .Select(line => new DemoCartLineSnapshot(line.Item, line.Quantity))
            .ToArray();

        if (snapshots.Length == 0)
        {
            throw new InvalidOperationException("Un panier vide ne peut pas être suspendu.");
        }

        lock (syncRoot)
        {
            referenceCounter++;
            var suspended = new DemoSuspendedSale(
                $"DEMO-HOLD-{referenceCounter:0000}",
                DateTimeOffset.Now,
                new ReadOnlyCollection<DemoCartLineSnapshot>(snapshots));
            sales.Insert(0, suspended);
            return suspended;
        }
    }

    public bool Contains(string reference)
    {
        lock (syncRoot)
        {
            return FindIndex(reference) >= 0;
        }
    }

    public bool TryTake(string reference, out DemoSuspendedSale suspended)
    {
        lock (syncRoot)
        {
            var index = FindIndex(reference);

            if (index < 0)
            {
                suspended = null!;
                return false;
            }

            suspended = sales[index];
            sales.RemoveAt(index);
            return true;
        }
    }

    public bool TryDelete(string reference)
    {
        lock (syncRoot)
        {
            var index = FindIndex(reference);

            if (index < 0)
            {
                return false;
            }

            sales.RemoveAt(index);
            return true;
        }
    }

    private int FindIndex(string reference)
    {
        return sales.FindIndex(
            sale => string.Equals(sale.Reference, reference, StringComparison.Ordinal));
    }
}
