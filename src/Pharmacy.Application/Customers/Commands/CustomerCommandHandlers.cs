using Pharmacy.Application.Customers.Contracts;
using Pharmacy.Application.Customers.Errors;
using Pharmacy.Application.Customers.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Customers.Commands;

public sealed class CustomerCommandHandlers
{
    private readonly ICustomerStore customers;
    private readonly ICreditPaymentStore creditPayments;

    public CustomerCommandHandlers(ICustomerStore customers, ICreditPaymentStore creditPayments)
    {
        this.customers = customers ?? throw new ArgumentNullException(nameof(customers));
        this.creditPayments = creditPayments ?? throw new ArgumentNullException(nameof(creditPayments));
    }

    public Task<Result<CustomerDetailView>> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.OperationId == Guid.Empty
            ? Task.FromResult(Result<CustomerDetailView>.Failure(CustomerApplicationErrors.InvalidOperationId))
            : customers.CreateAsync(request, cancellationToken);
    }

    public Task<Result<CustomerDetailView>> UpdateAsync(
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return customers.UpdateAsync(request, cancellationToken);
    }

    public Task<Result<CustomerDetailView>> RegisterCreditPaymentAsync(
        RegisterCreditPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.OperationId == Guid.Empty
            ? Task.FromResult(Result<CustomerDetailView>.Failure(CustomerApplicationErrors.InvalidOperationId))
            : creditPayments.RegisterPaymentAsync(request, cancellationToken);
    }
}
