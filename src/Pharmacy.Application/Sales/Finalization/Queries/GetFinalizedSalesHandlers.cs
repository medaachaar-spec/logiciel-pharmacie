using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Sales.Finalization.Queries;

public sealed record GetSaleByIdQuery(long SaleId);

public sealed class GetSaleByIdHandler
{
    private readonly ISalesReader reader;
    private readonly IPermissionAuthorizer? authorizer;

    public GetSaleByIdHandler(ISalesReader reader)
        : this(reader, null) { }

    public GetSaleByIdHandler(
        ISalesReader reader,
        IPermissionAuthorizer? authorizer)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.authorizer = authorizer;
    }

    public Task<Result<FinalizedSaleView>> HandleAsync(
        GetSaleByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var denied = SecurityCommandGuard.Denied<FinalizedSaleView>(
            authorizer,
            SecurityPermissions.SalesViewHistory);
        if (denied is not null)
        {
            return Task.FromResult(denied);
        }

        return query.SaleId <= 0
            ? Task.FromResult(Result<FinalizedSaleView>.Failure(SalesApplicationErrors.InvalidSaleQuery))
            : reader.GetByIdAsync(query.SaleId, cancellationToken);
    }
}

public sealed record GetRecentSalesQuery(int Limit = 10);

public sealed class GetRecentSalesHandler
{
    private readonly ISalesReader reader;
    private readonly IPermissionAuthorizer? authorizer;

    public GetRecentSalesHandler(ISalesReader reader)
        : this(reader, null) { }

    public GetRecentSalesHandler(
        ISalesReader reader,
        IPermissionAuthorizer? authorizer)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.authorizer = authorizer;
    }

    public Task<Result<IReadOnlyList<FinalizedSaleSummaryView>>> HandleAsync(
        GetRecentSalesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var denied = SecurityCommandGuard.Denied<IReadOnlyList<FinalizedSaleSummaryView>>(
            authorizer,
            SecurityPermissions.SalesViewHistory);
        if (denied is not null)
        {
            return Task.FromResult(denied);
        }

        return query.Limit is < 1 or > 50
            ? Task.FromResult(Result<IReadOnlyList<FinalizedSaleSummaryView>>.Failure(
                SalesApplicationErrors.InvalidSaleQuery))
            : reader.GetRecentAsync(query.Limit, cancellationToken);
    }
}
