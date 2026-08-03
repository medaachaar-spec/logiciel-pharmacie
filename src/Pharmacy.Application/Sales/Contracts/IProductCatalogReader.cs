using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Contracts;

public interface IProductCatalogReader
{
    Task<Result<SellableProductSnapshot>> FindByIdAsync(
        ProductId productId,
        CancellationToken cancellationToken = default);

    Task<Result<SellableProductSnapshot>> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SellableProductSnapshot>>> SearchAsync(
        ProductSearchQuery query,
        CancellationToken cancellationToken = default);
}
