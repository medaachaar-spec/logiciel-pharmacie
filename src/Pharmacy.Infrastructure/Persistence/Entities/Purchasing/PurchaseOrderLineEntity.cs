using Pharmacy.Infrastructure.Persistence.Entities.Catalog;

namespace Pharmacy.Infrastructure.Persistence.Entities.Purchasing;

internal sealed class PurchaseOrderLineEntity
{
    public long Id { get; set; }
    public long PurchaseOrderId { get; set; }
    public long ProductId { get; set; }
    public string ProductCodeSnapshot { get; set; } = string.Empty;
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public int OrderedQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public decimal? UnitPurchasePrice { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public PurchaseOrderEntity PurchaseOrder { get; set; } = null!;
    public ProductEntity Product { get; set; } = null!;
    public ICollection<GoodsReceiptLineEntity> GoodsReceiptLines { get; } = new List<GoodsReceiptLineEntity>();
}
