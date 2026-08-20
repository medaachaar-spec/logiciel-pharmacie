using Pharmacy.Application.Purchasing.Commands;
using Pharmacy.Application.Purchasing.Contracts;
using Pharmacy.Application.Purchasing.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Purchasing;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class PurchasingGateCoreTests
{
    private static readonly DateTime Now = new(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string RowVersion = Convert.ToBase64String(new byte[8]);

    [Fact]
    public async Task PUR005_InvalidLengthsAndDecimalPrecisionAreRejectedBeforeTheStore()
    {
        var store = new RecordingStore();
        var handler = new PurchasingCommandHandlers(store);
        var token = TestContext.Current.CancellationToken;

        Assert.Equal("purchasing.validation.failed", (await handler.CreateSupplierAsync(
            new("SUP", "Fournisseur", new string('1', 33)), token)).Error.Code);
        Assert.Equal("purchasing.validation.failed", (await handler.CreateOrderAsync(
            new(1, new string('P', 65), new DateOnly(2026, 8, 17)), token)).Error.Code);
        Assert.Equal("purchasing.validation.failed", (await handler.CreateOrderAsync(
            new(1, "PO-VALID", new DateOnly(2026, 8, 17), Notes: new string('N', 1001)), token)).Error.Code);
        Assert.Equal("purchasing.validation.failed", (await handler.AddOrderLineAsync(
            new(1, 1, 1, 10.12345m, RowVersion), token)).Error.Code);
        Assert.Equal("purchasing.validation.failed", (await handler.FinalizeReceiptAsync(new(
            Guid.NewGuid(), 1, null, 1, new string('B', 129), new DateOnly(2026, 8, 17), null,
            [new(1, null, 1, 10.12345m, null, null, null)]), token)).Error.Code);

        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task PUR013_ZeroOrNegativeReceiptQuantityIsRejectedBeforeTheStore()
    {
        var store = new RecordingStore();
        var handler = new PurchasingCommandHandlers(store);
        var token = TestContext.Current.CancellationToken;
        foreach (var quantity in new[] { 0, -1 })
        {
            var result = await handler.FinalizeReceiptAsync(new(
                Guid.NewGuid(), 1, null, 1, "BL-QUANTITY", new DateOnly(2026, 8, 17), null,
                [new(1, null, quantity, 1m, null, null, null)]), token);
            Assert.Equal("purchasing.validation.failed", result.Error.Code);
        }
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task PUR029_BackendRbacProtectsOrdersAndRequiresRecentReauthenticationForReceipts()
    {
        var deniedStore = new RecordingStore();
        var denied = new PurchasingCommandHandlers(deniedStore, Authorizer([], Now));
        var token = TestContext.Current.CancellationToken;
        var deniedOrder = await denied.CreateOrderAsync(new(1, "PO-DENIED", new DateOnly(2026, 8, 17)), token);
        Assert.Equal("security.authorization.denied", deniedOrder.Error.Code);
        Assert.Equal(0, deniedStore.CallCount);

        var staleStore = new RecordingStore();
        var stale = new PurchasingCommandHandlers(staleStore, Authorizer([SecurityPermissions.PurchasingReceive], Now.AddMinutes(-10)));
        var staleReceipt = await stale.FinalizeReceiptAsync(ValidReceipt(), token);
        Assert.Equal("security.authorization.reauthentication_required", staleReceipt.Error.Code);
        Assert.Equal(0, staleStore.CallCount);

        var recentStore = new RecordingStore();
        var recent = new PurchasingCommandHandlers(recentStore, Authorizer([SecurityPermissions.PurchasingReceive], Now));
        var authorizedReceipt = await recent.FinalizeReceiptAsync(ValidReceipt(), token);
        Assert.Equal("purchasing.validation.failed", authorizedReceipt.Error.Code);
        Assert.Equal(1, recentStore.CallCount);
    }

    private static FinalizeGoodsReceiptCommand ValidReceipt() => new(
        Guid.NewGuid(), 1, null, 1, "BL-RBAC", new DateOnly(2026, 8, 17), null,
        [new(1, null, 1, 1m, null, null, null)]);

    private static IPermissionAuthorizer Authorizer(IReadOnlyCollection<string> permissions, DateTime lastReauthenticated)
    {
        var clock = new FixedSecurityClock();
        var session = new CurrentSecuritySession();
        session.Establish(new UserSessionView(
            1200, "owner.pur", "Owner PUR", ["OWNER"],
            new HashSet<string>(permissions, StringComparer.Ordinal), Now, lastReauthenticated), lastReauthenticated);
        return new PermissionAuthorizer(session, clock);
    }

    private sealed class FixedSecurityClock : ISecurityClock { public DateTime UtcNow => Now; }

    private sealed class RecordingStore : IPurchasingStore
    {
        public int CallCount { get; private set; }
        private Task<Result<T>> Called<T>()
        {
            CallCount++;
            return Task.FromResult(Result<T>.Failure(PurchasingErrors.ValidationFailed));
        }
        public Task<Result<SupplierView>> CreateSupplierAsync(CreateSupplierCommand command, CancellationToken cancellationToken = default) => Called<SupplierView>();
        public Task<Result<SupplierView>> UpdateSupplierAsync(UpdateSupplierCommand command, CancellationToken cancellationToken = default) => Called<SupplierView>();
        public Task<Result<SupplierView>> SetSupplierActivationAsync(SetSupplierActivationCommand command, CancellationToken cancellationToken = default) => Called<SupplierView>();
        public Task<Result<PurchaseOrderView>> CreateOrderAsync(CreatePurchaseOrderCommand command, CancellationToken cancellationToken = default) => Called<PurchaseOrderView>();
        public Task<Result<PurchaseOrderView>> AddOrderLineAsync(AddPurchaseOrderLineCommand command, CancellationToken cancellationToken = default) => Called<PurchaseOrderView>();
        public Task<Result<PurchaseOrderView>> UpdateOrderLineAsync(UpdatePurchaseOrderLineCommand command, CancellationToken cancellationToken = default) => Called<PurchaseOrderView>();
        public Task<Result<PurchaseOrderView>> RemoveOrderLineAsync(RemovePurchaseOrderLineCommand command, CancellationToken cancellationToken = default) => Called<PurchaseOrderView>();
        public Task<Result<PurchaseOrderView>> ConfirmOrderAsync(ConfirmPurchaseOrderCommand command, CancellationToken cancellationToken = default) => Called<PurchaseOrderView>();
        public Task<Result<PurchaseOrderView>> CancelOrderAsync(CancelPurchaseOrderCommand command, CancellationToken cancellationToken = default) => Called<PurchaseOrderView>();
        public Task<Result<GoodsReceiptView>> FinalizeReceiptAsync(FinalizeGoodsReceiptCommand command, CancellationToken cancellationToken = default) => Called<GoodsReceiptView>();
    }
}
