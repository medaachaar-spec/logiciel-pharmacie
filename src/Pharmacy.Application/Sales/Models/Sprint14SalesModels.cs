using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Models;

public sealed record SuspendedSaleSummaryView(
    long SuspendedSaleId,
    Guid OperationId,
    string Reference,
    long? CustomerId,
    string? CustomerName,
    Money Total,
    int LineCount,
    DateTime SuspendedAtUtc,
    string Status,
    byte[] RowVersion);

public sealed record SuspendSaleRequest(
    Guid OperationId,
    long? CustomerId,
    IReadOnlyList<SuspendSaleLineRequest> Lines);

public sealed record SuspendSaleLineRequest(
    long ProductId,
    string ProductReference,
    string ProductName,
    string? Presentation,
    decimal UnitPrice,
    int Quantity);

public sealed record ReturnSaleRequest(
    Guid OperationId,
    long SaleId,
    IReadOnlyList<ReturnSaleLineRequest> Lines,
    ReturnRefundMethod RefundMethod);

public sealed record ReturnSaleLineRequest(
    long SaleLineId,
    int Quantity,
    ReturnStockDestination StockDestination);

public sealed record SaleReturnView(
    long ReturnId,
    string Reference,
    long OriginalSaleId,
    Money RefundAmount,
    ReturnRefundMethod RefundMethod,
    DateTime CompletedAtUtc);

public enum ReturnStockDestination
{
    RestockApproved,
    NotRestockable
}

public enum ReturnRefundMethod
{
    Cash,
    ExternalCardPending,
    CreditReduction
}
