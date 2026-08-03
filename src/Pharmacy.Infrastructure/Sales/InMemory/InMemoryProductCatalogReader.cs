using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Infrastructure.Sales.InMemory;

public sealed class InMemoryProductCatalogReader : IProductCatalogReader
{
    private readonly IReadOnlyList<SellableProductSnapshot> products;

    public InMemoryProductCatalogReader()
    {
        products = Array.AsReadOnly(
        [
            Product(1, "DEMO-000001", "Produit Démo A", "Boîte 20 unités", 12.50m),
            Product(2, "DEMO-000002", "Produit Démo B", "Flacon 100 ml", 18.90m),
            Product(3, "DEMO-000003", "Produit Démo C", "Tube 50 g", 24.00m),
            Product(4, "DEMO-000004", "Produit Démo D", "Sachet 12 unités", 34.75m),
            Product(5, "DEMO-000005", "Produit Démo E", "Boîte 30 unités", 49.90m),
            Product(6, "DEMO-000006", "Article Démo Hygiène A", "Flacon 250 ml", 27.50m),
            Product(7, "DEMO-000007", "Article Démo Parapharmacie A", "Tube 75 ml", 39.90m),
            Product(8, "DEMO-000008", "Produit Démo F", "Boîte 10 unités", 15.25m),
            Product(9, "DEMO-000009", "Article Démo Soin A", "Flacon 50 ml", 55.00m),
            Product(10, "DEMO-000010", "Article Démo Accessoire A", "Unité", 8.75m)
        ]);
    }

    public Task<Result<SellableProductSnapshot>> FindByIdAsync(
        ProductId productId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productId);
        cancellationToken.ThrowIfCancellationRequested();

        var product = products.FirstOrDefault(candidate => candidate.ProductId == productId);
        return Task.FromResult(product is null
            ? Result<SellableProductSnapshot>.Failure(SalesApplicationErrors.ProductNotFound)
            : Result<SellableProductSnapshot>.Success(product));
    }

    public Task<Result<SellableProductSnapshot>> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedCode = code?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return Task.FromResult(
                Result<SellableProductSnapshot>.Failure(
                    SalesApplicationErrors.InvalidProductCode));
        }

        var product = products.FirstOrDefault(candidate =>
            string.Equals(
                candidate.Reference.Value,
                normalizedCode,
                StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(product is null
            ? Result<SellableProductSnapshot>.Failure(SalesApplicationErrors.ProductNotFound)
            : Result<SellableProductSnapshot>.Success(product));
    }

    public Task<Result<IReadOnlyList<SellableProductSnapshot>>> SearchAsync(
        ProductSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedText = query.Text?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return Task.FromResult(
                Result<IReadOnlyList<SellableProductSnapshot>>.Success(
                    Array.Empty<SellableProductSnapshot>()));
        }

        var matches = products
            .Where(product => product.Reference.Value.Contains(normalizedText, StringComparison.OrdinalIgnoreCase)
                || product.DisplayName.Contains(normalizedText, StringComparison.OrdinalIgnoreCase)
                || (product.Presentation?.Contains(normalizedText, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(product => product.ProductId.Value)
            .ToArray();

        return Task.FromResult(
            Result<IReadOnlyList<SellableProductSnapshot>>.Success(
                Array.AsReadOnly(matches)));
    }

    private static SellableProductSnapshot Product(
        long id,
        string reference,
        string displayName,
        string presentation,
        decimal unitSalePrice)
    {
        return new SellableProductSnapshot(
            ProductId.Create(id).Value,
            ProductReference.Create(reference).Value,
            displayName,
            presentation,
            Money.Create(unitSalePrice).Value,
            true);
    }
}
