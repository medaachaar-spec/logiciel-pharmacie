using Pharmacy.Domain.Purchasing;

namespace Pharmacy.Infrastructure.Persistence.Entities.Purchasing;

internal sealed class PurchaseOrderEntity
{
    public long Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public long SupplierId { get; set; }
    public PurchaseOrderStatus Status { get; set; }
    public DateOnly OrderDate { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public string? Notes { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public SupplierEntity Supplier { get; set; } = null!;
    public ICollection<PurchaseOrderLineEntity> Lines { get; } = new List<PurchaseOrderLineEntity>();
    public ICollection<GoodsReceiptEntity> GoodsReceipts { get; } = new List<GoodsReceiptEntity>();
}
