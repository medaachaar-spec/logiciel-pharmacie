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
    byte[] RowVersion,
    IReadOnlyList<SuspendedSaleLineView> Lines);

public sealed record SuspendedSaleLineView(
    ProductId ProductId,
    ProductReference ProductReference,
    string ProductName,
    string? Presentation,
    Money UnitPrice,
    SaleQuantity Quantity,
    Money LineTotal);

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

public sealed record SuspendedSaleCompletionRequest(
    long SuspendedSaleId,
    byte[] RowVersion);

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
    DateTime CompletedAtUtc,
    IReadOnlyList<SaleReturnLineView> Lines);

public sealed record SaleReturnLineView(
    long SaleLineId,
    ProductId ProductId,
    int Quantity,
    Money UnitPrice,
    Money LineTotal,
    ReturnStockDestination StockDestination);

public sealed record SaleReturnPreparationView(
    long SaleId,
    string Reference,
    DateTime CompletedAtUtc,
    SalePaymentMethod PaymentMethod,
    Money Total,
    long? CustomerId,
    Money CreditAmount,
    IReadOnlyList<SaleReturnableLineView> Lines,
    IReadOnlyList<SaleReturnView> PreviousReturns);

public sealed record SaleReturnableLineView(
    long SaleLineId,
    ProductId ProductId,
    ProductReference ProductReference,
    string ProductName,
    string? Presentation,
    Money UnitPrice,
    int SoldQuantity,
    int ReturnedQuantity,
    int ReturnableQuantity);

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
