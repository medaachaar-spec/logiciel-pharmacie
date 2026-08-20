using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Commands;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Finalization.Models;
using Pharmacy.Application.Sales.Finalization.Queries;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Catalog;
using Pharmacy.Infrastructure.Inventory;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Sales;
using Pharmacy.Infrastructure.Sales.InMemory;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class SqlSalesIntegrationTests
{
    private const string TestDatabasePrefix = "Pharmacy_Sprint13_Test_";
    private const string PreviousMigration = "20260804214802_PurchasingPersistenceV01";

    [Fact]
    public async Task RealSqlSalesValidatesAtomicFefoPaymentsIdempotenceHistoryAndRollback()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var serverConnection = BuildServerConnection();
        var databaseName = TestDatabasePrefix + Guid.NewGuid().ToString("N")[..12];
        ValidateTemporaryDatabaseName(databaseName);
        var databaseConnection = new SqlConnectionStringBuilder(serverConnection.ConnectionString)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Pharmacy.Sprint13.Tests",
            Pooling = false
        };
        await CreateDatabaseAsync(serverConnection.ConnectionString, databaseName);

        try
        {
            var factory = new TestDbContextFactory(databaseConnection.ConnectionString);
            await using (var context = await factory.CreateDbContextAsync(cancellationToken))
            {
                await context.Database.MigrateAsync(cancellationToken);
                Assert.Empty(await context.Database.GetPendingMigrationsAsync(cancellationToken));
                Assert.Contains(await context.Database.GetAppliedMigrationsAsync(cancellationToken), migration =>
                    migration.EndsWith("_SalesPersistenceV01", StringComparison.Ordinal));
            }

            Assert.Equal(9, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'sales';"));

            var catalogStore = new SqlCatalogStore(factory);
            var category = await catalogStore.CreateAsync(
                new CreateCatalogCategoryCommand(
                    "S13-TEST",
                    "Catégorie ventes persistantes",
                    DefaultTracksLots: true,
                    DefaultTracksExpiry: true),
                cancellationToken);
            Assert.True(category.IsSuccess);

            var lotProduct = await new CreateCatalogProductHandler(catalogStore).HandleAsync(
                new CreateCatalogProductCommand(
                    "Produit FEFO Sprint 13",
                    "Boîte de test",
                    category.Value.CategoryId,
                    "S13-SALE-001",
                    10m,
                    TracksLots: true,
                    TracksExpiry: true),
                cancellationToken);
            Assert.True(lotProduct.IsSuccess);

            var plainProduct = await new CreateCatalogProductHandler(catalogStore).HandleAsync(
                new CreateCatalogProductCommand(
                    "Produit carte Sprint 13",
                    "Unité de test",
                    category.Value.CategoryId,
                    "S13-SALE-002",
                    7m),
                cancellationToken);
            Assert.True(plainProduct.IsSuccess);

            var clock = new FixedClock(
                new DateOnly(2026, 8, 10),
                new DateTime(2026, 8, 10, 9, 30, 0, DateTimeKind.Utc));
            var inventoryStore = new SqlInventoryStore(factory);
            var inventoryCommands = new InventoryCommandHandlers(inventoryStore, clock);
            var location = await inventoryCommands.CreateLocationAsync(
                new CreateStockLocationCommand(
                    Guid.NewGuid(), "S13-MAIN", "Emplacement ventes Sprint 13", true),
                cancellationToken);
            Assert.True(location.IsSuccess);

            var near = await inventoryCommands.CreateBatchAsync(
                new CreateStockBatchCommand(
                    Guid.NewGuid(), lotProduct.Value.ProductId, "S13-NEAR", new DateOnly(2026, 9, 1)),
                cancellationToken);
            var far = await inventoryCommands.CreateBatchAsync(
                new CreateStockBatchCommand(
                    Guid.NewGuid(), lotProduct.Value.ProductId, "S13-FAR", new DateOnly(2026, 12, 31)),
                cancellationToken);
            var expired = await inventoryCommands.CreateBatchAsync(
                new CreateStockBatchCommand(
                    Guid.NewGuid(), lotProduct.Value.ProductId, "S13-EXPIRED", new DateOnly(2026, 8, 9)),
                cancellationToken);
            Assert.All(new[] { near, far, expired }, result => Assert.True(result.IsSuccess));

            Assert.True((await inventoryCommands.RecordOpeningAsync(
                Opening(lotProduct.Value.ProductId, location.Value.LocationId, near.Value.BatchId, 3),
                cancellationToken)).IsSuccess);
            Assert.True((await inventoryCommands.RecordOpeningAsync(
                Opening(lotProduct.Value.ProductId, location.Value.LocationId, far.Value.BatchId, 8),
                cancellationToken)).IsSuccess);
            Assert.True((await inventoryCommands.RecordOpeningAsync(
                Opening(lotProduct.Value.ProductId, location.Value.LocationId, expired.Value.BatchId, 9),
                cancellationToken)).IsSuccess);
            Assert.True((await inventoryCommands.RecordOpeningAsync(
                Opening(plainProduct.Value.ProductId, location.Value.LocationId, null, 4),
                cancellationToken)).IsSuccess);

            var catalogReader = new SqlProductCatalogReader(factory);
            var availabilityReader = new SqlInventoryStockAvailabilityReader(factory, clock);
            var finalizer = new SqlSaleFinalizer(factory, clock);
            var reader = new SqlSalesReader(factory);

            var cashOperation = Guid.NewGuid();
            var cashScenario = await CreateDraftAsync(
                lotProduct.Value.ProductId, 5, catalogReader, availabilityReader, cancellationToken);
            var cashPreparation = await PrepareAsync(
                cashOperation, cashScenario, availabilityReader, cancellationToken);
            var cashRequest = new FinalizeSaleRequest(
                cashPreparation,
                new SalePaymentRequest(SalePaymentMethod.Cash, 50m, 60m, 10m, false));
            var cashSale = await finalizer.FinalizeAsync(cashRequest, cancellationToken);
            Assert.True(cashSale.IsSuccess, cashSale.IsFailure ? cashSale.Error.Code : null);
            Assert.StartsWith("VTE-20260810-", cashSale.Value.Reference, StringComparison.Ordinal);
            Assert.Equal(50m, cashSale.Value.Total.Amount);
            Assert.Equal(10m, cashSale.Value.Payment.ChangeGiven);
            Assert.Collection(
                Assert.Single(cashSale.Value.Lines).Allocations,
                allocation =>
                {
                    Assert.Equal("S13-NEAR", allocation.BatchNumber);
                    Assert.Equal(3, allocation.Quantity);
                },
                allocation =>
                {
                    Assert.Equal("S13-FAR", allocation.BatchNumber);
                    Assert.Equal(2, allocation.Quantity);
                });

            Assert.Equal(0, await BalanceAsync(databaseConnection.ConnectionString, near.Value.BatchId));
            Assert.Equal(6, await BalanceAsync(databaseConnection.ConnectionString, far.Value.BatchId));
            Assert.Equal(9, await BalanceAsync(databaseConnection.ConnectionString, expired.Value.BatchId));
            Assert.Equal(2, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM inventory.StockMovements WHERE MovementType=N'Sale';"));
            Assert.Equal(1, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sales.Sales WHERE Id=@saleId AND GrandTotal=50 AND PaidTotal=50;",
                new SqlParameter("@saleId", SqlDbType.BigInt) { Value = cashSale.Value.SaleId }));
            Assert.Equal(1, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sales.Payments WHERE SaleId=@saleId AND PaymentMethod=N'Cash' AND TenderedAmount=60 AND ChangeGiven=10;",
                new SqlParameter("@saleId", SqlDbType.BigInt) { Value = cashSale.Value.SaleId }));

            var replay = await finalizer.FinalizeAsync(cashRequest, cancellationToken);
            Assert.True(replay.IsSuccess);
            Assert.Equal(cashSale.Value.SaleId, replay.Value.SaleId);
            Assert.Equal(1, await ScalarAsync<int>(
                databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.Sales;"));
            var conflictingReplay = await finalizer.FinalizeAsync(
                cashRequest with
                {
                    Payment = new SalePaymentRequest(SalePaymentMethod.Cash, 50m, 70m, 20m, false)
                },
                cancellationToken);
            Assert.Equal("sales.operation.conflict", conflictingReplay.Error.Code);

            var insufficientOperation = Guid.NewGuid();
            var insufficientScenario = await CreateDraftAsync(
                lotProduct.Value.ProductId, 6, catalogReader, availabilityReader, cancellationToken);
            var insufficientPreparation = await PrepareAsync(
                insufficientOperation, insufficientScenario, availabilityReader, cancellationToken);
            await ExecuteAsync(
                databaseConnection.ConnectionString,
                "UPDATE inventory.StockBalances SET AvailableQuantity=0 WHERE StockBatchId=@batchId;",
                new SqlParameter("@batchId", SqlDbType.BigInt) { Value = far.Value.BatchId!.Value });
            var insufficient = await finalizer.FinalizeAsync(
                new FinalizeSaleRequest(
                    insufficientPreparation,
                    new SalePaymentRequest(SalePaymentMethod.Cash, 60m, 60m, 0m, false)),
                cancellationToken);
            Assert.Equal("sales.stock.insufficient", insufficient.Error.Code);
            Assert.Equal(1, await ScalarAsync<int>(
                databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.Sales;"));
            Assert.Equal(0, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sales.SaleOperations WHERE OperationId=@operationId;",
                new SqlParameter("@operationId", SqlDbType.UniqueIdentifier) { Value = insufficientOperation }));
            await ExecuteAsync(
                databaseConnection.ConnectionString,
                "UPDATE inventory.StockBalances SET AvailableQuantity=6 WHERE StockBatchId=@batchId;",
                new SqlParameter("@batchId", SqlDbType.BigInt) { Value = far.Value.BatchId!.Value });

            var priceScenario = await CreateDraftAsync(
                plainProduct.Value.ProductId, 1, catalogReader, availabilityReader, cancellationToken);
            var pricePreparation = await PrepareAsync(
                Guid.NewGuid(), priceScenario, availabilityReader, cancellationToken);
            await ExecuteAsync(
                databaseConnection.ConnectionString,
                "UPDATE catalog.ProductLocalSettings SET CurrentSalePrice=8 WHERE ProductId=@productId;",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = plainProduct.Value.ProductId });
            var changedPrice = await finalizer.FinalizeAsync(
                new FinalizeSaleRequest(
                    pricePreparation,
                    new SalePaymentRequest(SalePaymentMethod.Cash, 7m, 7m, 0m, false)),
                cancellationToken);
            Assert.Equal("sales.price.changed", changedPrice.Error.Code);
            Assert.Equal(4, await BalanceAsync(databaseConnection.ConnectionString, null, plainProduct.Value.ProductId));
            await ExecuteAsync(
                databaseConnection.ConnectionString,
                "UPDATE catalog.ProductLocalSettings SET CurrentSalePrice=7 WHERE ProductId=@productId;",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = plainProduct.Value.ProductId });

            var cardOperation = Guid.NewGuid();
            var cardScenario = await CreateDraftAsync(
                plainProduct.Value.ProductId, 1, catalogReader, availabilityReader, cancellationToken);
            var cardPreparationStore = new InMemorySaleFinalizationPreparationStore();
            var cardHandler = new FinalizeSaleHandler(
                new PrepareSaleFinalizationHandler(
                    cardScenario.Store, availabilityReader, cardPreparationStore),
                finalizer);
            var unconfirmedCard = await cardHandler.HandleAsync(
                new FinalizeSaleCommand(
                    cardOperation,
                    cardScenario.Draft.Id,
                    cardScenario.Draft.Version,
                    SalePaymentMethod.ExternalCard,
                    null,
                    false),
                cancellationToken);
            Assert.Equal("sales.payment.card_unconfirmed", unconfirmedCard.Error.Code);
            var cardSale = await cardHandler.HandleAsync(
                new FinalizeSaleCommand(
                    cardOperation,
                    cardScenario.Draft.Id,
                    cardScenario.Draft.Version,
                    SalePaymentMethod.ExternalCard,
                    null,
                    true),
                cancellationToken);
            Assert.True(cardSale.IsSuccess, cardSale.IsFailure ? cardSale.Error.Code : null);
            Assert.Equal(SalePaymentMethod.ExternalCard, cardSale.Value.Payment.Method);
            Assert.True(cardSale.Value.Payment.ExternalCardConfirmed);
            Assert.Equal(3, await BalanceAsync(databaseConnection.ConnectionString, null, plainProduct.Value.ProductId));

            var loaded = await new GetSaleByIdHandler(reader).HandleAsync(
                new GetSaleByIdQuery(cashSale.Value.SaleId), cancellationToken);
            var recent = await new GetRecentSalesHandler(reader).HandleAsync(
                new GetRecentSalesQuery(10), cancellationToken);
            Assert.Equal(cashSale.Value.Reference, loaded.Value.Reference);
            Assert.Equal(2, recent.Value.Count);
            Assert.Contains(recent.Value, item => item.SaleId == cashSale.Value.SaleId);
            Assert.Contains(recent.Value, item => item.SaleId == cardSale.Value.SaleId);

            var concurrentScenarioA = await CreateDraftAsync(
                lotProduct.Value.ProductId, 4, catalogReader, availabilityReader, cancellationToken);
            var concurrentScenarioB = await CreateDraftAsync(
                lotProduct.Value.ProductId, 4, catalogReader, availabilityReader, cancellationToken);
            var concurrentPreparationA = await PrepareAsync(
                Guid.NewGuid(), concurrentScenarioA, availabilityReader, cancellationToken);
            var concurrentPreparationB = await PrepareAsync(
                Guid.NewGuid(), concurrentScenarioB, availabilityReader, cancellationToken);
            var concurrentRequestA = new FinalizeSaleRequest(
                concurrentPreparationA,
                new SalePaymentRequest(SalePaymentMethod.Cash, 40m, 40m, 0m, false));
            var concurrentRequestB = new FinalizeSaleRequest(
                concurrentPreparationB,
                new SalePaymentRequest(SalePaymentMethod.Cash, 40m, 40m, 0m, false));
            var concurrencyBarrier = new AsyncBarrier(2);
            var concurrentFinalizer = new SqlSaleFinalizer(
                factory,
                clock,
                concurrencyBarrier.SignalAndWaitAsync);
            var concurrentResults = await Task.WhenAll(
                concurrentFinalizer.FinalizeAsync(concurrentRequestA, cancellationToken),
                concurrentFinalizer.FinalizeAsync(concurrentRequestB, cancellationToken));
            Assert.Single(concurrentResults, result => result.IsSuccess);
            var concurrencyFailure = Assert.Single(concurrentResults, result => result.IsFailure);
            Assert.Equal("sales.stock.concurrency_conflict", concurrencyFailure.Error.Code);
            Assert.Equal(2, await BalanceAsync(databaseConnection.ConnectionString, far.Value.BatchId));
            Assert.Equal(3, await ScalarAsync<int>(
                databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.Sales;"));
            Assert.Equal(3, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sales.SaleOperations WHERE Status=N'Completed';"));

            var referencedDelete = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(
                databaseConnection.ConnectionString,
                "DELETE FROM inventory.StockBalances WHERE Id=@balanceId;",
                new SqlParameter("@balanceId", SqlDbType.BigInt)
                {
                    Value = Assert.Single(cashSale.Value.Lines).Allocations[0].StockBalanceId
                }));
            Assert.Equal(547, referencedDelete.Number);
            var negativeBalance = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(
                databaseConnection.ConnectionString,
                "UPDATE inventory.StockBalances SET AvailableQuantity=-1 WHERE StockBatchId=@batchId;",
                new SqlParameter("@batchId", SqlDbType.BigInt) { Value = far.Value.BatchId!.Value }));
            Assert.Equal(547, negativeBalance.Number);

            var unavailable = await new SqlSalesReader(new ThrowingDbContextFactory())
                .GetRecentAsync(5, cancellationToken);
            Assert.Equal("sales.database.unavailable", unavailable.Error.Code);

            await using (var rollbackContext = await factory.CreateDbContextAsync(cancellationToken))
            {
                await rollbackContext.GetService<IMigrator>().MigrateAsync(
                    PreviousMigration,
                    cancellationToken);
            }
            Assert.Equal(0, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'sales';"));
            Assert.Equal(0, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM inventory.StockMovements WHERE MovementType=N'Sale';"));
            Assert.Equal(3, await BalanceAsync(databaseConnection.ConnectionString, near.Value.BatchId));
            Assert.Equal(8, await BalanceAsync(databaseConnection.ConnectionString, far.Value.BatchId));
            Assert.Equal(4, await BalanceAsync(databaseConnection.ConnectionString, null, plainProduct.Value.ProductId));

            await using (var reapplyContext = await factory.CreateDbContextAsync(cancellationToken))
            {
                await reapplyContext.Database.MigrateAsync(cancellationToken);
                Assert.Empty(await reapplyContext.Database.GetPendingMigrationsAsync(cancellationToken));
            }
            Assert.Equal(9, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'sales';"));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropTemporaryDatabaseAsync(serverConnection.ConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task RET001_RealSqlSuspensionsPersistRestoreAndCloseAtomicallyWithoutEarlyStockEffects()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var serverConnection = BuildServerConnection();
        var databaseName = TestDatabasePrefix + Guid.NewGuid().ToString("N")[..12];
        ValidateTemporaryDatabaseName(databaseName);
        var databaseConnection = new SqlConnectionStringBuilder(serverConnection.ConnectionString)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Pharmacy.RET001.Tests",
            Pooling = false
        };
        await CreateDatabaseAsync(serverConnection.ConnectionString, databaseName);

        try
        {
            var factoryA = new TestDbContextFactory(databaseConnection.ConnectionString);
            await using (var context = await factoryA.CreateDbContextAsync(cancellationToken))
            {
                await context.Database.MigrateAsync(cancellationToken);
                Assert.Empty(await context.Database.GetPendingMigrationsAsync(cancellationToken));
            }

            var clock = new FixedClock(
                new DateOnly(2026, 8, 16),
                new DateTime(2026, 8, 16, 9, 0, 0, DateTimeKind.Utc));
            var catalogStore = new SqlCatalogStore(factoryA);
            var category = await catalogStore.CreateAsync(
                new CreateCatalogCategoryCommand(
                    "RET001",
                    "Suspensions persistantes RET-001",
                    DefaultTracksLots: false,
                    DefaultTracksExpiry: false),
                cancellationToken);
            Assert.True(category.IsSuccess);
            var product = await new CreateCatalogProductHandler(catalogStore).HandleAsync(
                new CreateCatalogProductCommand(
                    "Produit suspension RET-001",
                    "Boîte snapshot",
                    category.Value.CategoryId,
                    "RET001-PRODUCT",
                    10m),
                cancellationToken);
            Assert.True(product.IsSuccess);

            var inventory = new InventoryCommandHandlers(new SqlInventoryStore(factoryA), clock);
            var location = await inventory.CreateLocationAsync(
                new CreateStockLocationCommand(
                    Guid.NewGuid(),
                    "RET001-MAIN",
                    "Emplacement RET-001",
                    true),
                cancellationToken);
            Assert.True(location.IsSuccess);
            Assert.True((await inventory.RecordOpeningAsync(
                Opening(product.Value.ProductId, location.Value.LocationId, null, 8),
                cancellationToken)).IsSuccess);

            var suspendedStoreA = new SqlCustomerCreditCashStore(factoryA, clock);
            var operationId = Guid.NewGuid();
            var suspendRequest = new SuspendSaleRequest(
                operationId,
                null,
                new[]
                {
                    new SuspendSaleLineRequest(
                        product.Value.ProductId,
                        "RET001-PRODUCT",
                        "Produit suspension RET-001",
                        "Boîte snapshot",
                        10m,
                        2)
                });
            var suspended = await suspendedStoreA.SuspendAsync(suspendRequest, cancellationToken);
            Assert.True(suspended.IsSuccess, suspended.IsFailure ? suspended.Error.Code : null);
            Assert.Equal(1, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.SuspendedSales;"));
            Assert.Equal(1, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.SuspendedSaleLines;"));
            Assert.Equal(8, await BalanceAsync(databaseConnection.ConnectionString, null, product.Value.ProductId));
            Assert.Equal(0, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.Sales;"));
            Assert.Equal(0, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.Payments;"));
            Assert.Equal(0, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.SaleLineAllocations;"));
            Assert.Equal(0, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM inventory.StockMovements WHERE MovementType=N'Sale';"));

            var replay = await suspendedStoreA.SuspendAsync(suspendRequest, cancellationToken);
            Assert.True(replay.IsSuccess);
            Assert.Equal(suspended.Value.SuspendedSaleId, replay.Value.SuspendedSaleId);
            Assert.Equal(1, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.SuspendedSales;"));
            var conflictingReplay = await suspendedStoreA.SuspendAsync(
                suspendRequest with
                {
                    Lines = new[] { suspendRequest.Lines[0] with { Quantity = 3 } }
                },
                cancellationToken);
            Assert.Equal("sales.operation.conflict", conflictingReplay.Error.Code);

            var factoryB = new TestDbContextFactory(databaseConnection.ConnectionString);
            var suspendedStoreB = new SqlCustomerCreditCashStore(factoryB, clock);
            var reloaded = await suspendedStoreB.GetOpenAsync(cancellationToken);
            var reloadedSuspension = Assert.Single(reloaded.Value);
            var reloadedLine = Assert.Single(reloadedSuspension.Lines);
            Assert.Equal(2, reloadedLine.Quantity.Value);
            Assert.Equal(10m, reloadedLine.UnitPrice.Amount);

            var draftStore = new InMemorySaleDraftStore();
            var restored = await new RestoreSuspendedSaleDraftHandler(
                new InMemorySaleDraftIdGenerator(), draftStore).HandleAsync(
                    reloadedSuspension,
                    cancellationToken);
            Assert.True(restored.IsSuccess);
            Assert.Equal(2, Assert.Single(restored.Value.Lines).Quantity.Value);
            Assert.Equal(10m, Assert.Single(restored.Value.Lines).UnitPrice.Amount);
            Assert.Equal(8, await BalanceAsync(databaseConnection.ConnectionString, null, product.Value.ProductId));
            Assert.Equal(0, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM inventory.StockMovements WHERE MovementType=N'Sale';"));

            var catalogReader = new SqlProductCatalogReader(factoryB);
            var availabilityReader = new SqlInventoryStockAvailabilityReader(factoryB, clock);
            var preparation = await PrepareAsync(
                Guid.NewGuid(),
                new DraftScenario(draftStore, restored.Value),
                availabilityReader,
                cancellationToken);
            await ExecuteAsync(
                databaseConnection.ConnectionString,
                "UPDATE catalog.ProductLocalSettings SET CurrentSalePrice=12 WHERE ProductId=@productId;",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = product.Value.ProductId });
            Assert.Equal(10m, Assert.Single(restored.Value.Lines).UnitPrice.Amount);
            var finalizer = new SqlSaleFinalizer(factoryB, clock);
            var priceChanged = await finalizer.FinalizeAsync(
                new FinalizeSaleRequest(
                    preparation,
                    new SalePaymentRequest(SalePaymentMethod.Cash, 20m, 20m, 0m, false),
                    SuspendedSale: new SuspendedSaleCompletionRequest(
                        reloadedSuspension.SuspendedSaleId,
                        reloadedSuspension.RowVersion)),
                cancellationToken);
            Assert.Equal("sales.price.changed", priceChanged.Error.Code);
            Assert.Equal("Open", await ScalarAsync<string>(
                databaseConnection.ConnectionString,
                "SELECT Status FROM sales.SuspendedSales WHERE Id=@id;",
                new SqlParameter("@id", SqlDbType.BigInt) { Value = reloadedSuspension.SuspendedSaleId }));
            Assert.Equal(8, await BalanceAsync(databaseConnection.ConnectionString, null, product.Value.ProductId));
            await ExecuteAsync(
                databaseConnection.ConnectionString,
                "UPDATE catalog.ProductLocalSettings SET CurrentSalePrice=10 WHERE ProductId=@productId;",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = product.Value.ProductId });

            var finalizeRequest = new FinalizeSaleRequest(
                preparation,
                new SalePaymentRequest(SalePaymentMethod.Cash, 20m, 20m, 0m, false),
                SuspendedSale: new SuspendedSaleCompletionRequest(
                    reloadedSuspension.SuspendedSaleId,
                    reloadedSuspension.RowVersion));
            var finalized = await finalizer.FinalizeAsync(finalizeRequest, cancellationToken);
            Assert.True(finalized.IsSuccess, finalized.IsFailure ? finalized.Error.Code : null);
            Assert.Equal("Resumed", await ScalarAsync<string>(
                databaseConnection.ConnectionString,
                "SELECT Status FROM sales.SuspendedSales WHERE Id=@id;",
                new SqlParameter("@id", SqlDbType.BigInt) { Value = reloadedSuspension.SuspendedSaleId }));
            Assert.Equal(6, await BalanceAsync(databaseConnection.ConnectionString, null, product.Value.ProductId));
            Assert.Equal(1, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.Sales;"));
            Assert.Equal(1, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.Payments;"));
            Assert.Equal(1, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM inventory.StockMovements WHERE MovementType=N'Sale';"));
            var finalizedReplay = await finalizer.FinalizeAsync(finalizeRequest, cancellationToken);
            Assert.True(finalizedReplay.IsSuccess);
            Assert.Equal(finalized.Value.SaleId, finalizedReplay.Value.SaleId);
            Assert.Equal(6, await BalanceAsync(databaseConnection.ConnectionString, null, product.Value.ProductId));
            Assert.Equal(1, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.Sales;"));

            var insufficientSuspension = await suspendedStoreB.SuspendAsync(
                new SuspendSaleRequest(
                    Guid.NewGuid(),
                    null,
                    new[] { suspendRequest.Lines[0] with { Quantity = 3 } }),
                cancellationToken);
            var insufficientDraftStore = new InMemorySaleDraftStore();
            var insufficientDraft = await new RestoreSuspendedSaleDraftHandler(
                new InMemorySaleDraftIdGenerator(), insufficientDraftStore).HandleAsync(
                    insufficientSuspension.Value,
                    cancellationToken);
            var insufficientPreparation = await PrepareAsync(
                Guid.NewGuid(),
                new DraftScenario(insufficientDraftStore, insufficientDraft.Value),
                availabilityReader,
                cancellationToken);
            await ExecuteAsync(
                databaseConnection.ConnectionString,
                "UPDATE inventory.StockBalances SET AvailableQuantity=1 WHERE ProductId=@productId AND StockBatchId IS NULL;",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = product.Value.ProductId });
            var insufficient = await finalizer.FinalizeAsync(
                new FinalizeSaleRequest(
                    insufficientPreparation,
                    new SalePaymentRequest(SalePaymentMethod.Cash, 30m, 30m, 0m, false),
                    SuspendedSale: new SuspendedSaleCompletionRequest(
                        insufficientSuspension.Value.SuspendedSaleId,
                        insufficientSuspension.Value.RowVersion)),
                cancellationToken);
            Assert.Equal("sales.stock.insufficient", insufficient.Error.Code);
            Assert.Equal("Open", await ScalarAsync<string>(
                databaseConnection.ConnectionString,
                "SELECT Status FROM sales.SuspendedSales WHERE Id=@id;",
                new SqlParameter("@id", SqlDbType.BigInt) { Value = insufficientSuspension.Value.SuspendedSaleId }));
            Assert.Equal(1, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.Sales;"));
            Assert.Equal(1, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.Payments;"));
            Assert.Equal(1, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM inventory.StockMovements WHERE MovementType=N'Sale';"));
            await ExecuteAsync(
                databaseConnection.ConnectionString,
                "UPDATE inventory.StockBalances SET AvailableQuantity=6 WHERE ProductId=@productId AND StockBatchId IS NULL;",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = product.Value.ProductId });

            var abandon = await suspendedStoreB.AbandonAsync(
                Guid.NewGuid(),
                insufficientSuspension.Value.SuspendedSaleId,
                insufficientSuspension.Value.RowVersion,
                cancellationToken);
            Assert.True(abandon.IsSuccess);
            var staleAbandon = await suspendedStoreA.AbandonAsync(
                Guid.NewGuid(),
                insufficientSuspension.Value.SuspendedSaleId,
                insufficientSuspension.Value.RowVersion,
                cancellationToken);
            Assert.Equal("sales.operation.conflict", staleAbandon.Error.Code);
            Assert.Equal(1, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.Sales;"));
            Assert.Equal(1, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.Payments;"));
            Assert.Equal(1, await ScalarAsync<int>(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM inventory.StockMovements WHERE MovementType=N'Sale';"));

            var disabledSuspension = await suspendedStoreB.SuspendAsync(
                new SuspendSaleRequest(
                    Guid.NewGuid(),
                    null,
                    new[] { suspendRequest.Lines[0] with { Quantity = 1 } }),
                cancellationToken);
            var disabledDraftStore = new InMemorySaleDraftStore();
            var disabledDraft = await new RestoreSuspendedSaleDraftHandler(
                new InMemorySaleDraftIdGenerator(), disabledDraftStore).HandleAsync(
                    disabledSuspension.Value,
                    cancellationToken);
            var disabledPreparation = await PrepareAsync(
                Guid.NewGuid(),
                new DraftScenario(disabledDraftStore, disabledDraft.Value),
                availabilityReader,
                cancellationToken);
            await ExecuteAsync(
                databaseConnection.ConnectionString,
                "UPDATE catalog.Products SET IsActive=0 WHERE Id=@productId;",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = product.Value.ProductId });
            var disabled = await finalizer.FinalizeAsync(
                new FinalizeSaleRequest(
                    disabledPreparation,
                    new SalePaymentRequest(SalePaymentMethod.Cash, 10m, 10m, 0m, false),
                    SuspendedSale: new SuspendedSaleCompletionRequest(
                        disabledSuspension.Value.SuspendedSaleId,
                        disabledSuspension.Value.RowVersion)),
                cancellationToken);
            Assert.Equal("sales.product.not_sellable", disabled.Error.Code);
            Assert.Equal("Open", await ScalarAsync<string>(
                databaseConnection.ConnectionString,
                "SELECT Status FROM sales.SuspendedSales WHERE Id=@id;",
                new SqlParameter("@id", SqlDbType.BigInt) { Value = disabledSuspension.Value.SuspendedSaleId }));
            Assert.Equal(6, await BalanceAsync(databaseConnection.ConnectionString, null, product.Value.ProductId));
            await ExecuteAsync(
                databaseConnection.ConnectionString,
                "UPDATE catalog.Products SET IsActive=1 WHERE Id=@productId;",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = product.Value.ProductId });
            Assert.True((await suspendedStoreB.AbandonAsync(
                Guid.NewGuid(),
                disabledSuspension.Value.SuspendedSaleId,
                disabledSuspension.Value.RowVersion,
                cancellationToken)).IsSuccess);
            Assert.Empty((await suspendedStoreB.GetOpenAsync(cancellationToken)).Value);
            _ = catalogReader;
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropTemporaryDatabaseAsync(serverConnection.ConnectionString, databaseName);
        }
    }

    [Fact]
    public Task SAL0807_SuspendedSalePricePolicyRemainsSnapshotThenRevalidationRefusal()
    {
        return RET001_RealSqlSuspensionsPersistRestoreAndCloseAtomicallyWithoutEarlyStockEffects();
    }

    private static async Task<DraftScenario> CreateDraftAsync(
        long productId,
        int quantity,
        IProductCatalogReader catalogReader,
        IStockAvailabilityReader availabilityReader,
        CancellationToken cancellationToken)
    {
        var store = new InMemorySaleDraftStore();
        var draft = await new CreateSaleDraftHandler(
            new InMemorySaleDraftIdGenerator(), store).HandleAsync(cancellationToken);
        Assert.True(draft.IsSuccess);
        var added = await new AddProductToSaleDraftHandler(
            store, catalogReader, availabilityReader).HandleAsync(
                new AddProductToSaleDraftCommand(
                    draft.Value.Id,
                    SellableProductLookup.ByProductId(ProductId.Create(productId).Value),
                    SaleQuantity.Create(quantity).Value,
                    draft.Value.Version),
                cancellationToken);
        Assert.True(added.IsSuccess, added.IsFailure ? added.Error.Code : null);
        return new DraftScenario(store, added.Value);
    }

    private static async Task<SaleFinalizationPreparation> PrepareAsync(
        Guid operationId,
        DraftScenario scenario,
        IStockAvailabilityReader availabilityReader,
        CancellationToken cancellationToken)
    {
        var prepared = await new PrepareSaleFinalizationHandler(
            scenario.Store,
            availabilityReader,
            new InMemorySaleFinalizationPreparationStore()).HandleAsync(
                new PrepareSaleFinalizationCommand(
                    operationId, scenario.Draft.Id, scenario.Draft.Version),
                cancellationToken);
        Assert.True(prepared.IsSuccess, prepared.IsFailure ? prepared.Error.Code : null);
        return prepared.Value;
    }

    private static RecordStockMovementCommand Opening(
        long productId,
        long locationId,
        long? batchId,
        int quantity) =>
        new(
            Guid.NewGuid(),
            StockMovementType.Opening,
            productId,
            locationId,
            batchId,
            quantity,
            "Ouverture test Sprint 13",
            "IntegrationTest",
            "S13");

    private static async Task<int> BalanceAsync(
        string connectionString,
        long? batchId,
        long? productId = null)
    {
        return batchId is not null
            ? await ScalarAsync<int>(
                connectionString,
                "SELECT AvailableQuantity FROM inventory.StockBalances WHERE StockBatchId=@batchId;",
                new SqlParameter("@batchId", SqlDbType.BigInt) { Value = batchId.Value })
            : await ScalarAsync<int>(
                connectionString,
                "SELECT AvailableQuantity FROM inventory.StockBalances WHERE StockBatchId IS NULL AND ProductId=@productId;",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = productId!.Value });
    }

    private static SqlConnectionStringBuilder BuildServerConnection()
    {
        var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("PHARMACY_TEST_SQLSERVER_CONNECTION est requise.");
        }

        return new SqlConnectionStringBuilder(raw)
        {
            InitialCatalog = "master",
            ApplicationName = "Pharmacy.Sprint13.Tests",
            Pooling = false
        };
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        ValidateTemporaryDatabaseName(databaseName);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE {QuoteIdentifier(databaseName)};";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropTemporaryDatabaseAsync(string connectionString, string databaseName)
    {
        ValidateTemporaryDatabaseName(databaseName);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"IF DB_ID(@name) IS NOT NULL BEGIN " +
            $"ALTER DATABASE {QuoteIdentifier(databaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE {QuoteIdentifier(databaseName)}; END;";
        command.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = databaseName });
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(
        string connectionString,
        string sql,
        params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("La requête scalaire n’a retourné aucune valeur.");
        return (T)Convert.ChangeType(
            value,
            typeof(T),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(
        string connectionString,
        string sql,
        params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static void ValidateTemporaryDatabaseName(string databaseName)
    {
        if (!Regex.IsMatch(
                databaseName,
                "^Pharmacy_Sprint13_Test_[0-9a-f]{12}$",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "Refus d’opérer sur une base qui ne respecte pas le préfixe Sprint 13.");
        }
    }

    private sealed record DraftScenario(
        InMemorySaleDraftStore Store,
        SaleDraftView Draft);

    private sealed class AsyncBarrier
    {
        private readonly int participantCount;
        private readonly TaskCompletionSource completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public AsyncBarrier(int participantCount) =>
            this.participantCount = participantCount;

        public Task SignalAndWaitAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref arrivals) == participantCount)
            {
                completion.TrySetResult();
            }

            return completion.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class FixedClock : ISalesClock, IInventoryDateProvider
    {
        public FixedClock(DateOnly currentDate, DateTime utcNow)
        {
            CurrentDate = currentDate;
            UtcNow = utcNow;
        }

        public DateOnly CurrentDate { get; }

        public DateTime UtcNow { get; }
    }

    private sealed class TestDbContextFactory : IDbContextFactory<PharmacyDbContext>
    {
        private readonly DbContextOptions<PharmacyDbContext> options;

        public TestDbContextFactory(string connectionString)
        {
            var builder = new DbContextOptionsBuilder<PharmacyDbContext>();
            builder.UsePharmacySqlServer(connectionString);
            options = builder.Options;
        }

        public PharmacyDbContext CreateDbContext() => new(options);

        public Task<PharmacyDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<PharmacyDbContext>
    {
        public PharmacyDbContext CreateDbContext() =>
            throw new InvalidOperationException("Connexion SQL indisponible simulée.");

        public Task<PharmacyDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Connexion SQL indisponible simulée.");
    }
}
