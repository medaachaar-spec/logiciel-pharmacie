using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Services;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Sales.Finalization.Commands;

public sealed class ReturnSaleHandler
{
    private readonly ISaleReturnStore store;
    private readonly IPermissionAuthorizer authorizer;
    private readonly IBusinessAuditRecorder audit;
    private readonly ICurrentSecuritySession? securitySession;

    public ReturnSaleHandler(
        ISaleReturnStore store,
        IPermissionAuthorizer authorizer,
        IBusinessAuditRecorder audit,
        ICurrentSecuritySession? securitySession = null)
    {
        this.store = store;
        this.authorizer = authorizer;
        this.audit = audit;
        this.securitySession = securitySession;
    }

    public async Task<Result<SaleReturnView>> HandleAsync(ReturnSaleRequest request, CancellationToken cancellationToken = default)
    {
        var returnPermission = SecurityCommandGuard.Denied<SaleReturnView>(authorizer, SecurityPermissions.SalesReturn, true);
        if (returnPermission is not null) return returnPermission;
        var refundPermission = SecurityCommandGuard.Denied<SaleReturnView>(authorizer, SecurityPermissions.SalesRefund, true);
        if (refundPermission is not null) return refundPermission;
        var attributedRequest = request with
        {
            PerformedByUserAccountId = securitySession?.User?.UserId
                ?? request.PerformedByUserAccountId
        };
        var result = await store.ReturnAsync(attributedRequest, cancellationToken);
        if (result.IsSuccess)
            await audit.RecordSuccessAsync("sales", "SaleReturn", result.Value.ReturnId.ToString(),
                "sale.return_refund", request.OperationId, cancellationToken);
        return result;
    }
}
