using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Sales;

public sealed record Money : IComparable<Money>
{
    public const string CurrencyCode = "MAD";

    public static Money Zero { get; } = new(0m);

    private Money(decimal amount)
    {
        Amount = amount;
    }

    public decimal Amount { get; }

    public static Result<Money> Create(decimal amount)
    {
        return amount < 0m
            ? Result<Money>.Failure(SalesErrors.InvalidMoney)
            : Result<Money>.Success(new Money(amount));
    }

    public Money Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new Money(checked(Amount + other.Amount));
    }

    public Money Multiply(SaleQuantity quantity)
    {
        ArgumentNullException.ThrowIfNull(quantity);
        return new Money(checked(Amount * quantity.Value));
    }

    public int CompareTo(Money? other)
    {
        return other is null ? 1 : Amount.CompareTo(other.Amount);
    }
}
