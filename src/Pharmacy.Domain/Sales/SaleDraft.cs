using System.Collections.ObjectModel;
using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Sales;

public sealed class SaleDraft
{
    private readonly List<SaleLine> lines = new();
    private readonly ReadOnlyCollection<SaleLine> readOnlyLines;

    private SaleDraft(SaleDraftId id)
    {
        Id = id;
        readOnlyLines = lines.AsReadOnly();
    }

    public SaleDraftId Id { get; }

    public IReadOnlyList<SaleLine> Lines => readOnlyLines;

    public int LineCount => lines.Count;

    public long TotalQuantity => lines.Sum(line => (long)line.Quantity.Value);

    public bool IsEmpty => lines.Count == 0;

    public Money Total
    {
        get
        {
            var total = Money.Zero;

            foreach (var line in lines)
            {
                total = total.Add(line.Subtotal);
            }

            return total;
        }
    }

    public static SaleDraft Create(SaleDraftId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return new SaleDraft(id);
    }

    public static SaleDraft New()
    {
        return new SaleDraft(SaleDraftId.New());
    }

    public Result AddItem(SaleProductSnapshot snapshot, SaleQuantity quantity)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(quantity);

        var lineIndex = lines.FindIndex(line => line.ProductId == snapshot.ProductId);

        if (lineIndex < 0)
        {
            var line = SaleLine.Create(snapshot, quantity);
            lines.Add(line.Value);
            return Result.Success();
        }

        var existingLine = lines[lineIndex];
        var combinedQuantity = existingLine.Quantity.Add(quantity);

        if (combinedQuantity.IsFailure)
        {
            return Result.Failure(combinedQuantity.Error);
        }

        var changedLine = existingLine.ChangeQuantity(combinedQuantity.Value);
        lines[lineIndex] = changedLine.Value;
        return Result.Success();
    }

    public Result ChangeQuantity(ProductId productId, SaleQuantity newQuantity)
    {
        ArgumentNullException.ThrowIfNull(productId);
        ArgumentNullException.ThrowIfNull(newQuantity);

        var lineIndex = lines.FindIndex(line => line.ProductId == productId);

        if (lineIndex < 0)
        {
            return Result.Failure(SalesErrors.LineNotFound(productId));
        }

        var changedLine = lines[lineIndex].ChangeQuantity(newQuantity);
        lines[lineIndex] = changedLine.Value;
        return Result.Success();
    }

    public Result RemoveItem(ProductId productId)
    {
        ArgumentNullException.ThrowIfNull(productId);

        var lineIndex = lines.FindIndex(line => line.ProductId == productId);

        if (lineIndex < 0)
        {
            return Result.Failure(SalesErrors.LineNotFound(productId));
        }

        lines.RemoveAt(lineIndex);
        return Result.Success();
    }

    public Result Clear()
    {
        lines.Clear();
        return Result.Success();
    }
}
