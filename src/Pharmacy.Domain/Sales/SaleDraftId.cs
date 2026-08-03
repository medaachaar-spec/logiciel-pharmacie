using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Sales;

public sealed record SaleDraftId
{
    private SaleDraftId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static Result<SaleDraftId> Create(Guid value)
    {
        return value == Guid.Empty
            ? Result<SaleDraftId>.Failure(SalesErrors.InvalidSaleDraftId)
            : Result<SaleDraftId>.Success(new SaleDraftId(value));
    }

    public static SaleDraftId New()
    {
        return new SaleDraftId(Guid.NewGuid());
    }
}
