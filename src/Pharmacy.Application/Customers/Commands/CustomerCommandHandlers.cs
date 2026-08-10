using Pharmacy.Application.Customers.Contracts;
using Pharmacy.Application.Customers.Errors;
using Pharmacy.Application.Customers.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Services;

namespace Pharmacy.Application.Customers.Commands;

public sealed class CustomerCommandHandlers
{
    private readonly ICustomerStore customers;
    private readonly ICreditPaymentStore creditPayments;
    private readonly IPermissionAuthorizer? authorizer;
    private readonly IBusinessAuditRecorder? audit;

    public CustomerCommandHandlers(ICustomerStore customers, ICreditPaymentStore creditPayments)
        : this(customers, creditPayments, null) { }

    public CustomerCommandHandlers(ICustomerStore customers, ICreditPaymentStore creditPayments, IPermissionAuthorizer? authorizer)
        : this(customers, creditPayments, authorizer, null) { }

    public CustomerCommandHandlers(ICustomerStore customers, ICreditPaymentStore creditPayments, IPermissionAuthorizer? authorizer, IBusinessAuditRecorder? audit)
    {
        this.customers = customers ?? throw new ArgumentNullException(nameof(customers));
        this.creditPayments = creditPayments ?? throw new ArgumentNullException(nameof(creditPayments));
        this.authorizer = authorizer;
        this.audit = audit;
    }

    public async Task<Result<CustomerDetailView>> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = request.CreditAllowed
            ? SecurityCommandGuard.Denied<CustomerDetailView>(authorizer, SecurityPermissions.CustomersManageCredit, true)
            : SecurityCommandGuard.Denied<CustomerDetailView>(authorizer, SecurityPermissions.CustomersManage);
        if (denied is not null) return denied;
        if (request.OperationId == Guid.Empty)
            return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.InvalidOperationId);
        var created = await customers.CreateAsync(request, cancellationToken);
        if (created.IsSuccess && request.CreditAllowed && audit is not null)
            await audit.RecordSuccessAsync("customers", "Customer", created.Value.CustomerId.ToString(),
                "credit.allow", request.OperationId, cancellationToken);
        return created;
    }

    public async Task<Result<CustomerDetailView>> UpdateAsync(
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = SecurityCommandGuard.Denied<CustomerDetailView>(authorizer, SecurityPermissions.CustomersManageCredit, true);
        if (denied is not null) return denied;
        var updated = await customers.UpdateAsync(request, cancellationToken);
        if (updated.IsSuccess && audit is not null)
            await audit.RecordSuccessAsync("customers", "Customer", updated.Value.CustomerId.ToString(),
                request.CreditAllowed ? "credit.allow" : "credit.disallow", null, cancellationToken);
        return updated;
    }

    public async Task<Result<CustomerDetailView>> RegisterCreditPaymentAsync(
        RegisterCreditPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = SecurityCommandGuard.Denied<CustomerDetailView>(authorizer, SecurityPermissions.CustomersManageCredit, true);
        if (denied is not null) return denied;
        if (request.OperationId == Guid.Empty)
            return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.InvalidOperationId);
        var payment = await creditPayments.RegisterPaymentAsync(request, cancellationToken);
        if (payment.IsSuccess && audit is not null)
            await audit.RecordSuccessAsync("customers", "Customer", payment.Value.CustomerId.ToString(),
                "credit.payment", request.OperationId, cancellationToken);
        return payment;
    }
}
