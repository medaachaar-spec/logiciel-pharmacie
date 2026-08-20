using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Purchasing.Commands;
using Pharmacy.Application.Purchasing.Contracts;
using Pharmacy.Application.Purchasing.Models;
using Pharmacy.Application.Purchasing.Queries;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Purchasing;
using Pharmacy.Infrastructure.Catalog;
using Pharmacy.Infrastructure.Inventory;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Purchasing;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class SqlPurchasingGateIntegrationTests(SqlPurchasingGateFixture fixture) : IClassFixture<SqlPurchasingGateFixture>
{
    [Fact]
    public async Task PUR001_SupplierCreationPersistsAndInactiveHistoryRemainsReadable()
    {
        var commands = Commands();
        var supplier = await SupplierAsync(commands, "SUP1");
        var rebuilt = Queries();
        var loaded = await rebuilt.GetSupplierAsync(supplier.SupplierId, Token);
        Assert.Equal((supplier.Code, supplier.Name, true), (loaded.Value.Code, loaded.Value.Name, loaded.Value.IsActive));

        var inactive = await commands.SetSupplierActivationAsync(new(supplier.SupplierId, false, supplier.RowVersion), Token);
        Assert.False(inactive.Value.IsActive);
        var historical = await new PurchasingQueryHandlers(new SqlPurchasingReader(fixture.Factory)).GetSupplierAsync(supplier.SupplierId, Token);
        Assert.False(historical.Value.IsActive);
    }

    [Fact]
    public async Task PUR002_DuplicateSupplierCodeIsRejectedAtomically()
    {
        var commands = Commands();
        var code = Code("DUP");
        Assert.True((await commands.CreateSupplierAsync(new(code, "Premier fournisseur"), Token)).IsSuccess);
        var duplicate = await commands.CreateSupplierAsync(new(code.ToLowerInvariant(), "Doublon"), Token);
        Assert.Equal("purchasing.supplier.code_duplicate", duplicate.Error.Code);
        Assert.Equal(1, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM purchasing.Suppliers WHERE Code=@code;", new SqlParameter("@code", code)));
    }

    [Fact]
    public async Task PUR003_TwoLinePurchaseOrderPersistsExactSnapshotsAndInitialStatus()
    {
        var commands = Commands();
        var supplier = await SupplierAsync(commands, "PO2");
        var productA = await ProductAsync("PO2A");
        var productB = await ProductAsync("PO2B");
        var order = await DraftOrderAsync(commands, supplier, [(productA, 10, 7m), (productB, 5, 14m)]);

        var loaded = await Queries().GetOrderAsync(order.OrderId, Token);
        Assert.Equal(PurchaseOrderStatus.Draft, loaded.Value.Status);
        Assert.Collection(loaded.Value.Lines.OrderBy(line => line.ProductId),
            line => Assert.Equal((10, 0, 10, 7m), (line.OrderedQuantity, line.ReceivedQuantity, line.RemainingQuantity, line.UnitPurchasePrice)),
            line => Assert.Equal((5, 0, 5, 14m), (line.OrderedQuantity, line.ReceivedQuantity, line.RemainingQuantity, line.UnitPurchasePrice)));
        Assert.Equal(2, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM purchasing.PurchaseOrderLines WHERE PurchaseOrderId=@id;", new SqlParameter("@id", order.OrderId)));
    }

    [Fact]
    public async Task PUR004_CreatingAndConfirmingOrderDoesNotAlterInventoryOrCreateReceipt()
    {
        var before = await fixture.InventoryCountsAsync();
        var commands = Commands();
        var supplier = await SupplierAsync(commands, "NOSTOCK");
        var order = await DraftOrderAsync(commands, supplier, [(await ProductAsync("NSA"), 10, 7m), (await ProductAsync("NSB"), 5, 14m)]);
        var confirmed = await commands.ConfirmOrderAsync(new(order.OrderId, order.RowVersion), Token);
        Assert.Equal(PurchaseOrderStatus.Confirmed, confirmed.Value.Status);
        Assert.Equal(before, await fixture.InventoryCountsAsync());
        Assert.Equal(0, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM purchasing.GoodsReceipts WHERE PurchaseOrderId=@id;", new SqlParameter("@id", order.OrderId)));
    }

    [Fact]
    public async Task PUR006_PartialReceiptOfTwoProductsCreatesOneReceiptAndTwoLines()
    {
        var commands = Commands();
        var scenario = await ConfirmedOrderAsync(commands, "PART2", [(10, 7m, false), (5, 14m, false)]);
        var receipt = await ReceiveAsync(commands, scenario.Order, scenario.Products, [4, 2], "PART2-1");
        var current = await Queries().GetOrderAsync(scenario.Order.OrderId, Token);

        Assert.True(receipt.IsSuccess, receipt.IsFailure ? receipt.Error.Code : null);
        Assert.Equal(2, receipt.Value.Lines.Count);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, current.Value.Status);
        Assert.Equal([4, 2], current.Value.Lines.OrderBy(line => line.ProductId).Select(line => line.ReceivedQuantity));
    }

    [Fact]
    public async Task PUR007_PartialReceiptCalculatesRemainingQuantitiesPerLineExactly()
    {
        var commands = Commands();
        var scenario = await ConfirmedOrderAsync(commands, "REMAIN", [(10, 7m, false), (5, 14m, false)]);
        Assert.True((await ReceiveAsync(commands, scenario.Order, scenario.Products, [4, 2], "REMAIN-1")).IsSuccess);
        var current = await Queries().GetOrderAsync(scenario.Order.OrderId, Token);
        Assert.Equal([6, 3], current.Value.Lines.OrderBy(line => line.ProductId).Select(line => line.RemainingQuantity));
    }

    [Fact]
    public async Task PUR008_PartialReceiptCreatesExactBatchesBalancesAndMovements()
    {
        var commands = Commands();
        var scenario = await ConfirmedOrderAsync(commands, "BATCH2", [(10, 7m, true), (5, 14m, true)]);
        var receipt = await ReceiveAsync(commands, scenario.Order, scenario.Products, [4, 2], "BATCH2-1", useLots: true);
        Assert.True(receipt.IsSuccess, receipt.IsFailure ? receipt.Error.Code : null);
        Assert.Equal(2, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM inventory.StockBatches WHERE ProductId IN (@a,@b);", new SqlParameter("@a", scenario.Products[0].Id), new SqlParameter("@b", scenario.Products[1].Id)));
        Assert.Equal(6m, await fixture.StockAsync(scenario.Products.Select(product => product.Id).ToArray()));
        Assert.Equal(2, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM inventory.StockMovements WHERE SourceReference=@reference AND MovementType=N'PurchaseReceipt';", new SqlParameter("@reference", receipt.Value.ReceiptNumber)));
    }

    [Fact]
    public async Task PUR009_SecondReceiptCompletesTwoProductOrderWithoutCumulativeError()
    {
        var commands = Commands();
        var scenario = await ConfirmedOrderAsync(commands, "COMPLETE", [(10, 7m, false), (5, 14m, false)]);
        Assert.True((await ReceiveAsync(commands, scenario.Order, scenario.Products, [4, 2], "COMPLETE-1")).IsSuccess);
        var partial = (await Queries().GetOrderAsync(scenario.Order.OrderId, Token)).Value;
        Assert.True((await ReceiveAsync(commands, partial, scenario.Products, [6, 3], "COMPLETE-2")).IsSuccess);
        var complete = (await Queries().GetOrderAsync(scenario.Order.OrderId, Token)).Value;
        Assert.Equal(PurchaseOrderStatus.FullyReceived, complete.Status);
        Assert.Equal([10, 5], complete.Lines.OrderBy(line => line.ProductId).Select(line => line.ReceivedQuantity));
        Assert.Equal(15m, await fixture.StockAsync(scenario.Products.Select(product => product.Id).ToArray()));
    }

    [Fact]
    public async Task PUR010_ThreePartialReceiptsAreCumulativeAndHistoricalReceiptsStayImmutable()
    {
        var commands = Commands();
        var scenario = await ConfirmedOrderAsync(commands, "THREE", [(10, 7m, false)]);
        var first = await ReceiveAsync(commands, scenario.Order, scenario.Products, [2], "THREE-1");
        var afterFirst = (await Queries().GetOrderAsync(scenario.Order.OrderId, Token)).Value;
        Assert.Equal(8, afterFirst.Lines[0].RemainingQuantity);
        Assert.True((await ReceiveAsync(commands, afterFirst, scenario.Products, [3], "THREE-2")).IsSuccess);
        var afterSecond = (await Queries().GetOrderAsync(scenario.Order.OrderId, Token)).Value;
        Assert.Equal((5, 5), (afterSecond.Lines[0].ReceivedQuantity, afterSecond.Lines[0].RemainingQuantity));
        Assert.True((await ReceiveAsync(commands, afterSecond, scenario.Products, [5], "THREE-3")).IsSuccess);
        var final = (await Queries().GetOrderAsync(scenario.Order.OrderId, Token)).Value;
        Assert.Equal((10, 0, PurchaseOrderStatus.FullyReceived), (final.Lines[0].ReceivedQuantity, final.Lines[0].RemainingQuantity, final.Status));
        Assert.Equal(2, (await Queries().GetReceiptAsync(first.Value.ReceiptId, Token)).Value.Lines[0].Quantity);
        Assert.Equal(3, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM purchasing.GoodsReceipts WHERE PurchaseOrderId=@id;", new SqlParameter("@id", scenario.Order.OrderId)));
    }

    [Fact]
    public async Task PUR011_OverReceiptIsRejectedWithNoMutationAnywhere()
    {
        var commands = Commands();
        var scenario = await ConfirmedOrderAsync(commands, "OVER", [(10, 7m, false)]);
        Assert.True((await ReceiveAsync(commands, scenario.Order, scenario.Products, [7], "OVER-1")).IsSuccess);
        var current = (await Queries().GetOrderAsync(scenario.Order.OrderId, Token)).Value;
        var before = await fixture.PurchasingStockCountsAsync();
        var rejected = await ReceiveAsync(commands, current, scenario.Products, [4], "OVER-2");
        Assert.Equal("purchasing.receipt.quantity_exceeds_order", rejected.Error.Code);
        Assert.Equal(before, await fixture.PurchasingStockCountsAsync());
        Assert.Equal((7, 3), ((await Queries().GetOrderAsync(current.OrderId, Token)).Value.Lines[0].ReceivedQuantity, (await Queries().GetOrderAsync(current.OrderId, Token)).Value.Lines[0].RemainingQuantity));
    }

    [Fact]
    public async Task PUR012_ExactRemainingQuantityIsAcceptedAndCompletesOrder()
    {
        var commands = Commands();
        var scenario = await ConfirmedOrderAsync(commands, "EXACT", [(10, 7m, false)]);
        Assert.True((await ReceiveAsync(commands, scenario.Order, scenario.Products, [7], "EXACT-1")).IsSuccess);
        var current = (await Queries().GetOrderAsync(scenario.Order.OrderId, Token)).Value;
        Assert.True((await ReceiveAsync(commands, current, scenario.Products, [3], "EXACT-2")).IsSuccess);
        var final = (await Queries().GetOrderAsync(current.OrderId, Token)).Value;
        Assert.Equal((10, 0, PurchaseOrderStatus.FullyReceived), (final.Lines[0].ReceivedQuantity, final.Lines[0].RemainingQuantity, final.Status));
    }

    [Fact]
    public async Task PUR014_ThreeProductMixedReceiptsKeepIndependentRemainingAndExactStock()
    {
        var commands = Commands();
        var scenario = await ConfirmedOrderAsync(commands, "MIX3", [(10, 7m, false), (8, 8m, false), (6, 9m, false)]);
        Assert.True((await ReceiveAsync(commands, scenario.Order, scenario.Products, [3, 8, 2], "MIX3-1")).IsSuccess);
        var partial = (await Queries().GetOrderAsync(scenario.Order.OrderId, Token)).Value;
        Assert.Equal([7, 0, 4], partial.Lines.OrderBy(line => line.ProductId).Select(line => line.RemainingQuantity));
        Assert.True((await ReceiveSelectedAsync(commands, partial, [scenario.Products[0], scenario.Products[2]], [7, 4], "MIX3-2")).IsSuccess);
        var final = (await Queries().GetOrderAsync(partial.OrderId, Token)).Value;
        Assert.Equal(PurchaseOrderStatus.FullyReceived, final.Status);
        Assert.Equal([10, 8, 6], final.Lines.OrderBy(line => line.ProductId).Select(line => line.ReceivedQuantity));
        Assert.Equal(24m, await fixture.StockAsync(scenario.Products.Select(product => product.Id).ToArray()));
        Assert.Equal(2, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM purchasing.GoodsReceipts WHERE PurchaseOrderId=@id;", new SqlParameter("@id", partial.OrderId)));
    }

    [Fact]
    public async Task PUR015_CompletedLineCannotBeReceivedAgainWhileOtherLinesRemainOpen()
    {
        var commands = Commands();
        var scenario = await ConfirmedOrderAsync(commands, "LINEFULL", [(10, 7m, false), (8, 8m, false)]);
        Assert.True((await ReceiveSelectedAsync(commands, scenario.Order, [scenario.Products[1]], [8], "LINEFULL-1")).IsSuccess);
        var partial = (await Queries().GetOrderAsync(scenario.Order.OrderId, Token)).Value;
        var before = await fixture.PurchasingStockCountsAsync();
        var rejected = await ReceiveSelectedAsync(commands, partial, [scenario.Products[1]], [1], "LINEFULL-2");
        Assert.Equal("purchasing.receipt.quantity_exceeds_order", rejected.Error.Code);
        Assert.Equal(before, await fixture.PurchasingStockCountsAsync());
    }

    [Fact]
    public async Task PUR016_SameOperationAndPayloadIsIdempotent()
    {
        var commands = Commands();
        var scenario = await ConfirmedOrderAsync(commands, "REPLAY", [(10, 7m, false)]);
        var operationId = Guid.NewGuid();
        var request = ReceiptCommand(scenario.Order, scenario.Products, [4], "REPLAY-1", operationId);
        var first = await commands.FinalizeReceiptAsync(request, Token);
        var replay = await commands.FinalizeReceiptAsync(request, Token);
        Assert.Equal(first.Value.ReceiptId, replay.Value.ReceiptId);
        Assert.Equal(1, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM purchasing.GoodsReceipts WHERE OperationId=@id;", new SqlParameter("@id", operationId)));
        Assert.Equal(4m, await fixture.StockAsync([scenario.Products[0].Id]));
    }

    [Fact]
    public async Task PUR017_SameOperationWithDifferentPayloadIsConflictWithoutSecondMutation()
    {
        var commands = Commands();
        var scenario = await ConfirmedOrderAsync(commands, "OPCONFLICT", [(10, 7m, false)]);
        var operationId = Guid.NewGuid();
        var request = ReceiptCommand(scenario.Order, scenario.Products, [4], "OPCONFLICT-1", operationId);
        Assert.True((await commands.FinalizeReceiptAsync(request, Token)).IsSuccess);
        var before = await fixture.PurchasingStockCountsAsync();
        var conflict = await commands.FinalizeReceiptAsync(request with { Lines = [request.Lines[0] with { Quantity = 5 }] }, Token);
        Assert.Equal("purchasing.receipt.operation_conflict", conflict.Error.Code);
        Assert.Equal(before, await fixture.PurchasingStockCountsAsync());
    }

    [Fact]
    public async Task PUR019_ConcurrentReceiptsCannotExceedOrderedQuantity()
    {
        var scenario = await ConfirmedOrderAsync(Commands(), "RACEQTY", [(10, 7m, false)]);
        var first = ReceiptCommand(scenario.Order, scenario.Products, [7], "RACEQTY-1", Guid.NewGuid());
        var second = ReceiptCommand(scenario.Order, scenario.Products, [5], "RACEQTY-2", Guid.NewGuid());
        var results = await Task.WhenAll(Commands().FinalizeReceiptAsync(first, Token), Commands().FinalizeReceiptAsync(second, Token));
        var current = (await Queries().GetOrderAsync(scenario.Order.OrderId, Token)).Value;
        Assert.InRange(current.Lines[0].ReceivedQuantity, 5, 7);
        Assert.Equal(current.Lines[0].ReceivedQuantity, await fixture.StockAsync([scenario.Products[0].Id]));
        Assert.Single(results, result => result.IsSuccess);
        Assert.All(results.Where(result => result.IsFailure), result => Assert.Contains(result.Error.Code,
            new[] { "purchasing.receipt.quantity_exceeds_order", "purchasing.receipt.concurrency_conflict" }));
    }

    [Fact]
    public async Task PUR020_ConcurrentReceiptsForSameBatchKeepOneBatchAndExactBalance()
    {
        var commands = Commands();
        var supplier = await SupplierAsync(commands, "RACELOT");
        var product = await ProductAsync("RACELOT", true);
        var expiry = fixture.Clock.CurrentDate.AddYears(1);
        var first = DirectCommand(supplier, product, 5, "RACELOT-1", Guid.NewGuid(), "LOT-RACE", expiry);
        var second = DirectCommand(supplier, product, 7, "RACELOT-2", Guid.NewGuid(), "LOT-RACE", expiry);
        var results = await Task.WhenAll(Commands().FinalizeReceiptAsync(first, Token), Commands().FinalizeReceiptAsync(second, Token));

        Assert.All(results, result => Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null));
        Assert.Equal(1, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM inventory.StockBatches WHERE ProductId=@id AND BatchNumber=N'LOT-RACE';", new SqlParameter("@id", product.Id)));
        Assert.Equal(12m, await fixture.StockAsync([product.Id]));
        Assert.Equal(2, await fixture.MovementCountAsync(product.Id));
    }

    [Fact]
    public async Task PUR021_DirectReceiptPersistsWithoutArtificialPurchaseOrder()
    {
        var commands = Commands();
        var supplier = await SupplierAsync(commands, "DIRECT");
        var product = await ProductAsync("DIRECT");
        var result = await commands.FinalizeReceiptAsync(DirectCommand(supplier, product, 5, "DIRECT-1", Guid.NewGuid()), Token);
        var loaded = await Queries().GetReceiptAsync(result.Value.ReceiptId, Token);
        Assert.Equal(GoodsReceiptType.Direct, loaded.Value.ReceiptType);
        Assert.Null(loaded.Value.PurchaseOrderId);
        Assert.Equal(0, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM purchasing.PurchaseOrders WHERE SupplierId=@id;", new SqlParameter("@id", supplier.SupplierId)));
    }

    [Fact]
    public async Task PUR022_DirectReceiptIncreasesStockExactlyOnceWithOneMovement()
    {
        var commands = Commands();
        var supplier = await SupplierAsync(commands, "DIRECTSTOCK");
        var product = await ProductAsync("DIRECTSTOCK");
        var receipt = await commands.FinalizeReceiptAsync(DirectCommand(supplier, product, 5, "DIRECTSTOCK-1", Guid.NewGuid()), Token);
        Assert.True(receipt.IsSuccess);
        Assert.Equal(5m, await fixture.StockAsync([product.Id]));
        Assert.Equal(1, await fixture.MovementCountAsync(product.Id));
    }

    [Fact]
    public async Task PUR023_DirectReceiptReplayDoesNotDuplicateStockOrMovement()
    {
        var commands = Commands();
        var supplier = await SupplierAsync(commands, "DIRECTREPLAY");
        var product = await ProductAsync("DIRECTREPLAY");
        var request = DirectCommand(supplier, product, 5, "DIRECTREPLAY-1", Guid.NewGuid());
        var first = await commands.FinalizeReceiptAsync(request, Token);
        var replay = await commands.FinalizeReceiptAsync(request, Token);
        Assert.Equal(first.Value.ReceiptId, replay.Value.ReceiptId);
        Assert.Equal(5m, await fixture.StockAsync([product.Id]));
        Assert.Equal(1, await fixture.MovementCountAsync(product.Id));
    }

    [Fact]
    public async Task PUR024_InvalidDirectReceiptsRollbackCompletely()
    {
        var commands = Commands();
        var supplier = await SupplierAsync(commands, "DIRECTBAD");
        var product = await ProductAsync("DIRECTBAD", true);
        var before = await fixture.PurchasingStockCountsAsync();
        var missingProduct = await commands.FinalizeReceiptAsync(DirectCommand(supplier, product with { Id = long.MaxValue }, 1, "DIRECTBAD-P", Guid.NewGuid(), "LOT-P", fixture.Clock.CurrentDate), Token);
        var missingSupplier = await commands.FinalizeReceiptAsync(DirectCommand(supplier with { SupplierId = long.MaxValue }, product, 1, "DIRECTBAD-S", Guid.NewGuid(), "LOT-S", fixture.Clock.CurrentDate), Token);
        var missingLot = await commands.FinalizeReceiptAsync(DirectCommand(supplier, product, 1, "DIRECTBAD-L", Guid.NewGuid()), Token);
        var expired = await commands.FinalizeReceiptAsync(DirectCommand(supplier, product, 1, "DIRECTBAD-E", Guid.NewGuid(), "LOT-E", fixture.Clock.CurrentDate.AddDays(-1)), Token);
        Assert.Equal("purchasing.receipt.product_not_found", missingProduct.Error.Code);
        Assert.Equal("purchasing.receipt.supplier_not_found", missingSupplier.Error.Code);
        Assert.Equal("purchasing.receipt.invalid_lot", missingLot.Error.Code);
        Assert.Equal("purchasing.receipt.invalid_expiry", expired.Error.Code);
        Assert.Equal(before, await fixture.PurchasingStockCountsAsync());
    }

    [Fact]
    public async Task PUR025_ReceiptUsesExplicitPurchasePriceSnapshotAndNeverChangesSalePrice()
    {
        var commands = Commands();
        var scenario = await ConfirmedOrderAsync(commands, "PRICE", [(5, 7m, false)]);
        var request = ReceiptCommand(scenario.Order, scenario.Products, [5], "PRICE-1", Guid.NewGuid());
        request = request with { Lines = [request.Lines[0] with { UnitPurchasePrice = 8m }] };
        var receipt = await commands.FinalizeReceiptAsync(request, Token);
        var order = (await Queries().GetOrderAsync(scenario.Order.OrderId, Token)).Value;
        Assert.Equal(8m, receipt.Value.Lines[0].UnitPurchasePrice);
        Assert.Equal(7m, order.Lines[0].UnitPurchasePrice);
        Assert.Equal(20m, await fixture.ScalarAsync<decimal>("SELECT CurrentSalePrice FROM catalog.ProductLocalSettings WHERE ProductId=@id;", new SqlParameter("@id", scenario.Products[0].Id)));
    }

    [Fact]
    public async Task PUR026_ProductDisabledAfterOrderIsExplicitlyRejectedWithoutCrash()
    {
        var commands = Commands();
        var scenario = await ConfirmedOrderAsync(commands, "DISPRODUCT", [(5, 7m, false)]);
        await fixture.ExecuteAsync("UPDATE catalog.Products SET IsActive=0 WHERE Id=@id;", new SqlParameter("@id", scenario.Products[0].Id));
        var before = await fixture.PurchasingStockCountsAsync();
        var result = await ReceiveAsync(commands, scenario.Order, scenario.Products, [1], "DISPRODUCT-1");
        Assert.Equal("purchasing.receipt.product_not_found", result.Error.Code);
        Assert.Equal(before, await fixture.PurchasingStockCountsAsync());
    }

    [Fact]
    public async Task PUR027_SupplierDisabledAfterOrderBlocksReceiptButPreservesHistory()
    {
        var commands = Commands();
        var scenario = await ConfirmedOrderAsync(commands, "DISSUP", [(5, 7m, false)]);
        var supplier = await commands.SetSupplierActivationAsync(new(scenario.Supplier.SupplierId, false, scenario.Supplier.RowVersion), Token);
        Assert.False(supplier.Value.IsActive);
        var before = await fixture.PurchasingStockCountsAsync();
        var result = await ReceiveAsync(commands, scenario.Order, scenario.Products, [1], "DISSUP-1");
        Assert.Equal("purchasing.supplier.inactive", result.Error.Code);
        Assert.True((await Queries().GetOrderAsync(scenario.Order.OrderId, Token)).IsSuccess);
        Assert.Equal(before, await fixture.PurchasingStockCountsAsync());
    }

    [Fact]
    public async Task PUR028_ExpiryTodayAndFutureAreAcceptedWhilePastDateIsAtomicRefusal()
    {
        var commands = Commands();
        var supplier = await SupplierAsync(commands, "EXPIRY");
        var product = await ProductAsync("EXPIRY", true);
        Assert.True((await commands.FinalizeReceiptAsync(DirectCommand(supplier, product, 1, "EXPIRY-TODAY", Guid.NewGuid(), "LOT-TODAY", fixture.Clock.CurrentDate), Token)).IsSuccess);
        Assert.True((await commands.FinalizeReceiptAsync(DirectCommand(supplier, product, 1, "EXPIRY-FUTURE", Guid.NewGuid(), "LOT-FUTURE", fixture.Clock.CurrentDate.AddYears(1)), Token)).IsSuccess);
        var before = await fixture.PurchasingStockCountsAsync();
        var past = await commands.FinalizeReceiptAsync(DirectCommand(supplier, product, 1, "EXPIRY-PAST", Guid.NewGuid(), "LOT-PAST", fixture.Clock.CurrentDate.AddDays(-1)), Token);
        Assert.Equal("purchasing.receipt.invalid_expiry", past.Error.Code);
        Assert.Equal(before, await fixture.PurchasingStockCountsAsync());
    }

    [Fact]
    public async Task PUR030_AuditContainsActorUtcAndActionsForSupplierOrderAndReceiptTypes()
    {
        var writer = new CapturingAuditWriter();
        var (authorizer, session) = Security();
        var audit = new BusinessAuditRecorder(writer, session, fixture.Clock);
        var commands = new PurchasingCommandHandlers(new SqlPurchasingStore(fixture.Factory, fixture.Clock), authorizer, audit);
        var supplier = await SupplierAsync(commands, "AUDIT");
        var product = await ProductAsync("AUDIT");
        var order = await DraftOrderAsync(commands, supplier, [(product, 2, 7m)]);
        order = (await commands.ConfirmOrderAsync(new(order.OrderId, order.RowVersion), Token)).Value;
        Assert.True((await ReceiveAsync(commands, order, [product], [1], "AUDIT-PART")).IsSuccess);
        var partial = (await Queries().GetOrderAsync(order.OrderId, Token)).Value;
        Assert.True((await ReceiveAsync(commands, partial, [product], [1], "AUDIT-FULL")).IsSuccess);
        Assert.True((await commands.FinalizeReceiptAsync(DirectCommand(supplier, product, 1, "AUDIT-DIRECT", Guid.NewGuid()), Token)).IsSuccess);

        Assert.Contains(writer.Events, item => item.Action == "supplier.create" && item.EntityType == "Supplier");
        Assert.Contains(writer.Events, item => item.Action == "order.create" && item.EntityType == "PurchaseOrder");
        Assert.Equal(3, writer.Events.Count(item => item.Action == "receipt.finalize" && item.EntityType == "GoodsReceipt"));
        Assert.All(writer.Events, item => { Assert.Equal(1900, item.ActorUserId); Assert.Equal("Owner PUR SQL", item.ActorDisplayName); Assert.Equal(fixture.Clock.UtcNow, item.OccurredAtUtc); Assert.Equal("Success", item.Result); });
    }

    [Fact]
    public async Task PUR031_NewReaderAndContextReloadSupplierOrderReceiptLotsAndStock()
    {
        var commands = Commands();
        var scenario = await ConfirmedOrderAsync(commands, "RELOAD", [(10, 7m, true)]);
        var receipt = await ReceiveAsync(commands, scenario.Order, scenario.Products, [4], "RELOAD-1", useLots: true);
        var rebuilt = new PurchasingQueryHandlers(new SqlPurchasingReader(fixture.CreateFactory()));
        var supplier = await rebuilt.GetSupplierAsync(scenario.Supplier.SupplierId, Token);
        var order = await rebuilt.GetOrderAsync(scenario.Order.OrderId, Token);
        var loadedReceipt = await rebuilt.GetReceiptAsync(receipt.Value.ReceiptId, Token);
        Assert.True(supplier.IsSuccess);
        Assert.Equal((4, 6), (order.Value.Lines[0].ReceivedQuantity, order.Value.Lines[0].RemainingQuantity));
        Assert.NotNull(loadedReceipt.Value.Lines[0].StockBatchId);
        Assert.Equal(4m, await fixture.StockAsync([scenario.Products[0].Id]));
    }

    [Fact]
    public async Task PUR032_InjectedSqlFailureBeforeCommitRollsBackReceiptBatchStockAndOrder()
    {
        var commands = Commands();
        var scenario = await ConfirmedOrderAsync(commands, "FAILSQL", [(10, 7m, true)]);
        var before = await fixture.PurchasingStockCountsAsync();
        var failing = Commands(fixture.CreateFactory(new FailGoodsReceiptInsertInterceptor()));
        var result = await ReceiveAsync(failing, scenario.Order, scenario.Products, [4], "FAILSQL-1", useLots: true);
        Assert.Equal("purchasing.receipt.database_unavailable", result.Error.Code);
        Assert.Equal(before, await fixture.PurchasingStockCountsAsync());
        var order = (await Queries().GetOrderAsync(scenario.Order.OrderId, Token)).Value;
        Assert.Equal((0, 10, PurchaseOrderStatus.Confirmed), (order.Lines[0].ReceivedQuantity, order.Lines[0].RemainingQuantity, order.Status));
    }

    private CancellationToken Token => TestContext.Current.CancellationToken;
    private PurchasingQueryHandlers Queries() => new(new SqlPurchasingReader(fixture.Factory));

    private PurchasingCommandHandlers Commands(IDbContextFactory<PharmacyDbContext>? factory = null)
    {
        var (authorizer, _) = Security();
        return new PurchasingCommandHandlers(new SqlPurchasingStore(factory ?? fixture.Factory, fixture.Clock), authorizer);
    }

    private (IPermissionAuthorizer Authorizer, CurrentSecuritySession Session) Security()
    {
        var session = new CurrentSecuritySession();
        session.Establish(new UserSessionView(
            1900, "owner.pur.sql", "Owner PUR SQL", ["OWNER"],
            new HashSet<string>([SecurityPermissions.PurchasingView, SecurityPermissions.PurchasingReceive], StringComparer.Ordinal),
            fixture.Clock.UtcNow, fixture.Clock.UtcNow), fixture.Clock.UtcNow);
        return (new PermissionAuthorizer(session, fixture.Clock), session);
    }

    private async Task<SupplierView> SupplierAsync(PurchasingCommandHandlers commands, string tag)
    {
        var result = await commands.CreateSupplierAsync(new(Code(tag), $"Fournisseur QA {tag}"), Token);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    private async Task<ProductInfo> ProductAsync(string tag, bool tracksLots = false)
    {
        var code = Code("P" + tag);
        var result = await new CreateCatalogProductHandler(new SqlCatalogStore(fixture.Factory)).HandleAsync(new(
            $"Produit QA {tag}", "Présentation QA", fixture.CategoryId, code, 20m,
            TracksLots: tracksLots, TracksExpiry: tracksLots), Token);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return new(result.Value.ProductId, code, tracksLots);
    }

    private async Task<PurchaseOrderView> DraftOrderAsync(PurchasingCommandHandlers commands, SupplierView supplier, IReadOnlyList<(ProductInfo Product, int Quantity, decimal Price)> lines)
    {
        var orderResult = await commands.CreateOrderAsync(new(supplier.SupplierId, Code("PO"), fixture.Clock.CurrentDate), Token);
        Assert.True(orderResult.IsSuccess, orderResult.IsFailure ? orderResult.Error.Code : null);
        var order = orderResult.Value;
        foreach (var line in lines)
        {
            var added = await commands.AddOrderLineAsync(new(order.OrderId, line.Product.Id, line.Quantity, line.Price, order.RowVersion), Token);
            Assert.True(added.IsSuccess, added.IsFailure ? added.Error.Code : null);
            order = added.Value;
        }
        return order;
    }

    private async Task<OrderScenario> ConfirmedOrderAsync(PurchasingCommandHandlers commands, string tag, IReadOnlyList<(int Quantity, decimal Price, bool TracksLots)> specifications)
    {
        var supplier = await SupplierAsync(commands, tag);
        var products = new List<ProductInfo>();
        for (var index = 0; index < specifications.Count; index++) products.Add(await ProductAsync($"{tag}{index}", specifications[index].TracksLots));
        var lines = specifications.Select((item, index) => (products[index], item.Quantity, item.Price)).ToArray();
        var draft = await DraftOrderAsync(commands, supplier, lines);
        var confirmed = await commands.ConfirmOrderAsync(new(draft.OrderId, draft.RowVersion), Token);
        Assert.True(confirmed.IsSuccess, confirmed.IsFailure ? confirmed.Error.Code : null);
        return new(supplier, confirmed.Value, products);
    }

    private Task<Result<GoodsReceiptView>> ReceiveAsync(PurchasingCommandHandlers commands, PurchaseOrderView order, IReadOnlyList<ProductInfo> products, IReadOnlyList<int> quantities, string tag, bool useLots = false)
        => commands.FinalizeReceiptAsync(ReceiptCommand(order, products, quantities, tag, Guid.NewGuid(), useLots), Token);

    private Task<Result<GoodsReceiptView>> ReceiveSelectedAsync(PurchasingCommandHandlers commands, PurchaseOrderView order, IReadOnlyList<ProductInfo> products, IReadOnlyList<int> quantities, string tag)
        => commands.FinalizeReceiptAsync(ReceiptCommand(order, products, quantities, tag, Guid.NewGuid()), Token);

    private FinalizeGoodsReceiptCommand ReceiptCommand(PurchaseOrderView order, IReadOnlyList<ProductInfo> products, IReadOnlyList<int> quantities, string tag, Guid operationId, bool useLots = false)
    {
        var lines = products.Select((product, index) =>
        {
            var orderLine = order.Lines.Single(line => line.ProductId == product.Id);
            var lot = useLots || product.TracksLots ? $"LOT-{tag}-{index}".ToUpperInvariant() : null;
            return new FinalizeGoodsReceiptLineCommand(product.Id, orderLine.LineId, quantities[index], orderLine.UnitPurchasePrice ?? 0m, null, lot, lot is null ? null : fixture.Clock.CurrentDate.AddYears(1));
        }).ToArray();
        return new(operationId, order.SupplierId, order.OrderId, fixture.LocationId, Code("BL" + tag), fixture.Clock.CurrentDate, order.RowVersion, lines);
    }

    private FinalizeGoodsReceiptCommand DirectCommand(SupplierView supplier, ProductInfo product, int quantity, string tag, Guid operationId, string? lot = null, DateOnly? expiry = null)
        => new(operationId, supplier.SupplierId, null, fixture.LocationId, Code("BL" + tag), fixture.Clock.CurrentDate, null,
            [new(product.Id, null, quantity, 7m, null, lot, expiry)]);

    private static string Code(string tag)
    {
        var compact = new string(tag.Where(char.IsLetterOrDigit).Take(14).ToArray()).ToUpperInvariant();
        return $"Q{compact}-{Guid.NewGuid():N}"[..Math.Min(32, compact.Length + 10)];
    }

    private sealed record ProductInfo(long Id, string Code, bool TracksLots);
    private sealed record OrderScenario(SupplierView Supplier, PurchaseOrderView Order, IReadOnlyList<ProductInfo> Products);

    private sealed class CapturingAuditWriter : IAuditWriter
    {
        public List<AppendAuditEventRequest> Events { get; } = [];
        public Task<Result> AppendAsync(AppendAuditEventRequest request, CancellationToken cancellationToken = default)
        {
            Events.Add(request);
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FailGoodsReceiptInsertInterceptor : DbCommandInterceptor
    {
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) => Fail(command, result);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) => ValueTask.FromResult(Fail(command, result));
        private static InterceptionResult<DbDataReader> Fail(DbCommand command, InterceptionResult<DbDataReader> result)
        {
            if (command.CommandText.Contains("INSERT", StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains("[purchasing].[GoodsReceipts]", StringComparison.Ordinal))
                throw new InvalidOperationException("Synthetic PUR032 SQL failure before commit.");
            return result;
        }
    }
}

public sealed class SqlPurchasingGateFixture : IAsyncLifetime
{
    private const string Prefix = "Pharmacy_PUR_Gate_Test_";
    private string serverConnection = string.Empty;
    private string databaseConnection = string.Empty;
    private string databaseName = string.Empty;

    public GateClock Clock { get; } = new(new DateTime(2026, 8, 17, 12, 30, 0, DateTimeKind.Utc));
    public IDbContextFactory<PharmacyDbContext> Factory { get; private set; } = null!;
    public long CategoryId { get; private set; }
    public long LocationId { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException("PHARMACY_TEST_SQLSERVER_CONNECTION est requise.");
        serverConnection = new SqlConnectionStringBuilder(raw) { InitialCatalog = "master", Pooling = false, ApplicationName = "Pharmacy.PUR.Gate.Tests" }.ConnectionString;
        databaseName = Prefix + Guid.NewGuid().ToString("N")[..12];
        ValidateDatabaseName(databaseName);
        await ExecuteServerAsync($"CREATE DATABASE [{databaseName}];");
        databaseConnection = new SqlConnectionStringBuilder(serverConnection) { InitialCatalog = databaseName }.ConnectionString;
        Factory = CreateFactory();
        await using (var context = await Factory.CreateDbContextAsync())
        {
            await context.Database.MigrateAsync();
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        }
        var category = await new SqlCatalogStore(Factory).CreateAsync(
            new CreateCatalogCategoryCommand(
                "PUR-GATE",
                "Catégorie PUR Gate",
                DefaultTracksLots: true,
                DefaultTracksExpiry: true),
            CancellationToken.None);
        Assert.True(category.IsSuccess);
        CategoryId = category.Value.CategoryId;
        var inventory = new InventoryCommandHandlers(new SqlInventoryStore(Factory), new GateInventoryDate(Clock.CurrentDate));
        var location = await inventory.CreateLocationAsync(new(Guid.NewGuid(), "PUR-GATE", "Réception PUR Gate", true), CancellationToken.None);
        Assert.True(location.IsSuccess);
        LocationId = location.Value.LocationId;
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            ValidateDatabaseName(databaseName);
            await ExecuteServerAsync($"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END;");
        }
        SqlConnection.ClearAllPools();
    }

    public IDbContextFactory<PharmacyDbContext> CreateFactory(params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<PharmacyDbContext>();
        builder.UsePharmacySqlServer(databaseConnection);
        if (interceptors.Length > 0) builder.AddInterceptors(interceptors);
        return new FactoryImpl(builder.Options);
    }

    public async Task<(int Batches, int Balances, int Movements)> InventoryCountsAsync() =>
        (await ScalarAsync<int>("SELECT COUNT(*) FROM inventory.StockBatches;"), await ScalarAsync<int>("SELECT COUNT(*) FROM inventory.StockBalances;"), await ScalarAsync<int>("SELECT COUNT(*) FROM inventory.StockMovements;"));

    public async Task<(int Receipts, int Lines, int Batches, int Balances, int Movements)> PurchasingStockCountsAsync() =>
        (await ScalarAsync<int>("SELECT COUNT(*) FROM purchasing.GoodsReceipts;"), await ScalarAsync<int>("SELECT COUNT(*) FROM purchasing.GoodsReceiptLines;"), await ScalarAsync<int>("SELECT COUNT(*) FROM inventory.StockBatches;"), await ScalarAsync<int>("SELECT COUNT(*) FROM inventory.StockBalances;"), await ScalarAsync<int>("SELECT COUNT(*) FROM inventory.StockMovements;"));

    public Task<decimal> StockAsync(long[] productIds)
    {
        var parameters = productIds.Select((id, index) => new SqlParameter($"@p{index}", SqlDbType.BigInt) { Value = id }).ToArray();
        return ScalarAsync<decimal>($"SELECT COALESCE(SUM(AvailableQuantity),0) FROM inventory.StockBalances WHERE ProductId IN ({string.Join(',', parameters.Select(parameter => parameter.ParameterName))});", parameters);
    }

    public Task<int> MovementCountAsync(long productId) => ScalarAsync<int>("SELECT COUNT(*) FROM inventory.StockMovements WHERE ProductId=@id AND MovementType=N'PurchaseReceipt';", new SqlParameter("@id", productId));

    public async Task<T> ScalarAsync<T>(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(databaseConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Valeur SQL absente.");
        return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task ExecuteAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(databaseConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    private async Task ExecuteServerAsync(string sql)
    {
        await using var connection = new SqlConnection(serverConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static void ValidateDatabaseName(string value)
    {
        if (!Regex.IsMatch(value, "^Pharmacy_PUR_Gate_Test_[0-9a-f]{12}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Nom de base temporaire PUR refusé.");
    }

    public sealed class GateClock(DateTime now) : IPurchasingClock, ISecurityClock
    {
        public DateTime UtcNow { get; } = now;
        public DateOnly CurrentDate => DateOnly.FromDateTime(UtcNow);
    }

    private sealed class GateInventoryDate(DateOnly date) : Pharmacy.Application.Inventory.Contracts.IInventoryDateProvider { public DateOnly CurrentDate { get; } = date; }
    private sealed class FactoryImpl(DbContextOptions<PharmacyDbContext> options) : IDbContextFactory<PharmacyDbContext>
    {
        public PharmacyDbContext CreateDbContext() => new(options);
        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
