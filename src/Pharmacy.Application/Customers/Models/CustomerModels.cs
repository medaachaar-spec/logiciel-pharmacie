using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Customers.Models;

public sealed record CustomerSummaryView(
    long CustomerId,
    string Reference,
    string FullName,
    string? Phone,
    bool IsActive,
    bool CreditAllowed,
    Money CreditBalance,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CustomerDetailView(
    long CustomerId,
    string Reference,
    string FullName,
    string? Phone,
    string? Note,
    bool IsActive,
    bool CreditAllowed,
    Money CreditBalance,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion,
    IReadOnlyList<CustomerLedgerEntryView> Ledger);

public sealed record CustomerLedgerEntryView(
    long EntryId,
    Guid OperationId,
    string EntryType,
    Money Amount,
    string SourceType,
    string SourceReference,
    DateTime OccurredAtUtc);

public sealed record CustomerSearchQuery(string? SearchTerm, int Limit = 25);

public sealed record CreateCustomerRequest(
    Guid OperationId,
    string FullName,
    string? Phone,
    string? Note,
    bool CreditAllowed);

public sealed record UpdateCustomerRequest(
    long CustomerId,
    string FullName,
    string? Phone,
    string? Note,
    bool IsActive,
    bool CreditAllowed,
    byte[] RowVersion);

public sealed record RegisterCreditPaymentRequest(
    Guid OperationId,
    long CustomerId,
    decimal Amount,
    SalePaymentMethod PaymentMethod,
    bool ExternalCardConfirmed);
