using Pharmacy.Application.Customers.Contracts;
using Pharmacy.Application.Customers.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Customers.Queries;

public sealed class CustomerQueryHandlers
{
    private readonly ICustomerStore customers;
    private readonly IPermissionAuthorizer authorizer;

    public CustomerQueryHandlers(ICustomerStore customers, IPermissionAuthorizer authorizer)
    {
        this.customers = customers ?? throw new ArgumentNullException(nameof(customers));
        this.authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
    }

    public Task<Result<IReadOnlyList<CustomerSummaryView>>> SearchAsync(
        CustomerSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var denied = SecurityCommandGuard.Denied<IReadOnlyList<CustomerSummaryView>>(
            authorizer,
            SecurityPermissions.CustomersView);
        if (denied is not null)
        {
            return Task.FromResult(denied);
        }
        return customers.SearchAsync(query, cancellationToken);
    }

    public Task<Result<CustomerDetailView>> GetByIdAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var denied = SecurityCommandGuard.Denied<CustomerDetailView>(
            authorizer,
            SecurityPermissions.CustomersView);
        if (denied is not null)
        {
            return Task.FromResult(denied);
        }
        return customers.GetByIdAsync(customerId, cancellationToken);
    }
}
