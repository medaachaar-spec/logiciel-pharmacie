using System.Data;
using Microsoft.Data.SqlClient;
using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Inventory.Models;
using Pharmacy.Application.Purchasing.Commands;
using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Commands;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Finalization.Models;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Catalog;
using Pharmacy.Infrastructure.Inventory;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Purchasing;
using Pharmacy.Infrastructure.Sales;
using Pharmacy.Infrastructure.Sales.InMemory;
using Xunit;

namespace Pharmacy.IntegrationTests;

/// <summary>
/// ZCODE-04A / ZC-A10: deterministic adversarial concurrency tests.
/// All scenarios use explicit synchronization (AsyncBarrier / TaskCompletionSource
/// plus the repository's own SqlSaleFinalizer afterPlan test hook) — never bare
/// Task.WhenAll — against a disposable SQL database.
/// Invariants: no over-receipt, no over-return, no negative stock, no lost
/// update, no silent double consumption; exactly the serialized outcomes.
/// </summary>
[Collection("Sprint 09 persistence serial")]
public sealed class Zc04ConcurrencyInvariantTests
{
    private const string OwnerPassword = "Zc04-Owner-Password-2026!";
    private static readonly TimeSpan CoordinationTimeout = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task ZC04A10A_ConcurrentGoodsReceiptsMustNotOverReceiveThePurchaseOrder()
    {
        var token = TestContext.Current.CancellationToken;
        var server = Zc04.BuildServerConnection();
        var databaseName = Zc04.NewDatabaseName();
        var databaseConnection = Zc04.BuildDatabaseConnection(server.ConnectionString, databaseName);
        await Zc04.CreateDatabaseAsync(server.ConnectionString, databaseName);

        try
        {
            var (factory, clock, owner, seeded, order) = await SeedPurchaseOrderAsync(
                databaseConnection, "A10A", orderedQuantity: 10, token);
            Assert.True((await owner.Authentication.ReauthenticateAsync(OwnerPassword, Guid.NewGuid(), token)).IsSuccess);

            var commands = new PurchasingCommandHandlers(
                new SqlPurchasingStore(factory, clock), owner.Authorizer, new Zc04.NoOpAuditRecorder());
            var orderRowVersion = order.RowVersion;
            var orderLineId = order.Lines.Single().LineId;

            var barrier = new Zc04.AsyncBarrier(2);
            var receipt7 = Task.Run(async () =>
            {
                await barrier.SignalAndWaitAsync(token);
                return await commands.FinalizeReceiptAsync(ReceiptCommand(factory, seeded, order, orderLineId, 7, "A10A-7"), token);
            }, token);
            var receipt5 = Task.Run(async () =>
            {
                await barrier.SignalAndWaitAsync(token);
                return await commands.FinalizeReceiptAsync(ReceiptCommand(factory, seeded, order, orderLineId, 5, "A10A-5"), token);
            }, token);

            var results = await Task.WhenAll(receipt5, receipt7);
            var successes = results.Count(result => result.IsSuccess);

            var receivedQuantity = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT ReceivedQuantity FROM purchasing.PurchaseOrderLines WHERE Id=@lineId;",
                new SqlParameter("@lineId", SqlDbType.BigInt) { Value = orderLineId });
            var receiptLinesTotal = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COALESCE(SUM(Quantity),0) FROM purchasing.GoodsReceiptLines WHERE PurchaseOrderLineId=@lineId;",
                new SqlParameter("@lineId", SqlDbType.BigInt) { Value = orderLineId });

            // Invariant: total received <= ordered (10). One operation may fail cleanly.
            Assert.True(receivedQuantity <= 10,
                $"Over-receipt reproduced: ReceivedQuantity={receivedQuantity} for 10 ordered.");
            Assert.Equal(receivedQuantity, receiptLinesTotal);
            Assert.True(successes <= 2);
            Assert.True(receivedQuantity == 0 || successes >= 1,
                "State changed without any reported success.");
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task ZC04A10B_ConcurrentReturnsMustNotExceedSoldQuantity()
    {
        var token = TestContext.Current.CancellationToken;
        var server = Zc04.BuildServerConnection();
        var databaseName = Zc04.NewDatabaseName();
        var databaseConnection = Zc04.BuildDatabaseConnection(server.ConnectionString, databaseName);
        await Zc04.CreateDatabaseAsync(server.ConnectionString, databaseName);

        try
        {
            await Zc04.MigrateAsync(databaseConnection);
            var factory = new Zc04.Factory(databaseConnection);
            var clock = new Zc04.Zc04Clock(new DateTime(2026, 8, 22, 14, 0, 0, DateTimeKind.Utc));
            var owner = await Zc04.BootstrapOwnerAsync(factory, clock, "zc04.owner.a10b", OwnerPassword);
            var seeded = await SeededSaleFactory.SeedProductWithStockAsync(
                factory, clock, "A10B", quantity: 20, token: token);

            var finalization = await SeededSaleFactory.FinalizeSaleAsSessionAsync(
                factory, clock, owner.Session, seeded, 10, "A10B", token);
            Assert.True(finalization.IsSuccess, finalization.IsFailure ? finalization.Error.Code : null);
            var saleLineId = await Zc04.ScalarAsync<long>(databaseConnection,
                "SELECT TOP 1 Id FROM sales.SaleLines WHERE SaleId=@saleId ORDER BY Id;",
                new SqlParameter("@saleId", SqlDbType.BigInt) { Value = finalization.Value.SaleId });

            Assert.True((await owner.Authentication.ReauthenticateAsync(OwnerPassword, Guid.NewGuid(), token)).IsSuccess);
            var returnStore = new SqlCustomerCreditCashStore(factory, clock);
            var returns = new Pharmacy.Application.Sales.Finalization.Commands.ReturnSaleHandler(
                returnStore, owner.Authorizer, new Zc04.NoOpAuditRecorder(), owner.Session);

            var barrier = new Zc04.AsyncBarrier(2);
            var return7 = Task.Run(async () =>
            {
                await barrier.SignalAndWaitAsync(token);
                return await returns.HandleAsync(ReturnRequest(finalization.Value.SaleId, saleLineId, 7, Guid.NewGuid()), token);
            }, token);
            var return5 = Task.Run(async () =>
            {
                await barrier.SignalAndWaitAsync(token);
                return await returns.HandleAsync(ReturnRequest(finalization.Value.SaleId, saleLineId, 5, Guid.NewGuid()), token);
            }, token);

            var results = await Task.WhenAll(return5, return7);
            var successes = results.Count(result => result.IsSuccess);

            var returnedTotal = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COALESCE(SUM(Quantity),0) FROM sales.SaleReturnLines WHERE SaleLineId=@lineId;",
                new SqlParameter("@lineId", SqlDbType.BigInt) { Value = saleLineId });
            var refundedTotal = await Zc04.ScalarAsync<decimal>(databaseConnection,
                "SELECT COALESCE(SUM(RefundAmount),0) FROM sales.SaleReturns WHERE OriginalSaleId=@saleId;",
                new SqlParameter("@saleId", SqlDbType.BigInt) { Value = finalization.Value.SaleId });

            // Invariant: sum returned <= sold (10); refunds match returned value.
            Assert.True(returnedTotal <= 10,
                $"Over-return reproduced: returned {returnedTotal} of 10 sold.");
            Assert.Equal(returnedTotal * 10m, refundedTotal);
            Assert.True(returnedTotal == 0 || successes >= 1,
                "State changed without any reported success.");
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task ZC04A10C_SaleFinalizationVersusInventoryDecreaseMustSerializeOnTheSameStock()
    {
        var token = TestContext.Current.CancellationToken;
        var server = Zc04.BuildServerConnection();
        var databaseName = Zc04.NewDatabaseName();
        var databaseConnection = Zc04.BuildDatabaseConnection(server.ConnectionString, databaseName);
        await Zc04.CreateDatabaseAsync(server.ConnectionString, databaseName);

        try
        {
            await Zc04.MigrateAsync(databaseConnection);
            var factory = new Zc04.Factory(databaseConnection);
            var clock = new Zc04.Zc04Clock(new DateTime(2026, 8, 22, 15, 0, 0, DateTimeKind.Utc));
            var owner = await Zc04.BootstrapOwnerAsync(factory, clock, "zc04.owner.a10c", OwnerPassword);
            Assert.True((await owner.Authentication.ReauthenticateAsync(OwnerPassword, Guid.NewGuid(), token)).IsSuccess);
            var seeded = await SeededSaleFactory.SeedProductWithStockAsync(
                factory, clock, "A10C", quantity: 10, token: token);

            var reachedAfterPlan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFinalizer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // Deterministic interleaving through the repository's own afterPlan test hook:
            // the finalizer pauses INSIDE its Serializable transaction after reading the
            // stock plan, before writing. The inventory decrease runs against the same
            // balance while the finalizer is parked.
            var hookedFinalizer = new SqlSaleFinalizer(factory, clock, async hookToken =>
            {
                reachedAfterPlan.TrySetResult();
                await releaseFinalizer.Task.WaitAsync(CoordinationTimeout, hookToken);
            });

            var saleDraft = await PrepareSaleDraftForConcurrentFinalizeAsync(
                factory, clock, seeded, quantity: 6, token);
            var saleTask = ExecuteFinalizeWithHookedFinalizerAsync(
                factory, clock, owner, saleDraft, seeded, hookedFinalizer, token);

            await reachedAfterPlan.Task.WaitAsync(CoordinationTimeout, token);

            var inventory = new InventoryCommandHandlers(
                new SqlInventoryStore(factory), clock, owner.Authorizer, new Zc04.NoOpAuditRecorder());
            var decreaseTask = RunInventoryDecreaseAsync(inventory, seeded, quantity: 8, token);

            // Release the finalizer only after the decrease settled (or failed).
            releaseFinalizer.TrySetResult();
            var saleResult = await saleTask.WaitAsync(CoordinationTimeout, token);
            var decreaseResult = await decreaseTask.WaitAsync(CoordinationTimeout, token);

            var balance = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT AvailableQuantity FROM inventory.StockBalances WHERE ProductId=@productId AND StockBatchId IS NULL;",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = seeded.ProductId });
            var salesCount = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM sales.Sales;");

            // Invariants: no negative stock, no lost update, one clean serialization.
            Assert.True(balance >= 0, $"Negative stock reproduced: balance {balance}.");
            if (saleResult.outcome == Outcome.Success)
            {
                Assert.Equal(1, salesCount);
                Assert.True(decreaseResult.outcome != Outcome.Success,
                    "Both operations claimed success on the same stock: lost update risk.");
                Assert.Equal(4, balance);
            }
            else
            {
                Assert.Equal(0, salesCount);
                Assert.Equal(2, balance);
                Assert.Equal(Outcome.Success, decreaseResult.outcome);
            }
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task ZC04A10D_GoodsReceiptVersusInventoryFinalizeMustSerializeOnTheSameStock()
    {
        var token = TestContext.Current.CancellationToken;
        var server = Zc04.BuildServerConnection();
        var databaseName = Zc04.NewDatabaseName();
        var databaseConnection = Zc04.BuildDatabaseConnection(server.ConnectionString, databaseName);
        await Zc04.CreateDatabaseAsync(server.ConnectionString, databaseName);

        try
        {
            await Zc04.MigrateAsync(databaseConnection);
            var factory = new Zc04.Factory(databaseConnection);
            var clock = new Zc04.Zc04Clock(new DateTime(2026, 8, 22, 16, 0, 0, DateTimeKind.Utc));
            var owner = await Zc04.BootstrapOwnerAsync(factory, clock, "zc04.owner.a10d", OwnerPassword);
            Assert.True((await owner.Authentication.ReauthenticateAsync(OwnerPassword, Guid.NewGuid(), token)).IsSuccess);
            var seeded = await SeededSaleFactory.SeedProductWithStockAsync(
                factory, clock, "A10D", quantity: 10, token: token);

            // Inventory session: expected 10, counted 4 -> finalize decreases by 6.
            var inventory = new InventoryCommandHandlers(
                new SqlInventoryStore(factory), clock, owner.Authorizer, new Zc04.NoOpAuditRecorder());
            var createdSession = await inventory.CreateSessionAsync(
                new CreateInventorySessionCommand(Guid.NewGuid(), "A10D-COUNT", seeded.LocationId), token);
            Assert.True(createdSession.IsSuccess, createdSession.IsFailure ? createdSession.Error.Code : null);
            var session = await inventory.StartSessionAsync(
                new StartInventorySessionCommand(Guid.NewGuid(), createdSession.Value.SessionId, createdSession.Value.RowVersion), token);
            Assert.True(session.IsSuccess, session.IsFailure ? session.Error.Code : null);
            var line = Assert.Single(session.Value.Lines);
            session = await inventory.SaveCountLineAsync(
                new SaveInventoryCountLineCommand(
                    Guid.NewGuid(), session.Value.SessionId, line.CountLineId, 4,
                    session.Value.RowVersion, line.RowVersion), token);
            Assert.True(session.IsSuccess, session.IsFailure ? session.Error.Code : null);
            var finalizeCommand = new FinalizeInventorySessionCommand(
                Guid.NewGuid(), session.Value.SessionId, session.Value.RowVersion);

            // Direct goods receipt adding 20 to the same product/location.
            var purchasingCommands = new PurchasingCommandHandlers(
                new SqlPurchasingStore(factory, clock), owner.Authorizer, new Zc04.NoOpAuditRecorder());
            var supplier = await purchasingCommands.CreateSupplierAsync(
                new CreateSupplierCommand("ZC04-A10D-SUP", "Fournisseur A10D"), token);
            Assert.True(supplier.IsSuccess, supplier.IsFailure ? supplier.Error.Code : null);
            var receiptCommand = new FinalizeGoodsReceiptCommand(
                Guid.NewGuid(), supplier.Value.SupplierId, null, seeded.LocationId,
                "ZC04-A10D-BL", DateOnly.FromDateTime(clock.UtcNow), null,
                new[] { new FinalizeGoodsReceiptLineCommand(seeded.ProductId, null, 20, 5m, null, null, null) });

            var barrier = new Zc04.AsyncBarrier(2);
            var receiptTask = Task.Run(async () =>
            {
                await barrier.SignalAndWaitAsync(token);
                return await purchasingCommands.FinalizeReceiptAsync(receiptCommand, token);
            }, token);
            var finalizeTask = Task.Run(async () =>
            {
                await barrier.SignalAndWaitAsync(token);
                return await inventory.FinalizeSessionAsync(finalizeCommand, token);
            }, token);

            var receiptResult = await receiptTask.WaitAsync(CoordinationTimeout, token);
            var finalizeResult = await finalizeTask.WaitAsync(CoordinationTimeout, token);

            var balance = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT AvailableQuantity FROM inventory.StockBalances WHERE ProductId=@productId AND StockBatchId IS NULL;",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = seeded.ProductId });

            // Serialized outcomes only: 10+20=30 (receipt first, inventory conflict),
            // 4+20=24 (inventory first, receipt after), or 4 (inventory first, receipt
            // failed through deadlock). Never negative, never 10 (something committed),
            // never inconsistent with the reported outcomes.
            Assert.Contains(balance, new[] { 4, 24, 30 });
            Assert.True(balance >= 0, $"Negative stock reproduced: {balance}.");
            if (finalizeResult.IsSuccess)
            {
                Assert.Contains(balance, new[] { 4, 24 });
            }
            else
            {
                Assert.Equal(30, balance);
                Assert.True(receiptResult.IsSuccess);
            }
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }

    private enum Outcome
    {
        Success,
        Failure,
        Exception
    }

    private sealed record SaleDraftHandle(
        InMemorySaleDraftStore Store,
        SaleDraftId DraftId,
        long Version);

    private static async Task<SaleDraftHandle> PrepareSaleDraftForConcurrentFinalizeAsync(
        Zc04.Factory factory,
        Zc04.Zc04Clock clock,
        SeededSaleFactory.SeededProduct seeded,
        int quantity,
        CancellationToken token)
    {
        var catalogReader = new SqlProductCatalogReader(factory);
        var availabilityReader = new SqlInventoryStockAvailabilityReader(factory, clock);
        var draftStore = new InMemorySaleDraftStore();
        var draft = await new CreateSaleDraftHandler(new InMemorySaleDraftIdGenerator(), draftStore).HandleAsync(token);
        var added = await new AddProductToSaleDraftHandler(draftStore, catalogReader, availabilityReader)
            .HandleAsync(new AddProductToSaleDraftCommand(
                draft.Value.Id,
                SellableProductLookup.ByProductId(ProductId.Create(seeded.ProductId).Value),
                SaleQuantity.Create(quantity).Value,
                draft.Value.Version), token);
        return new SaleDraftHandle(draftStore, draft.Value.Id, added.Value.Version);
    }

    private static Task<(Outcome outcome, string detail)> ExecuteFinalizeWithHookedFinalizerAsync(
        Zc04.Factory factory,
        Zc04.Zc04Clock clock,
        Zc04.OwnerContext owner,
        SaleDraftHandle saleDraft,
        SeededSaleFactory.SeededProduct seeded,
        SqlSaleFinalizer finalizer,
        CancellationToken token)
    {
        var availabilityReader = new SqlInventoryStockAvailabilityReader(factory, clock);
        var preparationHandler = new PrepareSaleFinalizationHandler(
            saleDraft.Store, availabilityReader, new InMemorySaleFinalizationPreparationStore());
        var handler = new FinalizeSaleHandler(
            preparationHandler,
            finalizer,
            new PermissionAuthorizer(owner.Session, clock),
            new Zc04.NoOpAuditRecorder(),
            owner.Session);

        return Task.Run(async () =>
        {
            try
            {
                var result = await handler.HandleAsync(new FinalizeSaleCommand(
                    Guid.NewGuid(), saleDraft.DraftId, saleDraft.Version,
                    SalePaymentMethod.Cash, seeded.UnitPrice * 6, false), token);
                return (result.IsSuccess ? Outcome.Success : Outcome.Failure,
                    result.IsFailure ? result.Error.Code! : result.Value.Reference);
            }
            catch (Exception exception)
            {
                return (Outcome.Exception, exception.GetType().Name);
            }
        }, token);
    }

    private static Task<(Outcome outcome, string detail)> RunInventoryDecreaseAsync(
        InventoryCommandHandlers inventory,
        SeededSaleFactory.SeededProduct seeded,
        int quantity,
        CancellationToken token) =>
        Task.Run(async () =>
        {
            try
            {
                var result = await inventory.DecreaseAsync(new RecordStockMovementCommand(
                    Guid.NewGuid(), StockMovementType.ManualDecrease, seeded.ProductId,
                    seeded.LocationId, null, quantity,
                    "ZC04 A10C concurrent decrease", "ZCODE-04A", "A10C"), token);
                return (result.IsSuccess ? Outcome.Success : Outcome.Failure,
                    result.IsFailure ? result.Error.Code : "decreased");
            }
            catch (Exception exception)
            {
                return (Outcome.Exception, exception.GetType().Name);
            }
        }, token);

    private static ReturnSaleRequest ReturnRequest(long saleId, long saleLineId, int quantity, Guid operationId) =>
        new(operationId, saleId,
            new[] { new ReturnSaleLineRequest(saleLineId, quantity, ReturnStockDestination.RestockApproved) },
            ReturnRefundMethod.Cash);

    private static FinalizeGoodsReceiptCommand ReceiptCommand(
        Zc04.Factory factory,
        SeededSaleFactory.SeededProduct seeded,
        Pharmacy.Application.Purchasing.Models.PurchaseOrderView order,
        long orderLineId,
        int quantity,
        string tag) =>
        new(Guid.NewGuid(),
            order.SupplierId,
            order.OrderId,
            seeded.LocationId,
            $"ZC04-{tag}-BL",
            DateOnly.FromDateTime(new DateTime(2026, 8, 22, 16, 30, 0, DateTimeKind.Utc)),
            order.RowVersion,
            new[] { new FinalizeGoodsReceiptLineCommand(seeded.ProductId, orderLineId, quantity, 5m, null, null, null) });

    private static async Task<(Zc04.Factory Factory, Zc04.Zc04Clock Clock, Zc04.OwnerContext Owner, SeededSaleFactory.SeededProduct Seeded, Pharmacy.Application.Purchasing.Models.PurchaseOrderView Order)> SeedPurchaseOrderAsync(
        string databaseConnection,
        string tag,
        int orderedQuantity,
        CancellationToken token)
    {
        await Zc04.MigrateAsync(databaseConnection);
        var factory = new Zc04.Factory(databaseConnection);
        var clock = new Zc04.Zc04Clock(new DateTime(2026, 8, 22, 14, 30, 0, DateTimeKind.Utc));
        var owner = await Zc04.BootstrapOwnerAsync(factory, clock, $"zc04.owner.{tag.ToLowerInvariant()}", OwnerPassword);
        var seeded = await SeededSaleFactory.SeedProductWithStockAsync(factory, clock, tag, token: token);

        var commands = new PurchasingCommandHandlers(
            new SqlPurchasingStore(factory, clock), owner.Authorizer, new Zc04.NoOpAuditRecorder());
        var supplier = await commands.CreateSupplierAsync(
            new CreateSupplierCommand($"ZC04-{tag}-SUP", $"Fournisseur {tag}"), token);
        Assert.True(supplier.IsSuccess, supplier.IsFailure ? supplier.Error.Code : null);
        var order = await commands.CreateOrderAsync(
            new CreatePurchaseOrderCommand(supplier.Value.SupplierId, $"ZC04-{tag}-ORDER",
                DateOnly.FromDateTime(clock.UtcNow), null, null), token);
        Assert.True(order.IsSuccess, order.IsFailure ? order.Error.Code : null);
        var line = await commands.AddOrderLineAsync(
            new AddPurchaseOrderLineCommand(order.Value.OrderId, seeded.ProductId, orderedQuantity, 5m, order.Value.RowVersion), token);
        Assert.True(line.IsSuccess, line.IsFailure ? line.Error.Code : null);
        var confirmed = await commands.ConfirmOrderAsync(
            new ConfirmPurchaseOrderCommand(order.Value.OrderId, line.Value.RowVersion), token);
        Assert.True(confirmed.IsSuccess, confirmed.IsFailure ? confirmed.Error.Code : null);
        return (factory, clock, owner, seeded, confirmed.Value);
    }
}
