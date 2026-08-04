using Pharmacy.Infrastructure.Persistence.Entities.Catalog;

namespace Pharmacy.Infrastructure.Persistence.Entities.Inventory;

internal sealed class InventoryCountLineEntity
{
    public long Id { get; set; }
    public long InventorySessionId { get; set; }
    public long ProductId { get; set; }
    public long? StockBatchId { get; set; }
    public int ExpectedQuantity { get; set; }
    public int? CountedQuantity { get; set; }
    public Guid? LastOperationId { get; set; }
    public string? LastOperationFingerprint { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public InventorySessionEntity InventorySession { get; set; } = null!;
    public ProductEntity Product { get; set; } = null!;
    public StockBatchEntity? StockBatch { get; set; }
}
