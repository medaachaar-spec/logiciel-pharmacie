using Pharmacy.Application.Customers.Contracts;
using Pharmacy.Application.Customers.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Customers.Queries;

public sealed class CustomerQueryHandlers
{
    private readonly ICustomerStore customers;

    public CustomerQueryHandlers(ICustomerStore customers)
    {
        this.customers = customers ?? throw new ArgumentNullException(nameof(customers));
    }

    public Task<Result<IReadOnlyList<CustomerSummaryView>>> SearchAsync(
        CustomerSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return customers.SearchAsync(query, cancellationToken);
    }

    public Task<Result<CustomerDetailView>> GetByIdAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        return customers.GetByIdAsync(customerId, cancellationToken);
    }
}
