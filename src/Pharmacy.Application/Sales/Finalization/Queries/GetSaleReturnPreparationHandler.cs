using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Sales.Finalization.Queries;

public sealed record GetSaleReturnPreparationQuery(long SaleId);

public sealed class GetSaleReturnPreparationHandler
{
    private readonly ISaleReturnReader reader;
    private readonly IPermissionAuthorizer? authorizer;

    public GetSaleReturnPreparationHandler(ISaleReturnReader reader)
        : this(reader, null) { }

    public GetSaleReturnPreparationHandler(
        ISaleReturnReader reader,
        IPermissionAuthorizer? authorizer)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.authorizer = authorizer;
    }

    public Task<Result<SaleReturnPreparationView>> HandleAsync(
        GetSaleReturnPreparationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var denied = SecurityCommandGuard.Denied<SaleReturnPreparationView>(
            authorizer,
            SecurityPermissions.SalesViewHistory);
        if (denied is not null)
        {
            return Task.FromResult(denied);
        }

        return query.SaleId <= 0
            ? Task.FromResult(Result<SaleReturnPreparationView>.Failure(
                SalesApplicationErrors.InvalidSaleQuery))
            : reader.GetPreparationAsync(query.SaleId, cancellationToken);
    }
}
