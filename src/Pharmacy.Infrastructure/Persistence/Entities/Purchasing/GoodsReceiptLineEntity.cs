using Pharmacy.Infrastructure.Persistence.Entities.Catalog;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;

namespace Pharmacy.Infrastructure.Persistence.Entities.Purchasing;

internal sealed class GoodsReceiptLineEntity
{
    public long Id { get; set; }
    public long GoodsReceiptId { get; set; }
    public long ProductId { get; set; }
    public long? PurchaseOrderLineId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPurchasePrice { get; set; }
    public string ProductCodeSnapshot { get; set; } = string.Empty;
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public long? StockBatchId { get; set; }
    public string? LotNumberSnapshot { get; set; }
    public DateOnly? ExpiryDateSnapshot { get; set; }
    public long StockMovementId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public GoodsReceiptEntity GoodsReceipt { get; set; } = null!;
    public ProductEntity Product { get; set; } = null!;
    public PurchaseOrderLineEntity? PurchaseOrderLine { get; set; }
    public StockBatchEntity? StockBatch { get; set; }
    public StockMovementEntity StockMovement { get; set; } = null!;
}
