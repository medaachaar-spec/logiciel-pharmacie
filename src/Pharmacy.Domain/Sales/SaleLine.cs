using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Sales;

public sealed record SaleLine
{
    private SaleLine(SaleProductSnapshot productSnapshot, SaleQuantity quantity)
    {
        ProductSnapshot = productSnapshot;
        Quantity = quantity;
    }

    public SaleProductSnapshot ProductSnapshot { get; }

    public ProductId ProductId => ProductSnapshot.ProductId;

    public SaleQuantity Quantity { get; }

    public Money UnitPrice => ProductSnapshot.UnitSalePrice;

    public Money Subtotal => UnitPrice.Multiply(Quantity);

    public static Result<SaleLine> Create(
        SaleProductSnapshot productSnapshot,
        SaleQuantity quantity)
    {
        ArgumentNullException.ThrowIfNull(productSnapshot);
        ArgumentNullException.ThrowIfNull(quantity);
        return Result<SaleLine>.Success(new SaleLine(productSnapshot, quantity));
    }

    public Result<SaleLine> ChangeQuantity(SaleQuantity quantity)
    {
        ArgumentNullException.ThrowIfNull(quantity);
        return Result<SaleLine>.Success(new SaleLine(ProductSnapshot, quantity));
    }
}
