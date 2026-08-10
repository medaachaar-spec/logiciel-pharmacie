using Pharmacy.Application.Customers.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Customers.Contracts;

public interface ICustomerStore
{
    Task<Result<CustomerDetailView>> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerDetailView>> UpdateAsync(
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<CustomerSummaryView>>> SearchAsync(
        CustomerSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerDetailView>> GetByIdAsync(
        long customerId,
        CancellationToken cancellationToken = default);
}

public interface ICreditPaymentStore
{
    Task<Result<CustomerDetailView>> RegisterPaymentAsync(
        RegisterCreditPaymentRequest request,
        CancellationToken cancellationToken = default);
}
