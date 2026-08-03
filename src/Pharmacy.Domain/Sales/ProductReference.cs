using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Sales;

public sealed class ProductReference : IEquatable<ProductReference>
{
    public const int MaxLength = 128;

    private ProductReference(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<ProductReference> Create(string? value)
    {
        var normalized = value?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Result<ProductReference>.Failure(SalesErrors.InvalidProductReference);
        }

        return normalized.Length > MaxLength
            ? Result<ProductReference>.Failure(SalesErrors.ProductReferenceTooLong)
            : Result<ProductReference>.Success(new ProductReference(normalized));
    }

    public bool Equals(ProductReference? other)
    {
        return other is not null
            && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return obj is ProductReference other && Equals(other);
    }

    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
    }

    public override string ToString()
    {
        return Value;
    }

    public static bool operator ==(ProductReference? left, ProductReference? right)
    {
        return EqualityComparer<ProductReference>.Default.Equals(left, right);
    }

    public static bool operator !=(ProductReference? left, ProductReference? right)
    {
        return !(left == right);
    }
}
