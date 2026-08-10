namespace Pharmacy.Application.Reporting.Models;

public sealed record ReportingPeriod(DateTime FromUtc, DateTime ToUtc)
{
    public bool IsValid => FromUtc.Kind == DateTimeKind.Utc
        && ToUtc.Kind == DateTimeKind.Utc
        && FromUtc < ToUtc;
}

public sealed record ReportingQuery(ReportingPeriod Period, int Limit = 200);

public sealed record PaymentBreakdownView(string Method, decimal Amount, int Count);

public sealed record ExecutiveSummaryView(
    ReportingPeriod Period,
    DateTime GeneratedAtUtc,
    string CurrencyCode,
    decimal GrossSales,
    decimal Refunds,
    decimal NetSales,
    int SaleCount,
    int ReturnCount,
    IReadOnlyList<PaymentBreakdownView> Payments,
    decimal OpenCredit,
    decimal CreditPayments,
    long CurrentSellableStock,
    int OutOfStockProducts,
    int LowStockProducts,
    int ExpiredBatches,
    int NearExpiryBatches,
    int PurchaseOrders,
    long ReceivedQuantity);

public sealed record SaleReportRow(
    long SaleId, string Reference, DateTime CompletedAtUtc, string PaymentMethod,
    int LineCount, long Quantity, decimal GrossAmount, decimal PaidAmount,
    decimal CreditAmount, decimal RefundAmount, decimal NetAmount);

public sealed record ProductSalesReportRow(
    long SaleLineId, DateTime CompletedAtUtc, string SaleReference, long ProductId,
    string ProductReference, string ProductName, int SoldQuantity, int ReturnedQuantity,
    decimal GrossAmount, decimal RefundAmount, decimal NetAmount);

public sealed record StockReportRow(
    long ProductId, string ProductReference, string ProductName, long SellableQuantity,
    decimal? MinimumStock, string Status);

public sealed record ExpiryReportRow(
    long BatchId, long ProductId, string ProductReference, string ProductName,
    string? BatchNumber, DateOnly? ExpiryDate, long Quantity, int? DaysRemaining, string Status);

public sealed record PurchasingReportRow(
    long ReceiptId, string ReceiptNumber, DateTime ReceivedAtUtc, string SupplierCode,
    string SupplierName, string ReceiptType, long Quantity, decimal HistoricalAmount);

public sealed record InventoryAdjustmentReportRow(
    long MovementId, DateTime OccurredAtUtc, long ProductId, string ProductReference,
    string ProductName, string MovementType, int Quantity, int QuantityBefore,
    int QuantityAfter, string Reason, string? SourceReference);

public sealed record CustomerCreditReportRow(
    long CustomerId, string CustomerReference, string CustomerName, bool CreditAllowed,
    decimal CreditGenerated, decimal CreditPaid, decimal OpenBalance);

public sealed record ReturnReportRow(
    long ReturnId, string ReturnReference, DateTime CompletedAtUtc, string SaleReference,
    string RefundMethod, decimal RefundAmount, int ReturnedQuantity, int RestockedQuantity);

public sealed record CashReportRow(
    long EntryId, DateTime OccurredAtUtc, string EntryType, decimal Amount,
    string SourceType, string SourceReference);

public sealed record AlertSettingsView(
    bool LowStockAlertsEnabled,
    bool ExpiryAlertsEnabled,
    int? ExpiryHorizonDays,
    DateTime UpdatedAtUtc,
    byte[] RowVersion);

public sealed record UpdateAlertSettingsRequest(
    Guid OperationId,
    bool LowStockAlertsEnabled,
    bool ExpiryAlertsEnabled,
    int? ExpiryHorizonDays,
    byte[] RowVersion);

public sealed record ReportingSnapshotView(
    ExecutiveSummaryView Summary,
    AlertSettingsView Settings,
    IReadOnlyList<SaleReportRow> Sales,
    IReadOnlyList<ProductSalesReportRow> ProductSales,
    IReadOnlyList<StockReportRow> Stock,
    IReadOnlyList<ExpiryReportRow> Expiry,
    IReadOnlyList<PurchasingReportRow> Purchasing,
    IReadOnlyList<InventoryAdjustmentReportRow> InventoryAdjustments,
    IReadOnlyList<CustomerCreditReportRow> CustomerCredit,
    IReadOnlyList<ReturnReportRow> Returns,
    IReadOnlyList<CashReportRow> Cash);

public enum ReportingExportKind
{
    Sales,
    ProductSales,
    Stock,
    Expiry,
    Purchasing,
    InventoryAdjustments,
    CustomerCredit,
    Returns,
    Cash
}

public sealed record ReportingExportRequest(
    Guid OperationId,
    ReportingExportKind Kind,
    ReportingQuery Query);

public sealed record CsvExportView(string FileName, string MediaType, byte[] Content);
