using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Sales;

public sealed record ProductId
{
    private ProductId(long value)
    {
        Value = value;
    }

    public long Value { get; }

    public static Result<ProductId> Create(long value)
    {
        return value <= 0
            ? Result<ProductId>.Failure(SalesErrors.InvalidProductId)
            : Result<ProductId>.Success(new ProductId(value));
    }
}
