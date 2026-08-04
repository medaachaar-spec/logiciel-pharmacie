namespace Pharmacy.Infrastructure.Persistence.Entities.Inventory;

internal sealed class StockLocationEntity
{
    public long Id { get; set; }
    public Guid CreationOperationId { get; set; }
    public string CreationFingerprint { get; set; } = string.Empty;
    public Guid? LastOperationId { get; set; }
    public string? LastOperationFingerprint { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsPrimary { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<StockBalanceEntity> Balances { get; } = new List<StockBalanceEntity>();
    public ICollection<StockMovementEntity> Movements { get; } = new List<StockMovementEntity>();
    public ICollection<InventorySessionEntity> InventorySessions { get; } = new List<InventorySessionEntity>();
}
