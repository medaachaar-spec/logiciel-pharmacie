using Pharmacy.Application.Catalog.Contracts;
using Pharmacy.Application.Catalog.Errors;
using Pharmacy.Application.Catalog.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Catalog.Queries;

public sealed record SearchCatalogProductsQuery(
    string? Text = null,
    CatalogProductStatusFilter Status = CatalogProductStatusFilter.All,
    long? CategoryId = null,
    int Limit = 50);

public sealed class SearchCatalogProductsHandler
{
    public const int MaximumResults = 100;
    private readonly ICatalogProductReader reader;
    private readonly IPermissionAuthorizer authorizer;

    public SearchCatalogProductsHandler(ICatalogProductReader reader, IPermissionAuthorizer authorizer)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
    }

    public Task<Result<IReadOnlyList<CatalogProductListItem>>> HandleAsync(
        SearchCatalogProductsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var denied = SecurityCommandGuard.Denied<IReadOnlyList<CatalogProductListItem>>(
            authorizer,
            SecurityPermissions.CatalogView);
        if (denied is not null) return Task.FromResult(denied);

        if (query.Limit is <= 0 or > MaximumResults || query.CategoryId <= 0)
        {
            return Task.FromResult(Result<IReadOnlyList<CatalogProductListItem>>.Failure(
                CatalogApplicationErrors.ValidationFailed("Les filtres Catalogue sont invalides.")));
        }

        return reader.SearchAsync(
            new CatalogProductSearchCriteria(
                query.Text?.Trim(),
                query.Status,
                query.CategoryId,
                query.Limit),
            cancellationToken);
    }
}

public sealed record GetCatalogProductByIdQuery(long ProductId);

public sealed class GetCatalogProductByIdHandler
{
    private readonly ICatalogProductReader reader;
    private readonly IPermissionAuthorizer authorizer;

    public GetCatalogProductByIdHandler(ICatalogProductReader reader, IPermissionAuthorizer authorizer)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
    }

    public Task<Result<CatalogProductDetails>> HandleAsync(
        GetCatalogProductByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var denied = SecurityCommandGuard.Denied<CatalogProductDetails>(
            authorizer,
            SecurityPermissions.CatalogView);
        if (denied is not null) return Task.FromResult(denied);

        return query.ProductId <= 0
            ? Task.FromResult(Result<CatalogProductDetails>.Failure(
                CatalogApplicationErrors.ValidationFailed("L’identifiant produit est invalide.")))
            : reader.GetByIdAsync(query.ProductId, cancellationToken);
    }
}

public sealed class ListCatalogCategoriesHandler
{
    private readonly ICatalogCategoryReader reader;
    private readonly IPermissionAuthorizer authorizer;

    public ListCatalogCategoriesHandler(ICatalogCategoryReader reader, IPermissionAuthorizer authorizer)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
    }

    public Task<Result<IReadOnlyList<CatalogCategoryView>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var denied = SecurityCommandGuard.Denied<IReadOnlyList<CatalogCategoryView>>(
            authorizer,
            SecurityPermissions.CatalogView);
        if (denied is not null) return Task.FromResult(denied);

        return reader.ListAsync(cancellationToken);
    }
}

public sealed class GetCatalogDiagnosticsHandler
{
    private readonly ICatalogDiagnosticsReader reader;

    public GetCatalogDiagnosticsHandler(ICatalogDiagnosticsReader reader)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public Task<CatalogDiagnosticsView> HandleAsync(CancellationToken cancellationToken = default)
    {
        return reader.GetAsync(cancellationToken);
    }
}
