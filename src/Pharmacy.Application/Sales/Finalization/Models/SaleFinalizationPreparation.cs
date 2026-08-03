using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Finalization.Models;

public sealed class SaleFinalizationPreparation
{
    public SaleFinalizationPreparation(
        Guid operationId,
        SaleDraftId draftId,
        long preparedVersion,
        IEnumerable<SaleDraftLineView> lines,
        long totalQuantity,
        Money total,
        SaleFinalizationFingerprint fingerprint)
    {
        ArgumentNullException.ThrowIfNull(draftId);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(total);
        ArgumentNullException.ThrowIfNull(fingerprint);

        OperationId = operationId;
        DraftId = draftId;
        PreparedVersion = preparedVersion;
        Lines = Array.AsReadOnly(lines.ToArray());
        TotalQuantity = totalQuantity;
        Total = total;
        Status = SaleFinalizationPreparationStatus.Prepared;
        Fingerprint = fingerprint;
    }

    public Guid OperationId { get; }

    public SaleDraftId DraftId { get; }

    public long PreparedVersion { get; }

    public IReadOnlyList<SaleDraftLineView> Lines { get; }

    public long TotalQuantity { get; }

    public Money Total { get; }

    public SaleFinalizationPreparationStatus Status { get; }

    public SaleFinalizationFingerprint Fingerprint { get; }
}
