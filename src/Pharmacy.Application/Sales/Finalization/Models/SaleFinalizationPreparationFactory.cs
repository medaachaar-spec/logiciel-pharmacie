using System.Globalization;
using Pharmacy.Application.Sales.Drafts.Models;

namespace Pharmacy.Application.Sales.Finalization.Models;

internal static class SaleFinalizationPreparationFactory
{
    public static SaleFinalizationPreparation Create(
        Guid operationId,
        SaleDraftView draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var fingerprintParts = draft.Lines
            .OrderBy(line => line.ProductId.Value)
            .Select(line => string.Join(
                ":",
                line.ProductId.Value.ToString(CultureInfo.InvariantCulture),
                line.Quantity.Value.ToString(CultureInfo.InvariantCulture),
                DecimalBits(line.UnitPrice.Amount)));
        var fingerprintValue = string.Join(
            "|",
            draft.Id.Value.ToString("N"),
            draft.Version.ToString(CultureInfo.InvariantCulture),
            string.Join(",", fingerprintParts),
            DecimalBits(draft.Total.Amount));

        return new SaleFinalizationPreparation(
            operationId,
            draft.Id,
            draft.Version,
            draft.Lines,
            draft.TotalQuantity,
            draft.Total,
            new SaleFinalizationFingerprint(fingerprintValue));
    }

    private static string DecimalBits(decimal value)
    {
        return string.Join(
            ".",
            decimal.GetBits(value)
                .Select(bits => bits.ToString("X8", CultureInfo.InvariantCulture)));
    }
}
