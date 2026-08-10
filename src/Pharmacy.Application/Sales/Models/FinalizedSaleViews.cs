using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Models;

public sealed class FinalizedSaleView
{
    public FinalizedSaleView(
        long saleId,
        string reference,
        Guid operationId,
        DateTime completedAtUtc,
        Money total,
        long totalQuantity,
        IEnumerable<FinalizedSaleLineView> lines,
        SalePaymentView payment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentNullException.ThrowIfNull(total);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(payment);

        SaleId = saleId;
        Reference = reference;
        OperationId = operationId;
        CompletedAtUtc = DateTime.SpecifyKind(completedAtUtc, DateTimeKind.Utc);
        Total = total;
        TotalQuantity = totalQuantity;
        Lines = Array.AsReadOnly(lines.ToArray());
        Payment = payment;
    }

    public long SaleId { get; }

    public string Reference { get; }

    public Guid OperationId { get; }

    public DateTime CompletedAtUtc { get; }

    public string Status => "Completed";

    public Money Total { get; }

    public long TotalQuantity { get; }

    public int LineCount => Lines.Count;

    public IReadOnlyList<FinalizedSaleLineView> Lines { get; }

    public SalePaymentView Payment { get; }
}

public sealed class FinalizedSaleLineView
{
    public FinalizedSaleLineView(
        long saleLineId,
        ProductId productId,
        ProductReference reference,
        string productName,
        string? presentation,
        Money unitPrice,
        SaleQuantity quantity,
        Money subtotal,
        IEnumerable<SaleAllocationView> allocations)
    {
        ArgumentNullException.ThrowIfNull(productId);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentNullException.ThrowIfNull(unitPrice);
        ArgumentNullException.ThrowIfNull(quantity);
        ArgumentNullException.ThrowIfNull(subtotal);
        ArgumentNullException.ThrowIfNull(allocations);

        SaleLineId = saleLineId;
        ProductId = productId;
        Reference = reference;
        ProductName = productName;
        Presentation = presentation;
        UnitPrice = unitPrice;
        Quantity = quantity;
        Subtotal = subtotal;
        Allocations = Array.AsReadOnly(allocations.ToArray());
    }

    public long SaleLineId { get; }

    public ProductId ProductId { get; }

    public ProductReference Reference { get; }

    public string ProductName { get; }

    public string? Presentation { get; }

    public Money UnitPrice { get; }

    public SaleQuantity Quantity { get; }

    public Money Subtotal { get; }

    public IReadOnlyList<SaleAllocationView> Allocations { get; }
}

public sealed record SaleAllocationView(
    long AllocationId,
    long StockBalanceId,
    long? BatchId,
    string? BatchNumber,
    DateOnly? ExpiryDate,
    int Quantity);

public sealed record SalePaymentView(
    long PaymentId,
    SalePaymentMethod Method,
    Money Amount,
    decimal? TenderedAmount,
    decimal ChangeGiven,
    bool ExternalCardConfirmed,
    DateTime ConfirmedAtUtc);

public sealed record FinalizedSaleSummaryView(
    long SaleId,
    string Reference,
    DateTime CompletedAtUtc,
    int LineCount,
    long TotalQuantity,
    Money Total,
    SalePaymentMethod PaymentMethod,
    string Status);
