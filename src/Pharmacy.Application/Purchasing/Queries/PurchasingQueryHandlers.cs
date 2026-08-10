using Pharmacy.Application.Purchasing.Contracts;
using Pharmacy.Application.Purchasing.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Purchasing;

namespace Pharmacy.Application.Purchasing.Queries;

public sealed class PurchasingQueryHandlers
{
    private readonly IPurchasingReader reader;
    public PurchasingQueryHandlers(IPurchasingReader reader) => this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
    public Task<Result<IReadOnlyList<SupplierView>>> SearchSuppliersAsync(string? text = null, bool includeInactive = true, CancellationToken token = default) => reader.SearchSuppliersAsync(text, includeInactive, token);
    public Task<Result<SupplierView>> GetSupplierAsync(long id, CancellationToken token = default) => id > 0 ? reader.GetSupplierAsync(id, token) : Task.FromResult(Result<SupplierView>.Failure(PurchasingErrors.SupplierNotFound));
    public Task<Result<IReadOnlyList<PurchaseOrderView>>> ListOrdersAsync(long? supplierId = null, PurchaseOrderStatus? status = null, CancellationToken token = default) => reader.ListOrdersAsync(supplierId, status, token);
    public Task<Result<PurchaseOrderView>> GetOrderAsync(long id, CancellationToken token = default) => id > 0 ? reader.GetOrderAsync(id, token) : Task.FromResult(Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderNotFound));
    public Task<Result<IReadOnlyList<GoodsReceiptView>>> ListReceiptsAsync(long? supplierId = null, int limit = 50, CancellationToken token = default) => reader.ListReceiptsAsync(supplierId, Math.Clamp(limit, 1, 200), token);
    public Task<Result<GoodsReceiptView>> GetReceiptAsync(long id, CancellationToken token = default) => id > 0 ? reader.GetReceiptAsync(id, token) : Task.FromResult(Result<GoodsReceiptView>.Failure(PurchasingErrors.ReceiptNotFound));
    public Task<Result<IReadOnlyList<PurchasableProductView>>> SearchProductsAsync(string? text, int limit = 30, CancellationToken token = default) => reader.SearchProductsAsync(text, Math.Clamp(limit, 1, 100), token);
    public Task<PurchasingDiagnosticsView> GetDiagnosticsAsync(CancellationToken token = default) => reader.GetDiagnosticsAsync(token);
}
