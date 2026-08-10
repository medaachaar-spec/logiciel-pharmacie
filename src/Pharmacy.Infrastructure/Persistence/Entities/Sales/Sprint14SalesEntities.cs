namespace Pharmacy.Infrastructure.Persistence.Entities.Sales;

internal sealed class SuspendedSaleEntity
{
    public long Id { get; set; }
    public Guid OperationId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public long? CustomerId { get; set; }
    public string? CustomerNameSnapshot { get; set; }
    public decimal GrandTotal { get; set; }
    public int LineCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SuspendedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<SuspendedSaleLineEntity> Lines { get; } = new List<SuspendedSaleLineEntity>();
}

internal sealed class SuspendedSaleLineEntity
{
    public long Id { get; set; }
    public long SuspendedSaleId { get; set; }
    public long ProductId { get; set; }
    public string ProductReferenceSnapshot { get; set; } = string.Empty;
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public string? PresentationSnapshot { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public SuspendedSaleEntity SuspendedSale { get; set; } = null!;
}

internal sealed class SaleReturnEntity
{
    public long Id { get; set; }
    public Guid OperationId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public long OriginalSaleId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
    public string RefundMethod { get; set; } = string.Empty;
    public DateTime CompletedAtUtc { get; set; }
    public ICollection<SaleReturnLineEntity> Lines { get; } = new List<SaleReturnLineEntity>();
}

internal sealed class SaleReturnLineEntity
{
    public long Id { get; set; }
    public long SaleReturnId { get; set; }
    public long SaleLineId { get; set; }
    public long ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string StockDestination { get; set; } = string.Empty;
    public SaleReturnEntity SaleReturn { get; set; } = null!;
}
