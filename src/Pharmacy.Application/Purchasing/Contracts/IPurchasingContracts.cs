using Pharmacy.Application.Purchasing.Commands;
using Pharmacy.Application.Purchasing.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Purchasing;

namespace Pharmacy.Application.Purchasing.Contracts;

public interface IPurchasingClock
{
    DateTime UtcNow { get; }
    DateOnly CurrentDate { get; }
}

public interface IPurchasingReader
{
    Task<Result<IReadOnlyList<SupplierView>>> SearchSuppliersAsync(string? text, bool includeInactive = true, CancellationToken cancellationToken = default);
    Task<Result<SupplierView>> GetSupplierAsync(long supplierId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PurchaseOrderView>>> ListOrdersAsync(long? supplierId = null, PurchaseOrderStatus? status = null, CancellationToken cancellationToken = default);
    Task<Result<PurchaseOrderView>> GetOrderAsync(long orderId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<GoodsReceiptView>>> ListReceiptsAsync(long? supplierId = null, int limit = 50, CancellationToken cancellationToken = default);
    Task<Result<GoodsReceiptView>> GetReceiptAsync(long receiptId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PurchasableProductView>>> SearchProductsAsync(string? text, int limit = 30, CancellationToken cancellationToken = default);
    Task<PurchasingDiagnosticsView> GetDiagnosticsAsync(CancellationToken cancellationToken = default);
}

public interface IPurchasingStore
{
    Task<Result<SupplierView>> CreateSupplierAsync(CreateSupplierCommand command, CancellationToken cancellationToken = default);
    Task<Result<SupplierView>> UpdateSupplierAsync(UpdateSupplierCommand command, CancellationToken cancellationToken = default);
    Task<Result<SupplierView>> SetSupplierActivationAsync(SetSupplierActivationCommand command, CancellationToken cancellationToken = default);
    Task<Result<PurchaseOrderView>> CreateOrderAsync(CreatePurchaseOrderCommand command, CancellationToken cancellationToken = default);
    Task<Result<PurchaseOrderView>> AddOrderLineAsync(AddPurchaseOrderLineCommand command, CancellationToken cancellationToken = default);
    Task<Result<PurchaseOrderView>> UpdateOrderLineAsync(UpdatePurchaseOrderLineCommand command, CancellationToken cancellationToken = default);
    Task<Result<PurchaseOrderView>> RemoveOrderLineAsync(RemovePurchaseOrderLineCommand command, CancellationToken cancellationToken = default);
    Task<Result<PurchaseOrderView>> ConfirmOrderAsync(ConfirmPurchaseOrderCommand command, CancellationToken cancellationToken = default);
    Task<Result<PurchaseOrderView>> CancelOrderAsync(CancelPurchaseOrderCommand command, CancellationToken cancellationToken = default);
    Task<Result<GoodsReceiptView>> FinalizeReceiptAsync(FinalizeGoodsReceiptCommand command, CancellationToken cancellationToken = default);
}
