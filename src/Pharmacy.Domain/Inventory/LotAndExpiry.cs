using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Inventory;

public sealed record LotNumber
{
    public const int MaxLength = 128;

    private LotNumber(string value) => Value = value;

    public string Value { get; }

    public static Result<LotNumber> Create(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > MaxLength
            ? Result<LotNumber>.Failure(InventoryErrors.InvalidOperation)
            : Result<LotNumber>.Success(new LotNumber(normalized));
    }
}

public sealed record ExpiryDate(DateOnly Value)
{
    public bool IsExpired(DateOnly currentDate) => Value < currentDate;
}
