namespace Pharmacy.Application.Purchasing.Commands;

public sealed record CreateSupplierCommand(string Code, string Name, string? Phone = null, string? Email = null, string? Address = null);
public sealed record UpdateSupplierCommand(long SupplierId, string Code, string Name, string? Phone, string? Email, string? Address, string RowVersion);
public sealed record SetSupplierActivationCommand(long SupplierId, bool IsActive, string RowVersion);

public sealed record CreatePurchaseOrderCommand(long SupplierId, string OrderNumber, DateOnly OrderDate, DateOnly? ExpectedDate = null, string? Notes = null);
public sealed record AddPurchaseOrderLineCommand(long OrderId, long ProductId, int Quantity, decimal? UnitPurchasePrice, string OrderRowVersion);
public sealed record UpdatePurchaseOrderLineCommand(long OrderId, long LineId, int Quantity, decimal? UnitPurchasePrice, string OrderRowVersion, string LineRowVersion);
public sealed record RemovePurchaseOrderLineCommand(long OrderId, long LineId, string OrderRowVersion, string LineRowVersion);
public sealed record ConfirmPurchaseOrderCommand(long OrderId, string OrderRowVersion);
public sealed record CancelPurchaseOrderCommand(long OrderId, string Reason, string OrderRowVersion);

public sealed record FinalizeGoodsReceiptLineCommand(
    long ProductId,
    long? PurchaseOrderLineId,
    int Quantity,
    decimal UnitPurchasePrice,
    long? ExistingBatchId,
    string? LotNumber,
    DateOnly? ExpiryDate);

public sealed record FinalizeGoodsReceiptCommand(
    Guid OperationId,
    long SupplierId,
    long? PurchaseOrderId,
    long StockLocationId,
    string DeliveryNoteNumber,
    DateOnly DocumentDate,
    string? ExpectedOrderRowVersion,
    IReadOnlyList<FinalizeGoodsReceiptLineCommand> Lines);
