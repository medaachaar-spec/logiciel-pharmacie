namespace Pharmacy.Infrastructure.Persistence.Entities.Purchasing;

internal sealed class SupplierEntity
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<PurchaseOrderEntity> PurchaseOrders { get; } = new List<PurchaseOrderEntity>();
    public ICollection<GoodsReceiptEntity> GoodsReceipts { get; } = new List<GoodsReceiptEntity>();
}
