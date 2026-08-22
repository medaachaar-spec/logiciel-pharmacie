using System.Data;
using Microsoft.Data.SqlClient;
using Pharmacy.Application.Purchasing.Commands;
using Pharmacy.Application.Purchasing.Models;
using Pharmacy.Application.Purchasing.Queries;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Infrastructure.Purchasing;
using Pharmacy.Infrastructure.Security;
using Pharmacy.Domain.Purchasing;
using Xunit;

namespace Pharmacy.IntegrationTests;

/// <summary>
/// ZCODE-04A / ZC-003: backend authorization matrix for the real Application
/// Purchasing operations. A Seller-like user WITHOUT purchasing.view and
/// WITHOUT purchasing.receive calls the real handlers wired exactly like the
/// production DI (PurchasingQueryHandlers over SqlPurchasingReader,
/// PurchasingCommandHandlers over SqlPurchasingStore with a real authorizer).
/// Expected per the application contract "UI hidden + backend denied".
/// </summary>
[Collection("Sprint 09 persistence serial")]
public sealed class Zc04PurchasingAuthorizationTests
{
    private const string OwnerPassword = "Zc04-Owner-Password-2026!";
    private const string SellerPassword = "Zc04-Seller-Pass-2026!";

    [Fact]
    public async Task ZC04A03A_SellerWithoutPurchasingViewMustNotListPurchaseOrders()
    {
        var token = TestContext.Current.CancellationToken;
        var environment = await PurchasingAuthEnvironment.CreateAsync(token);
        try
        {
            var seller = environment.SellerSession;
            var queries = new PurchasingQueryHandlers(
                new SqlPurchasingReader(environment.Factory),
                new PermissionAuthorizer(environment.SellerSession, environment.Clock));

            var orders = await queries.ListOrdersAsync(null, null, token);

            // INVARIANT: without purchasing.view, purchasing reads must be denied.
            // RED = real purchase-order data returned to an unauthorized Seller.
            if (orders.IsSuccess)
            {
                Assert.Fail(
                    $"ZC-003 REPRODUCED (A03A): unauthorized Seller listed purchase orders " +
                    $"({orders.Value.Count} orders returned, first = " +
                    (orders.Value.Count > 0 ? orders.Value[0].OrderNumber : "none") + ").");
            }

            Assert.Equal("security.authorization.denied", orders.Error.Code);
        }
        finally
        {
            await environment.DisposeAsync();
        }
    }

    [Fact]
    public async Task ZC04A03B_SellerWithoutPurchasingViewMustNotReadOrderDetails()
    {
        var token = TestContext.Current.CancellationToken;
        var environment = await PurchasingAuthEnvironment.CreateAsync(token);
        try
        {
            var queries = new PurchasingQueryHandlers(
                new SqlPurchasingReader(environment.Factory),
                new PermissionAuthorizer(environment.SellerSession, environment.Clock));

            var order = await queries.GetOrderAsync(environment.OrderId, token);
            if (order.IsSuccess)
            {
                Assert.Fail(
                    $"ZC-003 REPRODUCED (A03B): unauthorized Seller read purchase-order details " +
                    $"({order.Value.OrderNumber}, status {order.Value.Status}, " +
                    $"{order.Value.Lines.Count} lines).");
            }

            Assert.Equal("security.authorization.denied", order.Error.Code);

            var supplier = await queries.GetSupplierAsync(environment.SupplierId, token);
            if (supplier.IsSuccess)
            {
                Assert.Fail(
                    $"ZC-003 REPRODUCED (A03B): unauthorized Seller read supplier details ({supplier.Value.Name}).");
            }
            Assert.Equal("security.authorization.denied", supplier.Error.Code);
        }
        finally
        {
            await environment.DisposeAsync();
        }
    }

    [Fact]
    public async Task ZC04A03C_SellerWithoutPurchasingViewMustNotReadSuppliersOrGoodsReceipts()
    {
        var token = TestContext.Current.CancellationToken;
        var environment = await PurchasingAuthEnvironment.CreateAsync(token);
        try
        {
            var queries = new PurchasingQueryHandlers(
                new SqlPurchasingReader(environment.Factory),
                new PermissionAuthorizer(environment.SellerSession, environment.Clock));

            var suppliers = await queries.SearchSuppliersAsync(null, true, token);
            if (suppliers.IsSuccess)
            {
                Assert.Fail(
                    $"ZC-003 REPRODUCED (A03C): unauthorized Seller listed suppliers ({suppliers.Value.Count} rows).");
            }
            Assert.Equal("security.authorization.denied", suppliers.Error.Code);

            var receipts = await queries.ListReceiptsAsync(null, 50, token);
            if (receipts.IsSuccess)
            {
                Assert.Fail(
                    $"ZC-003 REPRODUCED (A03C): unauthorized Seller listed goods receipts ({receipts.Value.Count} rows).");
            }
            Assert.Equal("security.authorization.denied", receipts.Error.Code);

            var receipt = await queries.GetReceiptAsync(environment.ReceiptId, token);
            if (receipt.IsSuccess)
            {
                Assert.Fail(
                    $"ZC-003 REPRODUCED (A03C): unauthorized Seller read goods-receipt details " +
                    $"({receipt.Value.ReceiptNumber}).");
            }
            Assert.Equal("security.authorization.denied", receipt.Error.Code);

            var products = await queries.SearchProductsAsync("ZC04", 30, token);
            if (products.IsSuccess)
            {
                Assert.Fail(
                    $"ZC-003 REPRODUCED (A03C): unauthorized Seller searched purchasable products ({products.Value.Count} rows).");
            }
            Assert.Equal("security.authorization.denied", products.Error.Code);
        }
        finally
        {
            await environment.DisposeAsync();
        }
    }

    [Fact]
    public async Task ZC04A03D_SellerWithoutPurchasingReceiveMustNotMutatePurchasingAndSqlStaysUnchanged()
    {
        var token = TestContext.Current.CancellationToken;
        var environment = await PurchasingAuthEnvironment.CreateAsync(token);
        try
        {
            var sellerAuthorizer = new PermissionAuthorizer(environment.SellerSession, environment.Clock);
            var commands = new PurchasingCommandHandlers(
                new SqlPurchasingStore(environment.Factory, environment.Clock),
                sellerAuthorizer,
                new Zc04.NoOpAuditRecorder());

            var suppliersBefore = await Zc04.ScalarAsync<int>(environment.DatabaseConnection,
                "SELECT COUNT(*) FROM purchasing.Suppliers;");
            var receiptsBefore = await Zc04.ScalarAsync<int>(environment.DatabaseConnection,
                "SELECT COUNT(*) FROM purchasing.GoodsReceipts;");
            var ordersBefore = await Zc04.ScalarAsync<int>(environment.DatabaseConnection,
                "SELECT COUNT(*) FROM purchasing.PurchaseOrders;");

            var createSupplier = await commands.CreateSupplierAsync(
                new CreateSupplierCommand("ZC04-ROGUE", "Fournisseur pirate"), token);
            Assert.Equal("security.authorization.denied", createSupplier.Error.Code);

            var createOrder = await commands.CreateOrderAsync(
                new CreatePurchaseOrderCommand(environment.SupplierId, "ZC04-ROGUE-ORDER",
                    DateOnly.FromDateTime(environment.Clock.UtcNow), null, null), token);
            Assert.Equal("security.authorization.denied", createOrder.Error.Code);

            var finalizeReceipt = await commands.FinalizeReceiptAsync(
                new FinalizeGoodsReceiptCommand(
                    Guid.NewGuid(), environment.SupplierId, null, environment.LocationId,
                    "ZC04-ROGUE-BL", DateOnly.FromDateTime(environment.Clock.UtcNow), null,
                    new[] { new FinalizeGoodsReceiptLineCommand(environment.ProductId, null, 1, 5m, null, null, null) }),
                token);
            Assert.Equal("security.authorization.denied", finalizeReceipt.Error.Code);

            Assert.Equal(suppliersBefore, await Zc04.ScalarAsync<int>(environment.DatabaseConnection,
                "SELECT COUNT(*) FROM purchasing.Suppliers;"));
            Assert.Equal(receiptsBefore, await Zc04.ScalarAsync<int>(environment.DatabaseConnection,
                "SELECT COUNT(*) FROM purchasing.GoodsReceipts;"));
            Assert.Equal(ordersBefore, await Zc04.ScalarAsync<int>(environment.DatabaseConnection,
                "SELECT COUNT(*) FROM purchasing.PurchaseOrders;"));
        }
        finally
        {
            await environment.DisposeAsync();
        }
    }

    [Fact]
    public async Task ZC04A03E_Control_OwnerWithPurchasingPermissionsRetainsAccess()
    {
        var token = TestContext.Current.CancellationToken;
        var environment = await PurchasingAuthEnvironment.CreateAsync(token);
        try
        {
            var queries = new PurchasingQueryHandlers(
                new SqlPurchasingReader(environment.Factory),
                environment.OwnerAuthorizer);
            var orders = await queries.ListOrdersAsync(null, null, token);
            Assert.True(orders.IsSuccess, orders.IsFailure ? orders.Error.Code : null);
            Assert.Contains(orders.Value, order => order.OrderId == environment.OrderId);
        }
        finally
        {
            await environment.DisposeAsync();
        }
    }

    /// <summary>
    /// Seeds one disposable database with: one supplier, one confirmed purchase order
    /// (ordered 10), one finalized goods receipt, plus a Seller session that has
    /// sales.create/catalog.view/inventory.view/customers.view but NO purchasing
    /// permissions (exact SELLER preset).
    /// </summary>
    private sealed class PurchasingAuthEnvironment : IAsyncDisposable
    {
        public required Zc04.Factory Factory { get; init; }
        public required Zc04.Zc04Clock Clock { get; init; }
        public required string DatabaseConnection { get; init; }
        public required string ServerConnection { get; init; }
        public required string DatabaseName { get; init; }
        public required CurrentSecuritySession SellerSession { get; init; }
        public required PermissionAuthorizer OwnerAuthorizer { get; init; }
        public required long SupplierId { get; init; }
        public required long OrderId { get; init; }
        public required long ReceiptId { get; init; }
        public required long ProductId { get; init; }
        public required long LocationId { get; init; }

        public static async Task<PurchasingAuthEnvironment> CreateAsync(CancellationToken token)
        {
            var server = Zc04.BuildServerConnection();
            var databaseName = Zc04.NewDatabaseName();
            var databaseConnection = Zc04.BuildDatabaseConnection(server.ConnectionString, databaseName);
            await Zc04.CreateDatabaseAsync(server.ConnectionString, databaseName);
            try
            {
                await Zc04.MigrateAsync(databaseConnection);
                var factory = new Zc04.Factory(databaseConnection);
                var clock = new Zc04.Zc04Clock(new DateTime(2026, 8, 22, 10, 0, 0, DateTimeKind.Utc));
                var owner = await Zc04.BootstrapOwnerAsync(factory, clock, "zc04.owner.pur", OwnerPassword);

                var seeded = await SeededSaleFactory.SeedProductWithStockAsync(factory, clock, "PUR", token: token);

                var store = new SqlPurchasingStore(factory, clock);
                var ownerCommands = new PurchasingCommandHandlers(store, owner.Authorizer, new Zc04.NoOpAuditRecorder());
                var supplier = await ownerCommands.CreateSupplierAsync(
                    new CreateSupplierCommand("ZC04-SUP", "Fournisseur ZC04", "0600000000", null, null), token);
                Assert.True(supplier.IsSuccess, supplier.IsFailure ? supplier.Error.Code : null);

                var order = await ownerCommands.CreateOrderAsync(
                    new CreatePurchaseOrderCommand(supplier.Value.SupplierId, "ZC04-ORDER-1",
                        DateOnly.FromDateTime(clock.UtcNow), null, null), token);
                Assert.True(order.IsSuccess, order.IsFailure ? order.Error.Code : null);
                var line = await ownerCommands.AddOrderLineAsync(
                    new AddPurchaseOrderLineCommand(order.Value.OrderId, seeded.ProductId, 10, 5m, order.Value.RowVersion), token);
                Assert.True(line.IsSuccess, line.IsFailure ? line.Error.Code : null);
                var confirmed = await ownerCommands.ConfirmOrderAsync(
                    new ConfirmPurchaseOrderCommand(order.Value.OrderId, line.Value.RowVersion), token);
                Assert.True(confirmed.IsSuccess, confirmed.IsFailure ? confirmed.Error.Code : null);

                // FinalizeReceiptAsync requires a recent reauthentication for the owner.
                var reauthenticated = await owner.Authentication.ReauthenticateAsync(OwnerPassword, Guid.NewGuid(), token);
                Assert.True(reauthenticated.IsSuccess, reauthenticated.IsFailure ? reauthenticated.Error.Code : null);
                var receipt = await ownerCommands.FinalizeReceiptAsync(
                    new FinalizeGoodsReceiptCommand(
                        Guid.NewGuid(), supplier.Value.SupplierId, confirmed.Value.OrderId, seeded.LocationId,
                        "ZC04-BL-1", DateOnly.FromDateTime(clock.UtcNow), confirmed.Value.RowVersion,
                        new[] { new FinalizeGoodsReceiptLineCommand(seeded.ProductId, confirmed.Value.Lines[0].LineId, 4, 5m, null, null, null) }),
                    token);
                Assert.True(receipt.IsSuccess, receipt.IsFailure ? receipt.Error.Code : null);

                // Seller with the exact SELLER preset (no purchasing.view / purchasing.receive).
                var permissionViews = (await owner.Administration.GetPermissionsAsync(token)).Value;
                var sellerPermissionIds = permissionViews
                    .Where(permission => SecurityRolePresets.SellerPermissions.Contains(permission.Code, StringComparer.Ordinal))
                    .Select(permission => permission.PermissionId)
                    .ToArray();
                var role = await owner.Administration.CreateRoleAsync(new CreateSecurityRoleRequest(
                    Guid.NewGuid(), "ZC04-SELL-PUR", "Vendeur ZC04", sellerPermissionIds), token);
                Assert.True(role.IsSuccess, role.IsFailure ? role.Error.Code : null);
                var seller = await owner.Administration.CreateUserAsync(new CreateSecurityUserRequest(
                    Guid.NewGuid(), "ZC04SELLP", "Vendeur PUR", SellerPassword,
                    new[] { role.Value.RoleId }), token);
                Assert.True(seller.IsSuccess, seller.IsFailure ? seller.Error.Code : null);

                var sellerSession = new CurrentSecuritySession();
                var sellerAuthentication = new SqlAuthenticationService(
                    factory, Zc04.CreateHasher(), new Zc04.SequentialRecoveryGenerator(), sellerSession, clock);
                var login = await sellerAuthentication.LoginAsync(
                    new LoginRequest("ZC04SELLP", SellerPassword, Guid.NewGuid()), token);
                Assert.True(login.IsSuccess, login.IsFailure ? login.Error.Code : null);
                Assert.DoesNotContain(SecurityPermissions.PurchasingView, sellerSession.User!.Permissions);
                Assert.DoesNotContain(SecurityPermissions.PurchasingReceive, sellerSession.User!.Permissions);

                return new PurchasingAuthEnvironment
                {
                    Factory = factory,
                    Clock = clock,
                    DatabaseConnection = databaseConnection,
                    ServerConnection = server.ConnectionString,
                    DatabaseName = databaseName,
                    SellerSession = sellerSession,
                    OwnerAuthorizer = owner.Authorizer,
                    SupplierId = supplier.Value.SupplierId,
                    OrderId = confirmed.Value.OrderId,
                    ReceiptId = receipt.Value.ReceiptId,
                    ProductId = seeded.ProductId,
                    LocationId = seeded.LocationId
                };
            }
            catch
            {
                Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
                await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(ServerConnection, DatabaseName);
        }
    }
}
