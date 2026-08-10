using Pharmacy.Application.Inventory.Models;
using Pharmacy.Application.Inventory.Queries;
using Pharmacy.Application.Purchasing.Commands;
using Pharmacy.Application.Purchasing.Models;
using Pharmacy.Application.Purchasing.Queries;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Purchasing;

namespace Pharmacy.Desktop.Components.Purchasing;

public sealed class PurchasingCoordinator
{
    private readonly PurchasingQueryHandlers queries;
    private readonly PurchasingCommandHandlers commands;
    private readonly InventoryQueryHandlers inventory;

    public PurchasingCoordinator(PurchasingQueryHandlers queries, PurchasingCommandHandlers commands, InventoryQueryHandlers inventory)
    {
        this.queries = queries; this.commands = commands; this.inventory = inventory;
    }

    public IReadOnlyList<SupplierView> Suppliers { get; private set; } = [];
    public IReadOnlyList<PurchaseOrderView> Orders { get; private set; } = [];
    public IReadOnlyList<GoodsReceiptView> Receipts { get; private set; } = [];
    public IReadOnlyList<PurchasableProductView> Products { get; private set; } = [];
    public IReadOnlyList<StockLocationView> Locations { get; private set; } = [];
    public SupplierView? SelectedSupplier { get; private set; }
    public PurchaseOrderView? SelectedOrder { get; private set; }
    public GoodsReceiptView? SelectedReceipt { get; private set; }
    public bool IsBusy { get; private set; }
    public string? Message { get; private set; }
    public string? ErrorCode { get; private set; }

    public Task InitializeAsync(CancellationToken token) => RunAsync(async () =>
    {
        var suppliers = await queries.SearchSuppliersAsync(token: token); var orders = await queries.ListOrdersAsync(token: token); var receipts = await queries.ListReceiptsAsync(token: token); var products = await queries.SearchProductsAsync(null, token: token); var locations = await inventory.ListLocationsAsync(token);
        if (!Apply(suppliers) || !Apply(orders) || !Apply(receipts) || !Apply(products) || !Apply(locations)) return;
        Suppliers = suppliers.Value; Orders = orders.Value; Receipts = receipts.Value; Products = products.Value; Locations = locations.Value.Where(x => x.IsActive).ToArray();
        SelectedSupplier = Suppliers.FirstOrDefault(); SelectedOrder = Orders.FirstOrDefault(); SelectedReceipt = Receipts.FirstOrDefault();
    });

    public Task SearchSuppliersAsync(string? text, bool includeInactive, CancellationToken token) => RunAsync(async () => { var result = await queries.SearchSuppliersAsync(text, includeInactive, token); if (Apply(result)) Suppliers = result.Value; });
    public void SelectSupplier(long id) => SelectedSupplier = Suppliers.FirstOrDefault(x => x.SupplierId == id);
    public Task CreateSupplierAsync(CreateSupplierCommand command, CancellationToken token) => MutateAsync(() => commands.CreateSupplierAsync(command, token), async value => { Message = $"Fournisseur {value.Code} créé."; await ReloadAsync(token); SelectedSupplier = Suppliers.FirstOrDefault(x => x.SupplierId == value.SupplierId); });
    public Task UpdateSupplierAsync(UpdateSupplierCommand command, CancellationToken token) => MutateAsync(() => commands.UpdateSupplierAsync(command, token), async value => { Message = $"Fournisseur {value.Code} enregistré."; await ReloadAsync(token); SelectedSupplier = Suppliers.FirstOrDefault(x => x.SupplierId == value.SupplierId); });
    public Task ToggleSupplierAsync(CancellationToken token)
    {
        var supplier = SelectedSupplier; return supplier is null ? Task.CompletedTask : MutateAsync(() => commands.SetSupplierActivationAsync(new(supplier.SupplierId, !supplier.IsActive, supplier.RowVersion), token), async value => { Message = value.IsActive ? "Fournisseur réactivé." : "Fournisseur désactivé sans suppression."; await ReloadAsync(token); SelectedSupplier = Suppliers.FirstOrDefault(x => x.SupplierId == value.SupplierId); });
    }

    public void SelectOrder(long id) => SelectedOrder = Orders.FirstOrDefault(x => x.OrderId == id);
    public Task CreateOrderAsync(CreatePurchaseOrderCommand command, CancellationToken token) => MutateAsync(() => commands.CreateOrderAsync(command, token), async value => { Message = $"Commande {value.OrderNumber} créée en brouillon."; await ReloadAsync(token); SelectedOrder = Orders.FirstOrDefault(x => x.OrderId == value.OrderId); });
    public Task AddLineAsync(long productId, int quantity, decimal? price, CancellationToken token)
    {
        var order = SelectedOrder; return order is null ? Task.CompletedTask : MutateAsync(() => commands.AddOrderLineAsync(new(order.OrderId, productId, quantity, price, order.RowVersion), token), value => { SelectedOrder = value; ReplaceOrder(value); Message = "Ligne ajoutée ou fusionnée."; return Task.CompletedTask; });
    }
    public Task UpdateLineAsync(PurchaseOrderLineView line, int quantity, decimal? price, CancellationToken token)
    {
        var order = SelectedOrder; return order is null ? Task.CompletedTask : MutateAsync(() => commands.UpdateOrderLineAsync(new(order.OrderId, line.LineId, quantity, price, order.RowVersion, line.RowVersion), token), value => { SelectedOrder = value; ReplaceOrder(value); Message = "Ligne mise à jour."; return Task.CompletedTask; });
    }
    public Task RemoveLineAsync(PurchaseOrderLineView line, CancellationToken token)
    {
        var order = SelectedOrder; return order is null ? Task.CompletedTask : MutateAsync(() => commands.RemoveOrderLineAsync(new(order.OrderId, line.LineId, order.RowVersion, line.RowVersion), token), value => { SelectedOrder = value; ReplaceOrder(value); Message = "Ligne retirée."; return Task.CompletedTask; });
    }
    public Task ConfirmOrderAsync(CancellationToken token)
    {
        var order = SelectedOrder; return order is null ? Task.CompletedTask : MutateAsync(() => commands.ConfirmOrderAsync(new(order.OrderId, order.RowVersion), token), value => { SelectedOrder = value; ReplaceOrder(value); Message = "Commande confirmée. Aucun stock n’a encore été modifié."; return Task.CompletedTask; });
    }
    public Task CancelOrderAsync(string reason, CancellationToken token)
    {
        var order = SelectedOrder; return order is null ? Task.CompletedTask : MutateAsync(() => commands.CancelOrderAsync(new(order.OrderId, reason, order.RowVersion), token), value => { SelectedOrder = value; ReplaceOrder(value); Message = "Commande annulée sans suppression."; return Task.CompletedTask; });
    }

    public void SelectReceipt(long id) => SelectedReceipt = Receipts.FirstOrDefault(x => x.ReceiptId == id);
    public Task FinalizeReceiptAsync(FinalizeGoodsReceiptCommand command, CancellationToken token) => MutateAsync(() => commands.FinalizeReceiptAsync(command, token), async value => { Message = $"Réception {value.ReceiptNumber} finalisée : stock et commande mis à jour atomiquement."; await ReloadAsync(token); SelectedReceipt = Receipts.FirstOrDefault(x => x.ReceiptId == value.ReceiptId); SelectedOrder = value.PurchaseOrderId is null ? SelectedOrder : Orders.FirstOrDefault(x => x.OrderId == value.PurchaseOrderId); });

    private async Task ReloadAsync(CancellationToken token)
    {
        var suppliers = await queries.SearchSuppliersAsync(token: token); var orders = await queries.ListOrdersAsync(token: token); var receipts = await queries.ListReceiptsAsync(token: token);
        if (Apply(suppliers)) Suppliers = suppliers.Value; if (Apply(orders)) Orders = orders.Value; if (Apply(receipts)) Receipts = receipts.Value;
    }
    private void ReplaceOrder(PurchaseOrderView value) => Orders = Orders.Select(x => x.OrderId == value.OrderId ? value : x).ToArray();
    private bool Apply<T>(Result<T> result) { if (result.IsSuccess) return true; SetError(result.Error); return false; }
    private void SetError(Error error) { ErrorCode = error.Code; Message = error.Code switch { "purchasing.receipt.database_unavailable" => "SQL Purchasing est indisponible. Aucun fallback synthétique n’est utilisé.", "purchasing.supplier.concurrency_conflict" or "purchasing.order.concurrency_conflict" or "purchasing.receipt.concurrency_conflict" => "Les données ont changé. Rechargez avant de recommencer.", "purchasing.receipt.duplicate_delivery_note" => "Ce numéro de bon de livraison existe déjà pour ce fournisseur.", "purchasing.receipt.quantity_exceeds_order" => "La quantité dépasse le reliquat de la commande. Aucune écriture n’a été conservée.", "purchasing.receipt.operation_conflict" => "Cette opération a déjà été utilisée avec un contenu différent.", "purchasing.receipt.invalid_lot" => "Le lot est absent ou incompatible avec le produit.", "purchasing.receipt.invalid_expiry" => "La péremption est requise et doit être valide à la réception.", _ => error.Message }; }
    private async Task RunAsync(Func<Task> action) { if (IsBusy) return; IsBusy = true; ErrorCode = null; Message = null; try { await action(); } finally { IsBusy = false; } }
    private Task MutateAsync<T>(Func<Task<Result<T>>> action, Func<T, Task> success) => RunAsync(async () => { var result = await action(); if (!Apply(result)) return; await success(result.Value); });
}
