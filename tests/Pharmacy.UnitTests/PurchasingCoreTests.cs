using Pharmacy.Application.Purchasing.Commands;
using Pharmacy.Application.Purchasing.Contracts;
using Pharmacy.Application.Purchasing.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Purchasing;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class PurchasingCoreTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void QuantityMustBeStrictlyPositive(int quantity) => Assert.Equal("purchasing.order.quantity_invalid", PurchaseRules.ValidateQuantity(quantity).Error.Code);

    [Fact]
    public void PurchasePriceCannotBeNegative() => Assert.Equal("purchasing.validation.failed", PurchaseRules.ValidateUnitPurchasePrice(-0.0001m).Error.Code);

    [Fact]
    public void OnlyDraftOrdersCanBeEdited()
    {
        Assert.True(PurchaseRules.ValidateDraftMutation(PurchaseOrderStatus.Draft).IsSuccess);
        Assert.Equal("purchasing.order.invalid_status", PurchaseRules.ValidateDraftMutation(PurchaseOrderStatus.Confirmed).Error.Code);
    }

    [Fact]
    public void ReceiptStatusIsCalculatedFromCumulativeQuantities()
    {
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, PurchaseRules.CalculateReceiptStatus([(10, 4), (2, 2)]));
        Assert.Equal(PurchaseOrderStatus.FullyReceived, PurchaseRules.CalculateReceiptStatus([(10, 10), (2, 2)]));
    }

    [Fact]
    public async Task EmptyReceiptOperationIdIsRejectedBeforeStore()
    {
        var store = new RejectUnexpectedStore();
        var handler = new PurchasingCommandHandlers(store);
        var result = await handler.FinalizeReceiptAsync(new(Guid.Empty, 1, null, 1, "BL-1", new DateOnly(2026, 8, 10), null, [new(1, null, 1, 1m, null, null, null)]), TestContext.Current.CancellationToken);
        Assert.Equal("purchasing.validation.failed", result.Error.Code);
        Assert.False(store.WasCalled);
    }

    [Theory]
    [InlineData("purchasing.supplier.not_found")]
    [InlineData("purchasing.receipt.operation_conflict")]
    [InlineData("purchasing.receipt.database_unavailable")]
    public void StableErrorsRemainNamespaced(string code)
    {
        var values = typeof(PurchasingErrors).GetProperties().Where(x => x.PropertyType == typeof(Error)).Select(x => ((Error)x.GetValue(null)!).Code).ToArray();
        Assert.Contains(code, values);
        Assert.All(values, value => Assert.StartsWith("purchasing.", value, StringComparison.Ordinal));
    }

    private sealed class RejectUnexpectedStore : IPurchasingStore
    {
        public bool WasCalled { get; private set; }
        private Task<Result<T>> Unexpected<T>() { WasCalled = true; throw new InvalidOperationException("Le store ne devait pas être appelé."); }
        public Task<Result<SupplierView>> CreateSupplierAsync(CreateSupplierCommand command, CancellationToken cancellationToken = default) => Unexpected<SupplierView>();
        public Task<Result<SupplierView>> UpdateSupplierAsync(UpdateSupplierCommand command, CancellationToken cancellationToken = default) => Unexpected<SupplierView>();
        public Task<Result<SupplierView>> SetSupplierActivationAsync(SetSupplierActivationCommand command, CancellationToken cancellationToken = default) => Unexpected<SupplierView>();
        public Task<Result<PurchaseOrderView>> CreateOrderAsync(CreatePurchaseOrderCommand command, CancellationToken cancellationToken = default) => Unexpected<PurchaseOrderView>();
        public Task<Result<PurchaseOrderView>> AddOrderLineAsync(AddPurchaseOrderLineCommand command, CancellationToken cancellationToken = default) => Unexpected<PurchaseOrderView>();
        public Task<Result<PurchaseOrderView>> UpdateOrderLineAsync(UpdatePurchaseOrderLineCommand command, CancellationToken cancellationToken = default) => Unexpected<PurchaseOrderView>();
        public Task<Result<PurchaseOrderView>> RemoveOrderLineAsync(RemovePurchaseOrderLineCommand command, CancellationToken cancellationToken = default) => Unexpected<PurchaseOrderView>();
        public Task<Result<PurchaseOrderView>> ConfirmOrderAsync(ConfirmPurchaseOrderCommand command, CancellationToken cancellationToken = default) => Unexpected<PurchaseOrderView>();
        public Task<Result<PurchaseOrderView>> CancelOrderAsync(CancelPurchaseOrderCommand command, CancellationToken cancellationToken = default) => Unexpected<PurchaseOrderView>();
        public Task<Result<GoodsReceiptView>> FinalizeReceiptAsync(FinalizeGoodsReceiptCommand command, CancellationToken cancellationToken = default) => Unexpected<GoodsReceiptView>();
    }
}
