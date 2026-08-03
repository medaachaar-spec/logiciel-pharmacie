using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Sales;

public sealed record SaleQuantity : IComparable<SaleQuantity>
{
    public static SaleQuantity One { get; } = new(1);

    private SaleQuantity(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public static Result<SaleQuantity> Create(int value)
    {
        return value <= 0
            ? Result<SaleQuantity>.Failure(SalesErrors.InvalidQuantity)
            : Result<SaleQuantity>.Success(new SaleQuantity(value));
    }

    public Result<SaleQuantity> Add(SaleQuantity other)
    {
        ArgumentNullException.ThrowIfNull(other);

        try
        {
            return Result<SaleQuantity>.Success(new SaleQuantity(checked(Value + other.Value)));
        }
        catch (OverflowException)
        {
            return Result<SaleQuantity>.Failure(SalesErrors.QuantityOverflow);
        }
    }

    public Result<SaleQuantity> Increment()
    {
        return Add(One);
    }

    public int CompareTo(SaleQuantity? other)
    {
        return other is null ? 1 : Value.CompareTo(other.Value);
    }
}
