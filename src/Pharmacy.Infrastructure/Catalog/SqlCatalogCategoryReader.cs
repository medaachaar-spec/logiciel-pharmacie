using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Catalog.Contracts;
using Pharmacy.Application.Catalog.Errors;
using Pharmacy.Application.Catalog.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Catalog;

public sealed class SqlCatalogCategoryReader : ICatalogCategoryReader
{
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;

    public SqlCatalogCategoryReader(IDbContextFactory<PharmacyDbContext> contextFactory)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<Result<IReadOnlyList<CatalogCategoryView>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var categories = await context.ProductCategories
                .AsNoTracking()
                .OrderBy(category => category.SortOrder)
                .ThenBy(category => category.Name)
                .ToArrayAsync(cancellationToken);
            var views = categories.Select(SqlCatalogStore.MapCategory).ToArray();
            return Result<IReadOnlyList<CatalogCategoryView>>.Success(Array.AsReadOnly(views));
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<IReadOnlyList<CatalogCategoryView>>.Failure(
                CatalogApplicationErrors.DatabaseUnavailable);
        }
    }
}
