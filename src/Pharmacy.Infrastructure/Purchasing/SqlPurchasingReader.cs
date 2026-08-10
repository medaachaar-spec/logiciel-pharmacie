using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Purchasing.Contracts;
using Pharmacy.Application.Purchasing.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Purchasing;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Purchasing;

namespace Pharmacy.Infrastructure.Purchasing;

internal sealed class SqlPurchasingReader : IPurchasingReader
{
    private readonly IDbContextFactory<PharmacyDbContext> factory;
    public SqlPurchasingReader(IDbContextFactory<PharmacyDbContext> factory) => this.factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public async Task<Result<IReadOnlyList<SupplierView>>> SearchSuppliersAsync(string? text, bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            var query = db.Suppliers.AsNoTracking();
            if (!includeInactive) query = query.Where(x => x.IsActive);
            if (!string.IsNullOrWhiteSpace(text)) { var term = text.Trim(); query = query.Where(x => x.Code.Contains(term) || x.Name.Contains(term)); }
            var rows = await query.OrderBy(x => x.Name).Take(200).ToListAsync(cancellationToken);
            return Result<IReadOnlyList<SupplierView>>.Success(rows.Select(MapSupplier).ToArray());
        }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return Unavailable<IReadOnlyList<SupplierView>>(); }
    }

    public async Task<Result<SupplierView>> GetSupplierAsync(long supplierId, CancellationToken cancellationToken = default)
    {
        try { await using var db = await factory.CreateDbContextAsync(cancellationToken); var row = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == supplierId, cancellationToken); return row is null ? Result<SupplierView>.Failure(PurchasingErrors.SupplierNotFound) : Result<SupplierView>.Success(MapSupplier(row)); }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return Unavailable<SupplierView>(); }
    }

    public async Task<Result<IReadOnlyList<PurchaseOrderView>>> ListOrdersAsync(long? supplierId = null, PurchaseOrderStatus? status = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken); var query = db.PurchaseOrders.AsNoTracking().Include(x => x.Supplier).Include(x => x.Lines).AsQueryable();
            if (supplierId is not null) query = query.Where(x => x.SupplierId == supplierId); if (status is not null) query = query.Where(x => x.Status == status);
            var rows = await query.OrderByDescending(x => x.OrderDate).ThenByDescending(x => x.Id).Take(200).ToListAsync(cancellationToken);
            return Result<IReadOnlyList<PurchaseOrderView>>.Success(rows.Select(MapOrder).ToArray());
        }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return Unavailable<IReadOnlyList<PurchaseOrderView>>(); }
    }

    public async Task<Result<PurchaseOrderView>> GetOrderAsync(long orderId, CancellationToken cancellationToken = default)
    {
        try { await using var db = await factory.CreateDbContextAsync(cancellationToken); var row = await db.PurchaseOrders.AsNoTracking().Include(x => x.Supplier).Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken); return row is null ? Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderNotFound) : Result<PurchaseOrderView>.Success(MapOrder(row)); }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return Unavailable<PurchaseOrderView>(); }
    }

    public async Task<Result<IReadOnlyList<GoodsReceiptView>>> ListReceiptsAsync(long? supplierId = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken); var query = ReceiptQuery(db); if (supplierId is not null) query = query.Where(x => x.SupplierId == supplierId);
            var rows = await query.OrderByDescending(x => x.ReceivedAtUtc).Take(Math.Clamp(limit, 1, 200)).ToListAsync(cancellationToken);
            return Result<IReadOnlyList<GoodsReceiptView>>.Success(rows.Select(MapReceipt).ToArray());
        }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return Unavailable<IReadOnlyList<GoodsReceiptView>>(); }
    }

    public async Task<Result<GoodsReceiptView>> GetReceiptAsync(long receiptId, CancellationToken cancellationToken = default)
    {
        try { await using var db = await factory.CreateDbContextAsync(cancellationToken); var row = await ReceiptQuery(db).SingleOrDefaultAsync(x => x.Id == receiptId, cancellationToken); return row is null ? Result<GoodsReceiptView>.Failure(PurchasingErrors.ReceiptNotFound) : Result<GoodsReceiptView>.Success(MapReceipt(row)); }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return Unavailable<GoodsReceiptView>(); }
    }

    public async Task<Result<IReadOnlyList<PurchasableProductView>>> SearchProductsAsync(string? text, int limit = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken); var query = db.Products.AsNoTracking().Where(x => x.IsActive && x.LocalSettings != null && x.LocalSettings.IsActive && x.LocalSettings.IsPurchasable);
            if (!string.IsNullOrWhiteSpace(text)) { var term = text.Trim(); query = query.Where(x => x.Name.Contains(term) || (x.LocalSettings!.InternalCode != null && x.LocalSettings.InternalCode.Contains(term))); }
            var rows = await query.OrderBy(x => x.Name).Take(Math.Clamp(limit, 1, 100)).Select(x => new PurchasableProductView(x.Id, x.LocalSettings!.InternalCode ?? string.Empty, x.Name, x.LocalSettings.TracksLots, x.LocalSettings.TracksExpiry)).ToListAsync(cancellationToken);
            return Result<IReadOnlyList<PurchasableProductView>>.Success(rows);
        }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return Unavailable<IReadOnlyList<PurchasableProductView>>(); }
    }

    public async Task<PurchasingDiagnosticsView> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken); var connection = db.Database.GetDbConnection();
            var suppliers = await db.Suppliers.CountAsync(cancellationToken); var open = await db.PurchaseOrders.CountAsync(x => x.Status == PurchaseOrderStatus.Confirmed || x.Status == PurchaseOrderStatus.PartiallyReceived, cancellationToken); var receipts = await db.GoodsReceipts.CountAsync(cancellationToken);
            var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
            return new(true, true, connection.Database, pending.Any() ? "Migrations en attente" : "À jour", suppliers, open, receipts, null);
        }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return new(true, false, "Indisponible", "Non vérifiable", 0, 0, 0, PurchasingErrors.ReceiptDatabaseUnavailable.Code); }
    }

    internal static SupplierView MapSupplier(SupplierEntity x) => new(x.Id, x.Code, x.Name, x.Phone, x.Email, x.Address, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc, Convert.ToBase64String(x.RowVersion));
    internal static PurchaseOrderView MapOrder(PurchaseOrderEntity x) => new(x.Id, x.OrderNumber, x.SupplierId, x.Supplier.Code, x.Supplier.Name, x.Status, x.OrderDate, x.ExpectedDate, x.Notes, x.CreatedAtUtc, x.ConfirmedAtUtc, Convert.ToBase64String(x.RowVersion), x.Lines.OrderBy(line => line.Id).Select(line => new PurchaseOrderLineView(line.Id, line.ProductId, line.ProductCodeSnapshot, line.ProductNameSnapshot, line.OrderedQuantity, line.ReceivedQuantity, line.OrderedQuantity - line.ReceivedQuantity, line.UnitPurchasePrice, Convert.ToBase64String(line.RowVersion))));
    internal static GoodsReceiptView MapReceipt(GoodsReceiptEntity x) => new(x.Id, x.ReceiptNumber, x.OperationId, x.SupplierId, x.Supplier.Code, x.Supplier.Name, x.PurchaseOrderId, x.ReceiptType, x.Status, x.DeliveryNoteNumber, x.DocumentDate, x.ReceivedAtUtc, x.CreatedAtUtc, Convert.ToBase64String(x.RowVersion), x.Lines.OrderBy(line => line.Id).Select(line => new GoodsReceiptLineView(line.Id, line.ProductId, line.ProductCodeSnapshot, line.ProductNameSnapshot, line.PurchaseOrderLineId, line.Quantity, line.UnitPurchasePrice, line.StockBatchId, line.LotNumberSnapshot, line.ExpiryDateSnapshot, line.StockMovementId)));
    internal static IQueryable<GoodsReceiptEntity> ReceiptQuery(PharmacyDbContext db) => db.GoodsReceipts.AsNoTracking().Include(x => x.Supplier).Include(x => x.Lines);
    private static Result<T> Unavailable<T>() => Result<T>.Failure(PurchasingErrors.ReceiptDatabaseUnavailable);
}
