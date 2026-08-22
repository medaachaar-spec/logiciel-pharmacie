using Pharmacy.Application.Purchasing.Contracts;
using Pharmacy.Application.Purchasing.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Purchasing;

namespace Pharmacy.Application.Purchasing.Queries;

public sealed class PurchasingQueryHandlers
{
    private readonly IPurchasingReader reader;
    private readonly IPermissionAuthorizer authorizer;

    public PurchasingQueryHandlers(IPurchasingReader reader, IPermissionAuthorizer authorizer)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
    }

    public Task<Result<IReadOnlyList<SupplierView>>> SearchSuppliersAsync(
        string? text = null,
        bool includeInactive = true,
        CancellationToken token = default)
    {
        var denied = Denied<IReadOnlyList<SupplierView>>();
        return denied is null
            ? reader.SearchSuppliersAsync(text, includeInactive, token)
            : Task.FromResult(denied);
    }

    public Task<Result<SupplierView>> GetSupplierAsync(long id, CancellationToken token = default)
    {
        var denied = Denied<SupplierView>();
        if (denied is not null) return Task.FromResult(denied);
        return id > 0
            ? reader.GetSupplierAsync(id, token)
            : Task.FromResult(Result<SupplierView>.Failure(PurchasingErrors.SupplierNotFound));
    }

    public Task<Result<IReadOnlyList<PurchaseOrderView>>> ListOrdersAsync(
        long? supplierId = null,
        PurchaseOrderStatus? status = null,
        CancellationToken token = default)
    {
        var denied = Denied<IReadOnlyList<PurchaseOrderView>>();
        return denied is null
            ? reader.ListOrdersAsync(supplierId, status, token)
            : Task.FromResult(denied);
    }

    public Task<Result<PurchaseOrderView>> GetOrderAsync(long id, CancellationToken token = default)
    {
        var denied = Denied<PurchaseOrderView>();
        if (denied is not null) return Task.FromResult(denied);
        return id > 0
            ? reader.GetOrderAsync(id, token)
            : Task.FromResult(Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderNotFound));
    }

    public Task<Result<IReadOnlyList<GoodsReceiptView>>> ListReceiptsAsync(
        long? supplierId = null,
        int limit = 50,
        CancellationToken token = default)
    {
        var denied = Denied<IReadOnlyList<GoodsReceiptView>>();
        return denied is null
            ? reader.ListReceiptsAsync(supplierId, Math.Clamp(limit, 1, 200), token)
            : Task.FromResult(denied);
    }

    public Task<Result<GoodsReceiptView>> GetReceiptAsync(long id, CancellationToken token = default)
    {
        var denied = Denied<GoodsReceiptView>();
        if (denied is not null) return Task.FromResult(denied);
        return id > 0
            ? reader.GetReceiptAsync(id, token)
            : Task.FromResult(Result<GoodsReceiptView>.Failure(PurchasingErrors.ReceiptNotFound));
    }

    public Task<Result<IReadOnlyList<PurchasableProductView>>> SearchProductsAsync(
        string? text,
        int limit = 30,
        CancellationToken token = default)
    {
        var denied = Denied<IReadOnlyList<PurchasableProductView>>();
        return denied is null
            ? reader.SearchProductsAsync(text, Math.Clamp(limit, 1, 100), token)
            : Task.FromResult(denied);
    }

    public async Task<Result<PurchasingDiagnosticsView>> GetDiagnosticsAsync(
        CancellationToken token = default)
    {
        var denied = Denied<PurchasingDiagnosticsView>();
        if (denied is not null) return denied;
        return Result<PurchasingDiagnosticsView>.Success(await reader.GetDiagnosticsAsync(token));
    }

    private Result<T>? Denied<T>() => SecurityCommandGuard.Denied<T>(
        authorizer,
        SecurityPermissions.PurchasingView);
}
