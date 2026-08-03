using Pharmacy.Application.Sales.Errors;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Catalog.Models;

public enum SellableProductLookupKind
{
    ProductId,
    ExactCode
}

public sealed class SellableProductLookup
{
    private SellableProductLookup(
        SellableProductLookupKind kind,
        ProductId? productId,
        string? exactCode)
    {
        Kind = kind;
        ProductId = productId;
        ExactCode = exactCode;
    }

    public SellableProductLookupKind Kind { get; }

    public ProductId? ProductId { get; }

    public string? ExactCode { get; }

    public static SellableProductLookup ByProductId(ProductId productId)
    {
        ArgumentNullException.ThrowIfNull(productId);
        return new SellableProductLookup(
            SellableProductLookupKind.ProductId,
            productId,
            null);
    }

    public static Result<SellableProductLookup> ByExactCode(string? exactCode)
    {
        var normalized = exactCode?.Trim();

        return string.IsNullOrWhiteSpace(normalized)
            ? Result<SellableProductLookup>.Failure(SalesApplicationErrors.InvalidProductCode)
            : Result<SellableProductLookup>.Success(
                new SellableProductLookup(
                    SellableProductLookupKind.ExactCode,
                    null,
                    normalized));
    }
}
