using Pharmacy.Domain.Purchasing;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;

namespace Pharmacy.Infrastructure.Persistence.Entities.Purchasing;

internal sealed class GoodsReceiptEntity
{
    public long Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public Guid OperationId { get; set; }
    public string OperationFingerprint { get; set; } = string.Empty;
    public long SupplierId { get; set; }
    public long? PurchaseOrderId { get; set; }
    public long StockLocationId { get; set; }
    public GoodsReceiptType ReceiptType { get; set; }
    public GoodsReceiptStatus Status { get; set; }
    public string DeliveryNoteNumber { get; set; } = string.Empty;
    public DateOnly DocumentDate { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public SupplierEntity Supplier { get; set; } = null!;
    public PurchaseOrderEntity? PurchaseOrder { get; set; }
    public StockLocationEntity StockLocation { get; set; } = null!;
    public ICollection<GoodsReceiptLineEntity> Lines { get; } = new List<GoodsReceiptLineEntity>();
}
