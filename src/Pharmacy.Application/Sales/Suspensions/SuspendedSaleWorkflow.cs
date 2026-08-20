using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Services;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Sales.Suspensions;

public sealed class SuspendedSaleWorkflow
{
    private readonly ISuspendedSaleStore store;
    private readonly IPermissionAuthorizer? authorizer;
    private readonly IBusinessAuditRecorder? audit;

    public SuspendedSaleWorkflow(
        ISuspendedSaleStore store,
        IPermissionAuthorizer? authorizer = null,
        IBusinessAuditRecorder? audit = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.authorizer = authorizer;
        this.audit = audit;
    }

    public async Task<Result<SuspendedSaleSummaryView>> SuspendAsync(
        SuspendSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = SecurityCommandGuard.Denied<SuspendedSaleSummaryView>(
            authorizer,
            SecurityPermissions.SalesCreate);
        if (denied is not null)
        {
            return denied;
        }

        var result = await store.SuspendAsync(request, cancellationToken);
        if (result.IsSuccess && audit is not null)
        {
            await audit.RecordSuccessAsync(
                "sales",
                "SuspendedSale",
                result.Value.SuspendedSaleId.ToString(),
                "sale.suspend",
                request.OperationId,
                cancellationToken);
        }

        return result;
    }

    public Task<Result<IReadOnlyList<SuspendedSaleSummaryView>>> GetOpenAsync(
        CancellationToken cancellationToken = default)
    {
        var denied = SecurityCommandGuard.Denied<IReadOnlyList<SuspendedSaleSummaryView>>(
            authorizer,
            SecurityPermissions.SalesCreate);
        return denied is not null
            ? Task.FromResult(denied)
            : store.GetOpenAsync(cancellationToken);
    }

    public async Task<Result> AbandonAsync(
        Guid operationId,
        long suspendedSaleId,
        byte[] rowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rowVersion);
        if (authorizer is not null)
        {
            var authorization = authorizer.Authorize(SecurityPermissions.SalesCreate);
            if (authorization.IsFailure)
            {
                return Result.Failure(authorization.Error);
            }
        }

        var result = await store.AbandonAsync(
            operationId,
            suspendedSaleId,
            rowVersion,
            cancellationToken);
        if (result.IsSuccess && audit is not null)
        {
            await audit.RecordSuccessAsync(
                "sales",
                "SuspendedSale",
                suspendedSaleId.ToString(),
                "sale.abandon",
                operationId,
                cancellationToken);
        }

        return result;
    }
}
