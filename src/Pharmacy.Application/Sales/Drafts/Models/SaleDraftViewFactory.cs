namespace Pharmacy.Application.Sales.Drafts.Models;

internal static class SaleDraftViewFactory
{
    public static SaleDraftView Create(VersionedSaleDraft versionedDraft)
    {
        ArgumentNullException.ThrowIfNull(versionedDraft);

        var draft = versionedDraft.Draft;
        var lines = draft.Lines.Select(line => new SaleDraftLineView(
            line.ProductId,
            line.ProductSnapshot.Reference,
            line.ProductSnapshot.DisplayName,
            line.ProductSnapshot.Presentation,
            line.UnitPrice,
            line.Quantity,
            line.Subtotal));

        return new SaleDraftView(
            draft.Id,
            versionedDraft.Version,
            lines,
            draft.TotalQuantity,
            draft.Total,
            draft.IsEmpty);
    }
}
