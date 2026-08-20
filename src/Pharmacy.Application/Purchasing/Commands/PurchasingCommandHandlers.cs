using Pharmacy.Application.Purchasing.Contracts;
using Pharmacy.Application.Purchasing.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Purchasing;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Services;

namespace Pharmacy.Application.Purchasing.Commands;

public sealed class PurchasingCommandHandlers
{
    private readonly IPurchasingStore store;
    private readonly IPermissionAuthorizer? authorizer;
    private readonly IBusinessAuditRecorder? audit;
    public PurchasingCommandHandlers(IPurchasingStore store) : this(store, null) { }
    public PurchasingCommandHandlers(IPurchasingStore store, IPermissionAuthorizer? authorizer)
        : this(store, authorizer, null) { }
    public PurchasingCommandHandlers(IPurchasingStore store, IPermissionAuthorizer? authorizer, IBusinessAuditRecorder? audit)
    { this.store = store ?? throw new ArgumentNullException(nameof(store)); this.authorizer = authorizer; this.audit = audit; }

    public Task<Result<SupplierView>> CreateSupplierAsync(CreateSupplierCommand command, CancellationToken token = default) =>
        ExecuteAsync("Supplier", "supplier.create", null, false,
            () => ValidSupplier(command) ? store.CreateSupplierAsync(command, token) : Failure<SupplierView>(), value => value.SupplierId.ToString(), token);
    public Task<Result<SupplierView>> UpdateSupplierAsync(UpdateSupplierCommand command, CancellationToken token = default) =>
        ExecuteAsync("Supplier", "supplier.update", null, false,
            () => ValidSupplier(command.Code, command.Name, command.Phone, command.Email, command.Address) && ValidToken(command.RowVersion) ? store.UpdateSupplierAsync(command, token) : Failure<SupplierView>(), value => value.SupplierId.ToString(), token);
    public Task<Result<SupplierView>> SetSupplierActivationAsync(SetSupplierActivationCommand command, CancellationToken token = default) =>
        ExecuteAsync("Supplier", command.IsActive ? "supplier.activate" : "supplier.deactivate", null, false,
            () => command.SupplierId > 0 && ValidToken(command.RowVersion) ? store.SetSupplierActivationAsync(command, token) : Failure<SupplierView>(), value => value.SupplierId.ToString(), token);
    public Task<Result<PurchaseOrderView>> CreateOrderAsync(CreatePurchaseOrderCommand command, CancellationToken token = default) =>
        ExecuteAsync("PurchaseOrder", "order.create", null, false,
            () => ValidOrder(command) ? store.CreateOrderAsync(command, token) : Failure<PurchaseOrderView>(), value => value.OrderId.ToString(), token);
    public Task<Result<PurchaseOrderView>> AddOrderLineAsync(AddPurchaseOrderLineCommand command, CancellationToken token = default) =>
        ExecuteAsync("PurchaseOrder", "order.line.add", null, false,
            () => ValidLine(command.OrderId, command.ProductId, command.Quantity, command.UnitPurchasePrice) && ValidToken(command.OrderRowVersion) ? store.AddOrderLineAsync(command, token) : InvalidLine<PurchaseOrderView>(command.Quantity, command.UnitPurchasePrice), value => value.OrderId.ToString(), token);
    public Task<Result<PurchaseOrderView>> UpdateOrderLineAsync(UpdatePurchaseOrderLineCommand command, CancellationToken token = default) =>
        ExecuteAsync("PurchaseOrder", "order.line.update", null, false,
            () => command.LineId > 0 && ValidLine(command.OrderId, 1, command.Quantity, command.UnitPurchasePrice) && ValidToken(command.OrderRowVersion) && ValidToken(command.LineRowVersion) ? store.UpdateOrderLineAsync(command, token) : InvalidLine<PurchaseOrderView>(command.Quantity, command.UnitPurchasePrice), value => value.OrderId.ToString(), token);
    public Task<Result<PurchaseOrderView>> RemoveOrderLineAsync(RemovePurchaseOrderLineCommand command, CancellationToken token = default) =>
        ExecuteAsync("PurchaseOrder", "order.line.remove", null, false,
            () => command.OrderId > 0 && command.LineId > 0 && ValidToken(command.OrderRowVersion) && ValidToken(command.LineRowVersion) ? store.RemoveOrderLineAsync(command, token) : Failure<PurchaseOrderView>(), value => value.OrderId.ToString(), token);
    public Task<Result<PurchaseOrderView>> ConfirmOrderAsync(ConfirmPurchaseOrderCommand command, CancellationToken token = default) =>
        ExecuteAsync("PurchaseOrder", "order.confirm", null, false,
            () => command.OrderId > 0 && ValidToken(command.OrderRowVersion) ? store.ConfirmOrderAsync(command, token) : Failure<PurchaseOrderView>(), value => value.OrderId.ToString(), token);
    public Task<Result<PurchaseOrderView>> CancelOrderAsync(CancelPurchaseOrderCommand command, CancellationToken token = default) =>
        ExecuteAsync("PurchaseOrder", "order.cancel", null, false,
            () => command.OrderId > 0 && IsText(command.Reason, 500) && ValidToken(command.OrderRowVersion) ? store.CancelOrderAsync(command, token) : Failure<PurchaseOrderView>(), value => value.OrderId.ToString(), token);

    public async Task<Result<GoodsReceiptView>> FinalizeReceiptAsync(FinalizeGoodsReceiptCommand command, CancellationToken token = default)
    {
        return await ExecuteAsync("GoodsReceipt", "receipt.finalize", command.OperationId, true,
            () => ValidReceipt(command) ? store.FinalizeReceiptAsync(command, token) : Failure<GoodsReceiptView>(),
            value => value.ReceiptId.ToString(), token);
    }

    private async Task<Result<T>> ExecuteAsync<T>(string entityType, string action, Guid? operationId, bool recent, Func<Task<Result<T>>> operation, Func<T, string> entityId, CancellationToken token)
    {
        var denied = SecurityCommandGuard.Denied<T>(authorizer, SecurityPermissions.PurchasingReceive, recent);
        if (denied is not null) { await AuditFailureQuietlyAsync(entityType, action, operationId, denied.Error.Code, token); return denied; }
        var result = await operation();
        if (result.IsSuccess && audit is not null)
            await audit.RecordSuccessAsync("purchasing", entityType, entityId(result.Value), action, operationId, token);
        else if (result.IsFailure)
            await AuditFailureQuietlyAsync(entityType, action, operationId, result.Error.Code, token);
        return result;
    }

    private async Task AuditFailureQuietlyAsync(string entityType, string action, Guid? operationId, string reasonCode, CancellationToken token)
    {
        if (audit is null) return;
        try { await audit.RecordFailureAsync("purchasing", entityType, null, action, operationId, reasonCode, token); }
        catch { }
    }

    private static bool ValidSupplier(CreateSupplierCommand command) => ValidSupplier(command.Code, command.Name, command.Phone, command.Email, command.Address);
    private static bool ValidSupplier(string code, string name, string? phone, string? email, string? address) => IsText(code, 32) && IsText(name, 200) && IsOptionalText(phone, 32) && IsOptionalText(email, 254) && IsOptionalText(address, 500);
    private static bool ValidOrder(CreatePurchaseOrderCommand command) => command.SupplierId > 0 && IsText(command.OrderNumber, 64) && IsOptionalText(command.Notes, 1000);
    private static bool ValidLine(long orderId, long productId, int quantity, decimal? price) => orderId > 0 && productId > 0 && quantity > 0 && (price is null || PurchaseRules.ValidateUnitPurchasePrice(price.Value).IsSuccess);
    private static bool ValidReceipt(FinalizeGoodsReceiptCommand command) => command.OperationId != Guid.Empty && command.SupplierId > 0 && command.StockLocationId > 0 && IsText(command.DeliveryNoteNumber, 128) && (command.ExpectedOrderRowVersion is null || ValidToken(command.ExpectedOrderRowVersion)) && command.Lines is { Count: > 0 } && command.Lines.All(ValidReceiptLine);
    private static bool ValidReceiptLine(FinalizeGoodsReceiptLineCommand line) => line.ProductId > 0 && line.Quantity > 0 && PurchaseRules.ValidateUnitPurchasePrice(line.UnitPurchasePrice).IsSuccess && IsOptionalText(line.LotNumber, 80) && (line.ExistingBatchId is null || (string.IsNullOrWhiteSpace(line.LotNumber) && line.ExpiryDate is null));
    private static bool IsText(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximum;
    private static bool IsOptionalText(string? value, int maximum) => string.IsNullOrWhiteSpace(value) || value.Trim().Length <= maximum;
    private static bool ValidToken(string? token) { if (string.IsNullOrWhiteSpace(token)) return false; try { return Convert.FromBase64String(token).Length == 8; } catch (FormatException) { return false; } }
    private static Task<Result<T>> InvalidLine<T>(int quantity, decimal? price) => Failure<T>(quantity <= 0 ? PurchasingErrors.OrderQuantityInvalid : price is not null && PurchaseRules.ValidateUnitPurchasePrice(price.Value).IsFailure ? PurchasingErrors.ValidationFailed : null);
    private static Task<Result<T>> Failure<T>(Error? error = null) => Task.FromResult(Result<T>.Failure(error ?? PurchasingErrors.ValidationFailed));
}
