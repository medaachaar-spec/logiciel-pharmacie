using Pharmacy.Domain.Inventory;
using Pharmacy.Infrastructure.Persistence.Entities.Catalog;

namespace Pharmacy.Infrastructure.Persistence.Entities.Inventory;

internal sealed class StockMovementEntity
{
    public long Id { get; set; }
    public Guid OperationId { get; set; }
    public string OperationFingerprint { get; set; } = string.Empty;
    public long ProductId { get; set; }
    public long StockLocationId { get; set; }
    public long? StockBatchId { get; set; }
    public StockMovementType MovementType { get; set; }
    public int Quantity { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ProductEntity Product { get; set; } = null!;
    public StockLocationEntity StockLocation { get; set; } = null!;
    public StockBatchEntity? StockBatch { get; set; }
}
