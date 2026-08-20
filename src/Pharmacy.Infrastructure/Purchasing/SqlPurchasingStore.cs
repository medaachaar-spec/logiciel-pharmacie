using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Purchasing.Commands;
using Pharmacy.Application.Purchasing.Contracts;
using Pharmacy.Application.Purchasing.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Purchasing;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Catalog;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;
using Pharmacy.Infrastructure.Persistence.Entities.Purchasing;

namespace Pharmacy.Infrastructure.Purchasing;

internal sealed class SqlPurchasingStore : IPurchasingStore
{
    private readonly IDbContextFactory<PharmacyDbContext> factory;
    private readonly IPurchasingClock clock;
    public SqlPurchasingStore(IDbContextFactory<PharmacyDbContext> factory, IPurchasingClock clock) { this.factory = factory ?? throw new ArgumentNullException(nameof(factory)); this.clock = clock ?? throw new ArgumentNullException(nameof(clock)); }

    public async Task<Result<SupplierView>> CreateSupplierAsync(CreateSupplierCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken); var now = clock.UtcNow;
            var entity = new SupplierEntity { Code = NormalizeCode(command.Code), Name = command.Name.Trim(), Phone = Clean(command.Phone), Email = Clean(command.Email), Address = Clean(command.Address), IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now };
            db.Suppliers.Add(entity); await db.SaveChangesAsync(cancellationToken); return Result<SupplierView>.Success(SqlPurchasingReader.MapSupplier(entity));
        }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnique(ex)) { return Result<SupplierView>.Failure(PurchasingErrors.SupplierCodeDuplicate); }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return Unavailable<SupplierView>(); }
    }

    public async Task<Result<SupplierView>> UpdateSupplierAsync(UpdateSupplierCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken); var entity = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == command.SupplierId, cancellationToken); if (entity is null) return Result<SupplierView>.Failure(PurchasingErrors.SupplierNotFound);
            SetVersion(db, entity, nameof(SupplierEntity.RowVersion), command.RowVersion); entity.Code = NormalizeCode(command.Code); entity.Name = command.Name.Trim(); entity.Phone = Clean(command.Phone); entity.Email = Clean(command.Email); entity.Address = Clean(command.Address); entity.UpdatedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken); return Result<SupplierView>.Success(SqlPurchasingReader.MapSupplier(entity));
        }
        catch (DbUpdateConcurrencyException) { return Result<SupplierView>.Failure(PurchasingErrors.SupplierConcurrencyConflict); }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnique(ex)) { return Result<SupplierView>.Failure(PurchasingErrors.SupplierCodeDuplicate); }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return Unavailable<SupplierView>(); }
    }

    public async Task<Result<SupplierView>> SetSupplierActivationAsync(SetSupplierActivationCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken); var entity = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == command.SupplierId, cancellationToken); if (entity is null) return Result<SupplierView>.Failure(PurchasingErrors.SupplierNotFound);
            SetVersion(db, entity, nameof(SupplierEntity.RowVersion), command.RowVersion); entity.IsActive = command.IsActive; entity.UpdatedAtUtc = clock.UtcNow; await db.SaveChangesAsync(cancellationToken); return Result<SupplierView>.Success(SqlPurchasingReader.MapSupplier(entity));
        }
        catch (DbUpdateConcurrencyException) { return Result<SupplierView>.Failure(PurchasingErrors.SupplierConcurrencyConflict); }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return Unavailable<SupplierView>(); }
    }

    public async Task<Result<PurchaseOrderView>> CreateOrderAsync(CreatePurchaseOrderCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken); var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == command.SupplierId, cancellationToken); if (supplier is null) return Result<PurchaseOrderView>.Failure(PurchasingErrors.SupplierNotFound); if (!supplier.IsActive) return Result<PurchaseOrderView>.Failure(PurchasingErrors.SupplierInactive);
            var now = clock.UtcNow; var entity = new PurchaseOrderEntity { Supplier = supplier, OrderNumber = command.OrderNumber.Trim().ToUpperInvariant(), Status = PurchaseOrderStatus.Draft, OrderDate = command.OrderDate, ExpectedDate = command.ExpectedDate, Notes = Clean(command.Notes), CreatedAtUtc = now, UpdatedAtUtc = now };
            db.PurchaseOrders.Add(entity); await db.SaveChangesAsync(cancellationToken); return Result<PurchaseOrderView>.Success(SqlPurchasingReader.MapOrder(entity));
        }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnique(ex)) { return Result<PurchaseOrderView>.Failure(PurchasingErrors.ValidationFailed); }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return Unavailable<PurchaseOrderView>(); }
    }

    public async Task<Result<PurchaseOrderView>> AddOrderLineAsync(AddPurchaseOrderLineCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken); var order = await OrderQuery(db).SingleOrDefaultAsync(x => x.Id == command.OrderId, cancellationToken); if (order is null) return Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderNotFound); if (order.Status != PurchaseOrderStatus.Draft) return Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderInvalidStatus);
            SetVersion(db, order, nameof(PurchaseOrderEntity.RowVersion), command.OrderRowVersion); var product = await PurchasableProduct(db, command.ProductId, cancellationToken); if (product is null) return Result<PurchaseOrderView>.Failure(PurchasingErrors.ProductNotFound);
            if (!ValidProductSnapshot(product)) return Result<PurchaseOrderView>.Failure(PurchasingErrors.ValidationFailed);
            var existing = order.Lines.SingleOrDefault(x => x.ProductId == command.ProductId); var now = clock.UtcNow;
            if (existing is null) { order.Lines.Add(new PurchaseOrderLineEntity { Product = product, ProductCodeSnapshot = product.LocalSettings!.InternalCode ?? string.Empty, ProductNameSnapshot = product.Name, OrderedQuantity = command.Quantity, UnitPurchasePrice = command.UnitPurchasePrice, CreatedAtUtc = now, UpdatedAtUtc = now }); }
            else { existing.OrderedQuantity = checked(existing.OrderedQuantity + command.Quantity); if (command.UnitPurchasePrice is not null) existing.UnitPurchasePrice = command.UnitPurchasePrice; existing.UpdatedAtUtc = now; }
            TouchOrder(db, order, now); await db.SaveChangesAsync(cancellationToken); return Result<PurchaseOrderView>.Success(SqlPurchasingReader.MapOrder(order));
        }
        catch (OverflowException) { return Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderQuantityInvalid); }
        catch (DbUpdateConcurrencyException) { return Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderConcurrencyConflict); }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return Unavailable<PurchaseOrderView>(); }
    }

    public async Task<Result<PurchaseOrderView>> UpdateOrderLineAsync(UpdatePurchaseOrderLineCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken); var order = await OrderQuery(db).SingleOrDefaultAsync(x => x.Id == command.OrderId, cancellationToken); if (order is null) return Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderNotFound); if (order.Status != PurchaseOrderStatus.Draft) return Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderInvalidStatus); var line = order.Lines.SingleOrDefault(x => x.Id == command.LineId); if (line is null) return Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderLineNotFound);
            SetVersion(db, order, nameof(PurchaseOrderEntity.RowVersion), command.OrderRowVersion); SetVersion(db, line, nameof(PurchaseOrderLineEntity.RowVersion), command.LineRowVersion); line.OrderedQuantity = command.Quantity; line.UnitPurchasePrice = command.UnitPurchasePrice; line.UpdatedAtUtc = clock.UtcNow; db.Entry(line).Property(x => x.UpdatedAtUtc).IsModified = true; TouchOrder(db, order, line.UpdatedAtUtc); await db.SaveChangesAsync(cancellationToken); return Result<PurchaseOrderView>.Success(SqlPurchasingReader.MapOrder(order));
        }
        catch (DbUpdateConcurrencyException) { return Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderConcurrencyConflict); }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return Unavailable<PurchaseOrderView>(); }
    }

    public async Task<Result<PurchaseOrderView>> RemoveOrderLineAsync(RemovePurchaseOrderLineCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken); var order = await OrderQuery(db).SingleOrDefaultAsync(x => x.Id == command.OrderId, cancellationToken); if (order is null) return Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderNotFound); if (order.Status != PurchaseOrderStatus.Draft) return Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderInvalidStatus); var line = order.Lines.SingleOrDefault(x => x.Id == command.LineId); if (line is null) return Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderLineNotFound);
            SetVersion(db, order, nameof(PurchaseOrderEntity.RowVersion), command.OrderRowVersion); SetVersion(db, line, nameof(PurchaseOrderLineEntity.RowVersion), command.LineRowVersion); db.PurchaseOrderLines.Remove(line); TouchOrder(db, order, clock.UtcNow); await db.SaveChangesAsync(cancellationToken); return Result<PurchaseOrderView>.Success(SqlPurchasingReader.MapOrder(order));
        }
        catch (DbUpdateConcurrencyException) { return Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderConcurrencyConflict); }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return Unavailable<PurchaseOrderView>(); }
    }

    public async Task<Result<PurchaseOrderView>> ConfirmOrderAsync(ConfirmPurchaseOrderCommand command, CancellationToken cancellationToken = default) => await ChangeOrderStatus(command.OrderId, command.OrderRowVersion, false, null, cancellationToken);
    public async Task<Result<PurchaseOrderView>> CancelOrderAsync(CancelPurchaseOrderCommand command, CancellationToken cancellationToken = default) => await ChangeOrderStatus(command.OrderId, command.OrderRowVersion, true, command.Reason, cancellationToken);

    private async Task<Result<PurchaseOrderView>> ChangeOrderStatus(long orderId, string rowVersion, bool cancel, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken); var order = await OrderQuery(db).SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken); if (order is null) return Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderNotFound);
            if (cancel ? order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Confirmed) || order.Lines.Any(x => x.ReceivedQuantity > 0) : order.Status != PurchaseOrderStatus.Draft) return Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderInvalidStatus); if (!cancel && order.Lines.Count == 0) return Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderEmpty); if (!order.Supplier.IsActive) return Result<PurchaseOrderView>.Failure(PurchasingErrors.SupplierInactive);
            SetVersion(db, order, nameof(PurchaseOrderEntity.RowVersion), rowVersion); var now = clock.UtcNow; order.Status = cancel ? PurchaseOrderStatus.Cancelled : PurchaseOrderStatus.Confirmed; order.UpdatedAtUtc = now; if (cancel) { order.CancellationReason = reason!.Trim(); order.CancelledAtUtc = now; } else order.ConfirmedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken); return Result<PurchaseOrderView>.Success(SqlPurchasingReader.MapOrder(order));
        }
        catch (DbUpdateConcurrencyException) { return Result<PurchaseOrderView>.Failure(PurchasingErrors.OrderConcurrencyConflict); }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return Unavailable<PurchaseOrderView>(); }
    }

    public async Task<Result<GoodsReceiptView>> FinalizeReceiptAsync(FinalizeGoodsReceiptCommand command, CancellationToken cancellationToken = default)
    {
        var fingerprint = Fingerprint(command);
        try
        {
            await using var strategyContext = await factory.CreateDbContextAsync(cancellationToken); var strategy = strategyContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var db = await factory.CreateDbContextAsync(cancellationToken); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                var replay = await ReceiptTrackedQuery(db).SingleOrDefaultAsync(x => x.OperationId == command.OperationId, cancellationToken);
                if (replay is not null) { await transaction.CommitAsync(cancellationToken); return replay.OperationFingerprint == fingerprint ? Result<GoodsReceiptView>.Success(SqlPurchasingReader.MapReceipt(replay)) : Result<GoodsReceiptView>.Failure(PurchasingErrors.ReceiptOperationConflict); }

                var validation = await ValidateReceiptAsync(db, command, cancellationToken); if (validation.Error is not null) { await transaction.RollbackAsync(cancellationToken); return Result<GoodsReceiptView>.Failure(validation.Error); }
                var supplier = validation.Supplier!; var order = validation.Order; var location = validation.Location!; var now = clock.UtcNow;
                if (order is not null && command.ExpectedOrderRowVersion is not null) SetVersion(db, order, nameof(PurchaseOrderEntity.RowVersion), command.ExpectedOrderRowVersion);
                var receipt = new GoodsReceiptEntity { ReceiptNumber = $"REC-{now:yyyyMMdd}-{command.OperationId.ToString("N", CultureInfo.InvariantCulture)[..12].ToUpperInvariant()}", OperationId = command.OperationId, OperationFingerprint = fingerprint, Supplier = supplier, PurchaseOrder = order, StockLocation = location, ReceiptType = order is null ? GoodsReceiptType.Direct : GoodsReceiptType.PurchaseOrder, Status = GoodsReceiptStatus.Completed, DeliveryNoteNumber = command.DeliveryNoteNumber.Trim().ToUpperInvariant(), DocumentDate = command.DocumentDate, ReceivedAtUtc = now, CreatedAtUtc = now };
                db.GoodsReceipts.Add(receipt);

                var receiptLines = new List<(FinalizeGoodsReceiptLineCommand Command, ProductEntity Product, PurchaseOrderLineEntity? OrderLine, StockBatchEntity? Batch)>();
                for (var index = 0; index < command.Lines.Count; index++)
                {
                    var input = command.Lines[index]; var product = validation.Products![input.ProductId]; PurchaseOrderLineEntity? orderLine = order is null ? null : order.Lines.Single(x => x.Id == input.PurchaseOrderLineId);
                    var batchResult = await ResolveBatchAsync(db, input, product, command.OperationId, index, now, cancellationToken); if (batchResult.Error is not null) { await transaction.RollbackAsync(cancellationToken); return Result<GoodsReceiptView>.Failure(batchResult.Error); }
                    receiptLines.Add((input, product, orderLine, batchResult.Batch));
                }
                await db.SaveChangesAsync(cancellationToken);

                for (var index = 0; index < receiptLines.Count; index++)
                {
                    var item = receiptLines[index]; var batchId = item.Batch?.Id;
                    var balance = db.StockBalances.Local.SingleOrDefault(x => x.StockLocationId == location.Id && x.ProductId == item.Product.Id && x.StockBatchId == batchId)
                        ?? await db.StockBalances.SingleOrDefaultAsync(x => x.StockLocationId == location.Id && x.ProductId == item.Product.Id && x.StockBatchId == batchId, cancellationToken);
                    if (balance is null) { balance = new StockBalanceEntity { StockLocation = location, Product = item.Product, StockBatch = item.Batch, AvailableQuantity = 0, UpdatedAtUtc = now }; db.StockBalances.Add(balance); }
                    var before = balance.AvailableQuantity; balance.AvailableQuantity = checked(before + item.Command.Quantity); balance.UpdatedAtUtc = now;
                    var movementOperation = DeterministicGuid(command.OperationId, "movement", index); var movement = new StockMovementEntity { OperationId = movementOperation, OperationFingerprint = $"PURCHASE|{fingerprint}|{index}", Product = item.Product, StockLocation = location, StockBatch = item.Batch, MovementType = StockMovementType.PurchaseReceipt, Quantity = item.Command.Quantity, QuantityBefore = before, QuantityAfter = balance.AvailableQuantity, Reason = "Réception fournisseur finalisée", SourceType = "GoodsReceipt", SourceReference = receipt.ReceiptNumber, OccurredAtUtc = now, CreatedAtUtc = now };
                    db.StockMovements.Add(movement);
                    receipt.Lines.Add(new GoodsReceiptLineEntity { Product = item.Product, PurchaseOrderLine = item.OrderLine, Quantity = item.Command.Quantity, UnitPurchasePrice = item.Command.UnitPurchasePrice, ProductCodeSnapshot = item.Product.LocalSettings!.InternalCode ?? string.Empty, ProductNameSnapshot = item.Product.Name, StockBatch = item.Batch, LotNumberSnapshot = item.Batch?.BatchNumber, ExpiryDateSnapshot = item.Batch?.ExpiryDate, StockMovement = movement, CreatedAtUtc = now });
                    if (item.OrderLine is not null) { item.OrderLine.ReceivedQuantity += item.Command.Quantity; item.OrderLine.UpdatedAtUtc = now; }
                }
                if (order is not null) { order.Status = PurchaseRules.CalculateReceiptStatus(order.Lines.Select(x => (x.OrderedQuantity, x.ReceivedQuantity))); order.UpdatedAtUtc = now; }
                await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Result<GoodsReceiptView>.Success(SqlPurchasingReader.MapReceipt(receipt));
            });
        }
        catch (DbUpdateConcurrencyException) { return Result<GoodsReceiptView>.Failure(PurchasingErrors.ReceiptConcurrencyConflict); }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnique(ex))
        {
            try { await using var db = await factory.CreateDbContextAsync(cancellationToken); var replay = await ReceiptTrackedQuery(db).AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == command.OperationId, cancellationToken); if (replay is not null) return replay.OperationFingerprint == fingerprint ? Result<GoodsReceiptView>.Success(SqlPurchasingReader.MapReceipt(replay)) : Result<GoodsReceiptView>.Failure(PurchasingErrors.ReceiptOperationConflict); }
            catch (Exception retryEx) when (PurchasingSqlFailure.IsUnavailable(retryEx)) { return Unavailable<GoodsReceiptView>(); }
            return Result<GoodsReceiptView>.Failure(PurchasingErrors.DuplicateDeliveryNote);
        }
        catch (OverflowException) { return Result<GoodsReceiptView>.Failure(PurchasingErrors.ValidationFailed); }
        catch (Exception ex) when (PurchasingSqlFailure.IsUnavailable(ex)) { return Unavailable<GoodsReceiptView>(); }
    }

    private async Task<(SupplierEntity? Supplier, PurchaseOrderEntity? Order, StockLocationEntity? Location, Dictionary<long, ProductEntity>? Products, Error? Error)> ValidateReceiptAsync(PharmacyDbContext db, FinalizeGoodsReceiptCommand command, CancellationToken token)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == command.SupplierId, token); if (supplier is null) return (null, null, null, null, PurchasingErrors.ReceiptSupplierNotFound); if (!supplier.IsActive) return (null, null, null, null, PurchasingErrors.SupplierInactive);
        var location = await db.StockLocations.SingleOrDefaultAsync(x => x.Id == command.StockLocationId && x.IsActive, token); if (location is null) return (null, null, null, null, PurchasingErrors.ValidationFailed);
        PurchaseOrderEntity? order = null;
        if (command.PurchaseOrderId is not null) { order = await OrderQuery(db).SingleOrDefaultAsync(x => x.Id == command.PurchaseOrderId, token); if (order is null) return (null, null, null, null, PurchasingErrors.OrderNotFound); if (order.SupplierId != supplier.Id || order.Status is not (PurchaseOrderStatus.Confirmed or PurchaseOrderStatus.PartiallyReceived)) return (null, null, null, null, PurchasingErrors.OrderInvalidStatus); if (string.IsNullOrWhiteSpace(command.ExpectedOrderRowVersion)) return (null, null, null, null, PurchasingErrors.ReceiptConcurrencyConflict); }
        else if (command.Lines.Any(x => x.PurchaseOrderLineId is not null)) return (null, null, null, null, PurchasingErrors.OrderLineNotFound);
        var ids = command.Lines.Select(x => x.ProductId).Distinct().ToArray(); var products = await db.Products.Include(x => x.LocalSettings).Where(x => ids.Contains(x.Id) && x.IsActive && x.LocalSettings != null && x.LocalSettings.IsActive && x.LocalSettings.IsPurchasable).ToDictionaryAsync(x => x.Id, token); if (products.Count != ids.Length) return (null, null, null, null, PurchasingErrors.ProductNotFound); if (products.Values.Any(product => !ValidProductSnapshot(product))) return (null, null, null, null, PurchasingErrors.ValidationFailed);
        if (order is not null)
        {
            foreach (var group in command.Lines.GroupBy(x => x.PurchaseOrderLineId)) { if (group.Key is null) return (null, null, null, null, PurchasingErrors.OrderLineNotFound); var line = order.Lines.SingleOrDefault(x => x.Id == group.Key.Value); if (line is null || group.Any(x => x.ProductId != line.ProductId)) return (null, null, null, null, PurchasingErrors.OrderLineNotFound); if (group.Sum(x => x.Quantity) > line.OrderedQuantity - line.ReceivedQuantity) return (null, null, null, null, PurchasingErrors.QuantityExceedsOrder); }
        }
        return (supplier, order, location, products, null);
    }

    private async Task<(StockBatchEntity? Batch, Error? Error)> ResolveBatchAsync(PharmacyDbContext db, FinalizeGoodsReceiptLineCommand input, ProductEntity product, Guid operationId, int index, DateTime now, CancellationToken token)
    {
        var settings = product.LocalSettings!;
        if (input.ExistingBatchId is not null)
        {
            var existing = await db.StockBatches.SingleOrDefaultAsync(x => x.Id == input.ExistingBatchId && x.IsActive, token);
            if (existing is null || existing.ProductId != product.Id || (settings.TracksLots && string.IsNullOrWhiteSpace(existing.BatchNumber))) return (null, PurchasingErrors.InvalidLot);
            if ((settings.TracksExpiry && existing.ExpiryDate is null) || existing.ExpiryDate < clock.CurrentDate) return (null, PurchasingErrors.InvalidExpiry);
            return (existing, null);
        }
        var lot = Clean(input.LotNumber)?.ToUpperInvariant(); if (!settings.TracksLots && lot is null && input.ExpiryDate is null) return (null, null); if (settings.TracksLots && lot is null) return (null, PurchasingErrors.InvalidLot); if (settings.TracksExpiry && input.ExpiryDate is null) return (null, PurchasingErrors.InvalidExpiry); if (input.ExpiryDate is not null && input.ExpiryDate < clock.CurrentDate) return (null, PurchasingErrors.InvalidExpiry);
        var reusable = lot is null ? null : db.StockBatches.Local.SingleOrDefault(x => x.ProductId == product.Id && x.BatchNumber == lot)
            ?? (lot is null ? null : await db.StockBatches.SingleOrDefaultAsync(x => x.ProductId == product.Id && x.BatchNumber == lot, token));
        if (reusable is not null) return reusable.ExpiryDate != input.ExpiryDate ? (null, PurchasingErrors.InvalidLot) : (reusable, null);
        var batch = new StockBatchEntity { CreationOperationId = DeterministicGuid(operationId, "batch", index), CreationFingerprint = $"PURCHASE|{operationId:N}|{index}|{product.Id}|{lot}|{input.ExpiryDate:yyyy-MM-dd}", Product = product, BatchNumber = lot, ExpiryDate = input.ExpiryDate, IsActive = true, CreatedAtUtc = now }; db.StockBatches.Add(batch); return (batch, null);
    }

    private static IQueryable<PurchaseOrderEntity> OrderQuery(PharmacyDbContext db) => db.PurchaseOrders.Include(x => x.Supplier).Include(x => x.Lines);
    private static IQueryable<GoodsReceiptEntity> ReceiptTrackedQuery(PharmacyDbContext db) => db.GoodsReceipts.Include(x => x.Supplier).Include(x => x.Lines);
    private static Task<ProductEntity?> PurchasableProduct(PharmacyDbContext db, long id, CancellationToken token) => db.Products.Include(x => x.LocalSettings).SingleOrDefaultAsync(x => x.Id == id && x.IsActive && x.LocalSettings != null && x.LocalSettings.IsActive && x.LocalSettings.IsPurchasable, token);
    private static void SetVersion<TEntity>(PharmacyDbContext db, TEntity entity, string property, string token) where TEntity : class => db.Entry(entity).Property(property).OriginalValue = Convert.FromBase64String(token);
    private static void TouchOrder(PharmacyDbContext db, PurchaseOrderEntity order, DateTime now) { order.UpdatedAtUtc = now; db.Entry(order).Property(x => x.UpdatedAtUtc).IsModified = true; }
    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool ValidProductSnapshot(ProductEntity product) => product.Name.Length <= 200 && (product.LocalSettings!.InternalCode?.Length ?? 0) <= 64;
    private static Result<T> Unavailable<T>() => Result<T>.Failure(PurchasingErrors.ReceiptDatabaseUnavailable);
    private static string Fingerprint(FinalizeGoodsReceiptCommand command)
    {
        var canonical = new StringBuilder().Append(command.SupplierId).Append('|').Append(command.PurchaseOrderId).Append('|').Append(command.StockLocationId).Append('|').Append(command.DeliveryNoteNumber.Trim().ToUpperInvariant()).Append('|').Append(command.DocumentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        foreach (var line in command.Lines.OrderBy(x => x.PurchaseOrderLineId).ThenBy(x => x.ProductId).ThenBy(x => x.LotNumber, StringComparer.OrdinalIgnoreCase)) canonical.Append('|').Append(line.ProductId).Append(':').Append(line.PurchaseOrderLineId).Append(':').Append(line.Quantity).Append(':').Append(line.UnitPurchasePrice.ToString("0.0000", CultureInfo.InvariantCulture)).Append(':').Append(line.ExistingBatchId).Append(':').Append(line.LotNumber?.Trim().ToUpperInvariant()).Append(':').Append(line.ExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
    private static Guid DeterministicGuid(Guid operationId, string purpose, int index)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{operationId:N}|{purpose}|{index}")); return new Guid(hash.AsSpan(0, 16));
    }
}
