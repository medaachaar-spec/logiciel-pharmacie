namespace Pharmacy.Domain.Purchasing;

public enum PurchaseOrderStatus
{
    Draft,
    Confirmed,
    PartiallyReceived,
    FullyReceived,
    Cancelled
}

public enum GoodsReceiptStatus
{
    Completed
}

public enum GoodsReceiptType
{
    PurchaseOrder,
    Direct
}
