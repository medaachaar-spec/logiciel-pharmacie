using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Sales;

public sealed record SaleProductSnapshot
{
    private SaleProductSnapshot(
        ProductId productId,
        ProductReference reference,
        string displayName,
        string? presentation,
        Money unitSalePrice)
    {
        ProductId = productId;
        Reference = reference;
        DisplayName = displayName;
        Presentation = presentation;
        UnitSalePrice = unitSalePrice;
    }

    public ProductId ProductId { get; }

    public ProductReference Reference { get; }

    public string DisplayName { get; }

    public string? Presentation { get; }

    public Money UnitSalePrice { get; }

    public static Result<SaleProductSnapshot> Create(
        ProductId productId,
        ProductReference reference,
        string? displayName,
        string? presentation,
        Money unitSalePrice)
    {
        ArgumentNullException.ThrowIfNull(productId);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(unitSalePrice);

        var normalizedName = displayName?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return Result<SaleProductSnapshot>.Failure(SalesErrors.InvalidProductName);
        }

        var normalizedPresentation = string.IsNullOrWhiteSpace(presentation)
            ? null
            : presentation.Trim();

        return Result<SaleProductSnapshot>.Success(
            new SaleProductSnapshot(
                productId,
                reference,
                normalizedName,
                normalizedPresentation,
                unitSalePrice));
    }
}
