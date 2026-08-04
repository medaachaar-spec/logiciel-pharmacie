using Pharmacy.Domain.Inventory;

namespace Pharmacy.Infrastructure.Persistence.Entities.Inventory;

internal sealed class InventorySessionEntity
{
    public long Id { get; set; }
    public Guid CreationOperationId { get; set; }
    public string CreationFingerprint { get; set; } = string.Empty;
    public Guid? StartOperationId { get; set; }
    public string? StartOperationFingerprint { get; set; }
    public Guid? FinalizationOperationId { get; set; }
    public string? FinalizationFingerprint { get; set; }
    public Guid? CancellationOperationId { get; set; }
    public string? CancellationFingerprint { get; set; }
    public string Reference { get; set; } = string.Empty;
    public long StockLocationId { get; set; }
    public InventorySessionStatus Status { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Notes { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public StockLocationEntity StockLocation { get; set; } = null!;
    public ICollection<InventoryCountLineEntity> Lines { get; } = new List<InventoryCountLineEntity>();
}
