using Pharmacy.Domain.Purchasing;

namespace Pharmacy.Application.Purchasing.Models;

public sealed record SupplierView(long SupplierId, string Code, string Name, string? Phone, string? Email, string? Address, bool IsActive, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string RowVersion);
public sealed record PurchasableProductView(long ProductId, string InternalCode, string Name, bool TracksLots, bool TracksExpiry);
public sealed record PurchaseOrderLineView(long LineId, long ProductId, string ProductCodeSnapshot, string ProductNameSnapshot, int OrderedQuantity, int ReceivedQuantity, int RemainingQuantity, decimal? UnitPurchasePrice, string RowVersion);

public sealed class PurchaseOrderView
{
    public PurchaseOrderView(long orderId, string orderNumber, long supplierId, string supplierCode, string supplierName, PurchaseOrderStatus status, DateOnly orderDate, DateOnly? expectedDate, string? notes, DateTime createdAtUtc, DateTime? confirmedAtUtc, string rowVersion, IEnumerable<PurchaseOrderLineView> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        OrderId = orderId; OrderNumber = orderNumber; SupplierId = supplierId; SupplierCode = supplierCode; SupplierName = supplierName;
        Status = status; OrderDate = orderDate; ExpectedDate = expectedDate; Notes = notes; CreatedAtUtc = createdAtUtc; ConfirmedAtUtc = confirmedAtUtc; RowVersion = rowVersion;
        Lines = Array.AsReadOnly(lines.ToArray());
    }
    public long OrderId { get; }
    public string OrderNumber { get; }
    public long SupplierId { get; }
    public string SupplierCode { get; }
    public string SupplierName { get; }
    public PurchaseOrderStatus Status { get; }
    public DateOnly OrderDate { get; }
    public DateOnly? ExpectedDate { get; }
    public string? Notes { get; }
    public DateTime CreatedAtUtc { get; }
    public DateTime? ConfirmedAtUtc { get; }
    public string RowVersion { get; }
    public IReadOnlyList<PurchaseOrderLineView> Lines { get; }
    public decimal DescriptiveTotal => Lines.Sum(line => (line.UnitPurchasePrice ?? 0m) * line.OrderedQuantity);
}

public sealed record GoodsReceiptLineView(long LineId, long ProductId, string ProductCodeSnapshot, string ProductNameSnapshot, long? PurchaseOrderLineId, int Quantity, decimal UnitPurchasePrice, long? StockBatchId, string? LotNumber, DateOnly? ExpiryDate, long StockMovementId);
public sealed class GoodsReceiptView
{
    public GoodsReceiptView(long receiptId, string receiptNumber, Guid operationId, long supplierId, string supplierCode, string supplierName, long? purchaseOrderId, GoodsReceiptType receiptType, GoodsReceiptStatus status, string deliveryNoteNumber, DateOnly documentDate, DateTime receivedAtUtc, DateTime createdAtUtc, string rowVersion, IEnumerable<GoodsReceiptLineView> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ReceiptId = receiptId; ReceiptNumber = receiptNumber; OperationId = operationId; SupplierId = supplierId; SupplierCode = supplierCode; SupplierName = supplierName; PurchaseOrderId = purchaseOrderId; ReceiptType = receiptType; Status = status; DeliveryNoteNumber = deliveryNoteNumber; DocumentDate = documentDate; ReceivedAtUtc = receivedAtUtc; CreatedAtUtc = createdAtUtc; RowVersion = rowVersion; Lines = Array.AsReadOnly(lines.ToArray());
    }
    public long ReceiptId { get; }
    public string ReceiptNumber { get; }
    public Guid OperationId { get; }
    public long SupplierId { get; }
    public string SupplierCode { get; }
    public string SupplierName { get; }
    public long? PurchaseOrderId { get; }
    public GoodsReceiptType ReceiptType { get; }
    public GoodsReceiptStatus Status { get; }
    public string DeliveryNoteNumber { get; }
    public DateOnly DocumentDate { get; }
    public DateTime ReceivedAtUtc { get; }
    public DateTime CreatedAtUtc { get; }
    public string RowVersion { get; }
    public IReadOnlyList<GoodsReceiptLineView> Lines { get; }
    public decimal DescriptiveTotal => Lines.Sum(line => line.UnitPurchasePrice * line.Quantity);
}

public sealed record PurchasingDiagnosticsView(bool IsConfigured, bool IsReachable, string DatabaseName, string MigrationStatus, int SupplierCount, int OpenOrderCount, int ReceiptCount, string? ErrorCode);
