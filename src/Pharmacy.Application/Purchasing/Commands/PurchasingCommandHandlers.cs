using Pharmacy.Application.Purchasing.Contracts;
using Pharmacy.Application.Purchasing.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Purchasing;

namespace Pharmacy.Application.Purchasing.Commands;

public sealed class PurchasingCommandHandlers
{
    private readonly IPurchasingStore store;
    public PurchasingCommandHandlers(IPurchasingStore store) => this.store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<Result<SupplierView>> CreateSupplierAsync(CreateSupplierCommand command, CancellationToken token = default) =>
        ValidSupplier(command.Code, command.Name) ? store.CreateSupplierAsync(command, token) : Failure<SupplierView>();
    public Task<Result<SupplierView>> UpdateSupplierAsync(UpdateSupplierCommand command, CancellationToken token = default) =>
        ValidSupplier(command.Code, command.Name) && ValidToken(command.RowVersion) ? store.UpdateSupplierAsync(command, token) : Failure<SupplierView>();
    public Task<Result<SupplierView>> SetSupplierActivationAsync(SetSupplierActivationCommand command, CancellationToken token = default) =>
        command.SupplierId > 0 && ValidToken(command.RowVersion) ? store.SetSupplierActivationAsync(command, token) : Failure<SupplierView>();
    public Task<Result<PurchaseOrderView>> CreateOrderAsync(CreatePurchaseOrderCommand command, CancellationToken token = default) =>
        command.SupplierId > 0 && !string.IsNullOrWhiteSpace(command.OrderNumber) ? store.CreateOrderAsync(command, token) : Failure<PurchaseOrderView>();
    public Task<Result<PurchaseOrderView>> AddOrderLineAsync(AddPurchaseOrderLineCommand command, CancellationToken token = default) =>
        ValidLine(command.OrderId, command.ProductId, command.Quantity, command.UnitPurchasePrice) && ValidToken(command.OrderRowVersion) ? store.AddOrderLineAsync(command, token) : Failure<PurchaseOrderView>(PurchasingErrors.OrderQuantityInvalid);
    public Task<Result<PurchaseOrderView>> UpdateOrderLineAsync(UpdatePurchaseOrderLineCommand command, CancellationToken token = default) =>
        command.LineId > 0 && ValidLine(command.OrderId, 1, command.Quantity, command.UnitPurchasePrice) && ValidToken(command.OrderRowVersion) && ValidToken(command.LineRowVersion) ? store.UpdateOrderLineAsync(command, token) : Failure<PurchaseOrderView>(PurchasingErrors.OrderQuantityInvalid);
    public Task<Result<PurchaseOrderView>> RemoveOrderLineAsync(RemovePurchaseOrderLineCommand command, CancellationToken token = default) =>
        command.OrderId > 0 && command.LineId > 0 && ValidToken(command.OrderRowVersion) && ValidToken(command.LineRowVersion) ? store.RemoveOrderLineAsync(command, token) : Failure<PurchaseOrderView>();
    public Task<Result<PurchaseOrderView>> ConfirmOrderAsync(ConfirmPurchaseOrderCommand command, CancellationToken token = default) =>
        command.OrderId > 0 && ValidToken(command.OrderRowVersion) ? store.ConfirmOrderAsync(command, token) : Failure<PurchaseOrderView>();
    public Task<Result<PurchaseOrderView>> CancelOrderAsync(CancelPurchaseOrderCommand command, CancellationToken token = default) =>
        command.OrderId > 0 && !string.IsNullOrWhiteSpace(command.Reason) && ValidToken(command.OrderRowVersion) ? store.CancelOrderAsync(command, token) : Failure<PurchaseOrderView>();

    public Task<Result<GoodsReceiptView>> FinalizeReceiptAsync(FinalizeGoodsReceiptCommand command, CancellationToken token = default)
    {
        if (command.OperationId == Guid.Empty || command.SupplierId <= 0 || command.StockLocationId <= 0 || string.IsNullOrWhiteSpace(command.DeliveryNoteNumber) || command.Lines is null || command.Lines.Count == 0 ||
            command.Lines.Any(line => line.ProductId <= 0 || line.Quantity <= 0 || line.UnitPurchasePrice < 0m || (line.ExistingBatchId is not null && !string.IsNullOrWhiteSpace(line.LotNumber))))
        {
            return Failure<GoodsReceiptView>();
        }
        return store.FinalizeReceiptAsync(command, token);
    }

    private static bool ValidSupplier(string code, string name) => !string.IsNullOrWhiteSpace(code) && code.Trim().Length <= 32 && !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 200;
    private static bool ValidLine(long orderId, long productId, int quantity, decimal? price) => orderId > 0 && productId > 0 && quantity > 0 && (price is null || price >= 0m);
    private static bool ValidToken(string token) { try { return Convert.FromBase64String(token).Length == 8; } catch (FormatException) { return false; } }
    private static Task<Result<T>> Failure<T>(Error? error = null) => Task.FromResult(Result<T>.Failure(error ?? PurchasingErrors.ValidationFailed));
}
