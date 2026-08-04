using Pharmacy.Infrastructure.Persistence.Entities.Catalog;

namespace Pharmacy.Infrastructure.Persistence.Entities.Inventory;

internal sealed class StockBatchEntity
{
    public long Id { get; set; }
    public Guid CreationOperationId { get; set; }
    public string CreationFingerprint { get; set; } = string.Empty;
    public long ProductId { get; set; }
    public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ProductEntity Product { get; set; } = null!;
    public ICollection<StockBalanceEntity> Balances { get; } = new List<StockBalanceEntity>();
    public ICollection<StockMovementEntity> Movements { get; } = new List<StockMovementEntity>();
    public ICollection<InventoryCountLineEntity> InventoryCountLines { get; } = new List<InventoryCountLineEntity>();
}
