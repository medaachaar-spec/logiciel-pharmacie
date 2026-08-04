using Pharmacy.Infrastructure.Persistence.Entities.Catalog;

namespace Pharmacy.Infrastructure.Persistence.Entities.Inventory;

internal sealed class StockBalanceEntity
{
    public long Id { get; set; }
    public long StockLocationId { get; set; }
    public long ProductId { get; set; }
    public long? StockBatchId { get; set; }
    public int AvailableQuantity { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public StockLocationEntity StockLocation { get; set; } = null!;
    public ProductEntity Product { get; set; } = null!;
    public StockBatchEntity? StockBatch { get; set; }
}
