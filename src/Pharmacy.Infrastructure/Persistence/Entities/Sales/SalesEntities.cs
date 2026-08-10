using Pharmacy.Application.Sales.Models;
using Pharmacy.Infrastructure.Persistence.Entities.Catalog;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;

namespace Pharmacy.Infrastructure.Persistence.Entities.Sales;

internal sealed class SaleEntity
{
    public long Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public Guid OperationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CompletedAtUtc { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidTotal { get; set; }
    public decimal ChangeGiven { get; set; }
    public long TotalQuantity { get; set; }
    public int LineCount { get; set; }
    public SalePaymentMethod PaymentMethod { get; set; }
    public ICollection<SaleLineEntity> Lines { get; } = new List<SaleLineEntity>();
    public ICollection<PaymentEntity> Payments { get; } = new List<PaymentEntity>();
}

internal sealed class SaleLineEntity
{
    public long Id { get; set; }
    public long SaleId { get; set; }
    public long ProductId { get; set; }
    public string ProductReferenceSnapshot { get; set; } = string.Empty;
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public string? PresentationSnapshot { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public SaleEntity Sale { get; set; } = null!;
    public ProductEntity Product { get; set; } = null!;
    public ICollection<SaleLineAllocationEntity> Allocations { get; } =
        new List<SaleLineAllocationEntity>();
}

internal sealed class SaleLineAllocationEntity
{
    public long Id { get; set; }
    public long SaleLineId { get; set; }
    public long StockBalanceId { get; set; }
    public long? StockBatchId { get; set; }
    public int Quantity { get; set; }
    public string? BatchNumberSnapshot { get; set; }
    public DateOnly? ExpiryDateSnapshot { get; set; }
    public SaleLineEntity SaleLine { get; set; } = null!;
    public StockBalanceEntity StockBalance { get; set; } = null!;
    public StockBatchEntity? StockBatch { get; set; }
}

internal sealed class PaymentEntity
{
    public long Id { get; set; }
    public long SaleId { get; set; }
    public Guid OperationId { get; set; }
    public SalePaymentMethod PaymentMethod { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? TenderedAmount { get; set; }
    public decimal ChangeGiven { get; set; }
    public bool ExternalCardConfirmed { get; set; }
    public DateTime ConfirmedAtUtc { get; set; }
    public SaleEntity Sale { get; set; } = null!;
}

internal sealed class SaleOperationEntity
{
    public long Id { get; set; }
    public Guid OperationId { get; set; }
    public string OperationFingerprint { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long? ResultSaleId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public SaleEntity? ResultSale { get; set; }
}
