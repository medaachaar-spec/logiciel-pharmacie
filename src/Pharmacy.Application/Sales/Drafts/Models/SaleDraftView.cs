using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Drafts.Models;

public sealed class SaleDraftView
{
    public SaleDraftView(
        SaleDraftId id,
        long version,
        IEnumerable<SaleDraftLineView> lines,
        long totalQuantity,
        Money total,
        bool isEmpty)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(total);

        Id = id;
        Version = version;
        Lines = Array.AsReadOnly(lines.ToArray());
        LineCount = Lines.Count;
        TotalQuantity = totalQuantity;
        Total = total;
        IsEmpty = isEmpty;
    }

    public SaleDraftId Id { get; }

    public long Version { get; }

    public IReadOnlyList<SaleDraftLineView> Lines { get; }

    public int LineCount { get; }

    public long TotalQuantity { get; }

    public Money Total { get; }

    public bool IsEmpty { get; }
}
