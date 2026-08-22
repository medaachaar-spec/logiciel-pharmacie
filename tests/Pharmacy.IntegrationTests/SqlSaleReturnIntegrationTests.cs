using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Commands;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Finalization.Models;
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
public sealed class SqlSaleReturnIntegrationTests
{
    private const string DatabasePrefix = "Pharmacy_RET002_Test_";

    [Fact]
    public async Task RET002_03_04_05_06_07_08_09_10_11_12_14_15_16_20_21_22_RealSqlReturnGatePasses()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var serverConnection = BuildServerConnection();
        var databaseName = DatabasePrefix + Guid.NewGuid().ToString("N")[..12];
        ValidateTemporaryDatabaseName(databaseName);
        var databaseConnection = new SqlConnectionStringBuilder(serverConnection.ConnectionString)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Pharmacy.RET002.Tests",
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
            }

            var catalogStore = new SqlCatalogStore(factory);
            var category = await catalogStore.CreateAsync(
                new CreateCatalogCategoryCommand(
                    "RET002",
                    "Retours RET-002",
                    DefaultTracksLots: true,
                    DefaultTracksExpiry: true),
                cancellationToken);
            Assert.True(category.IsSuccess);
            var productOne = await new CreateCatalogProductHandler(catalogStore, TestPermissionAuthorizer.AllowAll).HandleAsync(
                new CreateCatalogProductCommand(
                    "Médicament retour M1", "Boîte", category.Value.CategoryId,
                    "RET002-M1", 50m, TracksLots: true, TracksExpiry: true),
                cancellationToken);
            var productTwo = await new CreateCatalogProductHandler(catalogStore, TestPermissionAuthorizer.AllowAll).HandleAsync(
                new CreateCatalogProductCommand(
                    "Médicament retour M2", "Unité", category.Value.CategoryId,
                    "RET002-M2", 20m),
                cancellationToken);
            Assert.True(productOne.IsSuccess);
            Assert.True(productTwo.IsSuccess);

            var clock = new FixedClock(
                new DateOnly(2026, 8, 16),
                new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc));
            var inventory = new InventoryCommandHandlers(new SqlInventoryStore(factory), clock);
            var location = await inventory.CreateLocationAsync(
                new CreateStockLocationCommand(Guid.NewGuid(), "RET002-MAIN", "Stock retours"),
                cancellationToken);
            var batchA = await inventory.CreateBatchAsync(
                new CreateStockBatchCommand(
                    Guid.NewGuid(), productOne.Value.ProductId, "RET002-A", new DateOnly(2026, 10, 1)),
                cancellationToken);
            var batchB = await inventory.CreateBatchAsync(
                new CreateStockBatchCommand(
                    Guid.NewGuid(), productOne.Value.ProductId, "RET002-B", new DateOnly(2026, 12, 1)),
                cancellationToken);
            Assert.True(location.IsSuccess);
            Assert.True(batchA.IsSuccess);
            Assert.True(batchB.IsSuccess);
            Assert.True((await inventory.RecordOpeningAsync(
                Opening(productOne.Value.ProductId, location.Value.LocationId, batchA.Value.BatchId, 2),
                cancellationToken)).IsSuccess);
            Assert.True((await inventory.RecordOpeningAsync(
                Opening(productOne.Value.ProductId, location.Value.LocationId, batchB.Value.BatchId, 2),
                cancellationToken)).IsSuccess);
            Assert.True((await inventory.RecordOpeningAsync(
                Opening(productTwo.Value.ProductId, location.Value.LocationId, null, 2),
                cancellationToken)).IsSuccess);

            var catalogReader = new SqlProductCatalogReader(factory);
            var availability = new SqlInventoryStockAvailabilityReader(factory, clock);
            var draft = await CreateDraftAsync(
                new[]
                {
                    (productOne.Value.ProductId, 4),
                    (productTwo.Value.ProductId, 2)
                },
                catalogReader,
                availability,
                cancellationToken);
            var saleOperation = Guid.NewGuid();
            var preparation = await PrepareAsync(saleOperation, draft, availability, cancellationToken);
            var sale = await new SqlSaleFinalizer(factory, clock).FinalizeAsync(
                new FinalizeSaleRequest(
                    preparation,
                    new SalePaymentRequest(SalePaymentMethod.Cash, 240m, 240m, 0m, false)),
                cancellationToken);
            Assert.True(sale.IsSuccess, sale.IsFailure ? sale.Error.Code : null);
            var saleLines = sale.Value.Lines.OrderBy(line => line.ProductId.Value).ToArray();
            var lineOne = saleLines.Single(line => line.ProductId.Value == productOne.Value.ProductId);
            var lineTwo = saleLines.Single(line => line.ProductId.Value == productTwo.Value.ProductId);
            Assert.Equal(0, await BalanceAsync(databaseConnection.ConnectionString, batchA.Value.BatchId));
            Assert.Equal(0, await BalanceAsync(databaseConnection.ConnectionString, batchB.Value.BatchId));
            Assert.Equal(0, await BalanceAsync(
                databaseConnection.ConnectionString, null, productTwo.Value.ProductId));

            await ExecuteAsync(
                databaseConnection.ConnectionString,
                "UPDATE catalog.ProductLocalSettings SET CurrentSalePrice=70 WHERE ProductId=@productId;",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = productOne.Value.ProductId });
            await ExecuteAsync(
                databaseConnection.ConnectionString,
                "UPDATE inventory.StockBatches SET ExpiryDate='2026-08-15' WHERE Id=@batchId;",
                new SqlParameter("@batchId", SqlDbType.BigInt) { Value = batchA.Value.BatchId!.Value });

            var store = new SqlCustomerCreditCashStore(factory, clock);
            var firstOperation = Guid.NewGuid();
            var firstRequest = new ReturnSaleRequest(
                firstOperation,
                sale.Value.SaleId,
                new[]
                {
                    new ReturnSaleLineRequest(
                        lineOne.SaleLineId, 1, ReturnStockDestination.RestockApproved),
                    new ReturnSaleLineRequest(
                        lineTwo.SaleLineId, 1, ReturnStockDestination.NotRestockable)
                },
                ReturnRefundMethod.Cash);
            var firstReturn = await store.ReturnAsync(firstRequest, cancellationToken);

            // RET002_03/04/05/06/07/08/09: two decisions, partial multi-line and historical price.
            Assert.True(firstReturn.IsSuccess, firstReturn.IsFailure ? firstReturn.Error.Code : null);
            Assert.Equal(70m, firstReturn.Value.RefundAmount.Amount);
            Assert.Equal(2, firstReturn.Value.Lines.Count);
            Assert.Equal(1, await BalanceAsync(databaseConnection.ConnectionString, batchA.Value.BatchId));
            Assert.Equal(0, await BalanceAsync(databaseConnection.ConnectionString, batchB.Value.BatchId));
            Assert.Equal(0, await BalanceAsync(
                databaseConnection.ConnectionString, null, productTwo.Value.ProductId));
            var expiredRestockAvailability = await availability.GetAvailabilityAsync(
                ProductId.Create(productOne.Value.ProductId).Value,
                SaleQuantity.One,
                cancellationToken);
            Assert.True(expiredRestockAvailability.IsSuccess);
            Assert.Equal(0, expiredRestockAvailability.Value.AvailableQuantity);
            Assert.Equal(-70m, await ScalarAsync<decimal>(
                databaseConnection.ConnectionString,
                "SELECT Amount FROM cash.CashLedgerEntries WHERE SourceReference=@reference;",
                new SqlParameter("@reference", SqlDbType.NVarChar, 128) { Value = firstReturn.Value.Reference }));

            // RET002_12: same operation is a replay, a different payload is a conflict.
            var replay = await store.ReturnAsync(firstRequest, cancellationToken);
            Assert.True(replay.IsSuccess);
            Assert.Equal(firstReturn.Value.ReturnId, replay.Value.ReturnId);
            var conflictingReplay = await store.ReturnAsync(
                firstRequest with
                {
                    Lines = new[]
                    {
                        new ReturnSaleLineRequest(
                            lineOne.SaleLineId, 2, ReturnStockDestination.RestockApproved)
                    }
                },
                cancellationToken);
            Assert.Equal("sales.operation.conflict", conflictingReplay.Error.Code);
            Assert.Equal(1, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sales.SaleReturns WHERE OperationId=@operationId;",
                new SqlParameter("@operationId", SqlDbType.UniqueIdentifier) { Value = firstOperation }));

            // Successive restocks continue across the original FEFO allocations instead of overfilling lot A.
            var secondReturn = await store.ReturnAsync(
                new ReturnSaleRequest(
                    Guid.NewGuid(),
                    sale.Value.SaleId,
                    new[]
                    {
                        new ReturnSaleLineRequest(
                            lineOne.SaleLineId, 2, ReturnStockDestination.RestockApproved)
                    },
                    ReturnRefundMethod.Cash),
                cancellationToken);
            Assert.True(secondReturn.IsSuccess);
            Assert.Equal(100m, secondReturn.Value.RefundAmount.Amount);
            Assert.Equal(2, await BalanceAsync(databaseConnection.ConnectionString, batchA.Value.BatchId));
            Assert.Equal(1, await BalanceAsync(databaseConnection.ConnectionString, batchB.Value.BatchId));

            // RET002_10/11: excess, cumulative excess and duplicate lines are refused without mutation.
            var beforeRejected = await ScalarAsync<int>(
                databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.SaleReturns;");
            var excess = await store.ReturnAsync(
                new ReturnSaleRequest(
                    Guid.NewGuid(), sale.Value.SaleId,
                    new[]
                    {
                        new ReturnSaleLineRequest(
                            lineOne.SaleLineId, 2, ReturnStockDestination.NotRestockable)
                    },
                    ReturnRefundMethod.Cash),
                cancellationToken);
            Assert.Equal("sales.return.quantity_exceeded", excess.Error.Code);
            var duplicate = await store.ReturnAsync(
                new ReturnSaleRequest(
                    Guid.NewGuid(), sale.Value.SaleId,
                    new[]
                    {
                        new ReturnSaleLineRequest(
                            lineOne.SaleLineId, 1, ReturnStockDestination.NotRestockable),
                        new ReturnSaleLineRequest(
                            lineOne.SaleLineId, 1, ReturnStockDestination.NotRestockable)
                    },
                    ReturnRefundMethod.Cash),
                cancellationToken);
            Assert.Equal("sales.return.invalid", duplicate.Error.Code);
            Assert.Equal(beforeRejected, await ScalarAsync<int>(
                databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.SaleReturns;"));

            // RET002_21: two transactions compete for the last unit; only one logical return wins.
            var concurrent = await Task.WhenAll(
                store.ReturnAsync(
                    new ReturnSaleRequest(
                        Guid.NewGuid(), sale.Value.SaleId,
                        new[]
                        {
                            new ReturnSaleLineRequest(
                                lineOne.SaleLineId, 1, ReturnStockDestination.NotRestockable)
                        },
                        ReturnRefundMethod.Cash),
                    cancellationToken),
                store.ReturnAsync(
                    new ReturnSaleRequest(
                        Guid.NewGuid(), sale.Value.SaleId,
                        new[]
                        {
                            new ReturnSaleLineRequest(
                                lineOne.SaleLineId, 1, ReturnStockDestination.NotRestockable)
                        },
                        ReturnRefundMethod.Cash),
                    cancellationToken));
            Assert.Single(concurrent, result => result.IsSuccess);
            Assert.Single(concurrent, result => result.IsFailure);

            // RET002_14: a refund persistence collision rolls back return lines and tentative restock.
            var refundFailureOperation = Guid.NewGuid();
            await ExecuteAsync(
                databaseConnection.ConnectionString,
                "INSERT cash.CashLedgerEntries(OperationId,EntryType,Amount,SourceType,SourceReference,OccurredAtUtc,CreatedAtUtc) " +
                "VALUES(@operationId,N'RefundCashOut',-1,N'IntegrationTest',N'RET002-COLLISION',SYSUTCDATETIME(),SYSUTCDATETIME());",
                new SqlParameter("@operationId", SqlDbType.UniqueIdentifier)
                {
                    Value = DeterministicGuid(refundFailureOperation, "cash-refund")
                });
            var beforeRollbackReturns = await ScalarAsync<int>(
                databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.SaleReturns;");
            var refundFailure = await store.ReturnAsync(
                new ReturnSaleRequest(
                    refundFailureOperation, sale.Value.SaleId,
                    new[]
                    {
                        new ReturnSaleLineRequest(
                            lineTwo.SaleLineId, 1, ReturnStockDestination.RestockApproved)
                    },
                    ReturnRefundMethod.Cash),
                cancellationToken);
            Assert.Equal("sales.operation.conflict", refundFailure.Error.Code);
            Assert.Equal(beforeRollbackReturns, await ScalarAsync<int>(
                databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.SaleReturns;"));
            Assert.Equal(0, await BalanceAsync(
                databaseConnection.ConnectionString, null, productTwo.Value.ProductId));

            // RET002_15: a stock movement failure rolls back the return and refund atomically.
            await ExecuteAsync(
                databaseConnection.ConnectionString,
                "CREATE TRIGGER inventory.TR_RET002_BlockReturnMovement ON inventory.StockMovements AFTER INSERT AS " +
                "BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE SourceType=N'SaleReturn') THROW 51000,N'RET002 simulated stock failure',1; END;");
            var stockFailure = await store.ReturnAsync(
                new ReturnSaleRequest(
                    Guid.NewGuid(), sale.Value.SaleId,
                    new[]
                    {
                        new ReturnSaleLineRequest(
                            lineTwo.SaleLineId, 1, ReturnStockDestination.RestockApproved)
                    },
                    ReturnRefundMethod.Cash),
                cancellationToken);
            Assert.Equal("sales.return.concurrency_conflict", stockFailure.Error.Code);
            await ExecuteAsync(
                databaseConnection.ConnectionString,
                "DROP TRIGGER inventory.TR_RET002_BlockReturnMovement;");
            Assert.Equal(beforeRollbackReturns, await ScalarAsync<int>(
                databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sales.SaleReturns;"));
            Assert.Equal(0, await BalanceAsync(
                databaseConnection.ConnectionString, null, productTwo.Value.ProductId));

            // RET002_16/20: the original snapshot is immutable and a fresh reader rebuilds the return projection.
            Assert.Equal(4, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT Quantity FROM sales.SaleLines WHERE Id=@lineId;",
                new SqlParameter("@lineId", SqlDbType.BigInt) { Value = lineOne.SaleLineId }));
            Assert.Equal(50m, await ScalarAsync<decimal>(
                databaseConnection.ConnectionString,
                "SELECT UnitPrice FROM sales.SaleLines WHERE Id=@lineId;",
                new SqlParameter("@lineId", SqlDbType.BigInt) { Value = lineOne.SaleLineId }));
            await ExecuteAsync(
                databaseConnection.ConnectionString,
                "UPDATE catalog.Products SET IsActive=0 WHERE Id=@productId;",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = productOne.Value.ProductId });
            var reloaded = await new SqlSalesReader(new TestDbContextFactory(databaseConnection.ConnectionString))
                .GetPreparationAsync(sale.Value.SaleId, cancellationToken);
            Assert.True(reloaded.IsSuccess);
            Assert.Equal(4, reloaded.Value.Lines.Single(line => line.SaleLineId == lineOne.SaleLineId).ReturnedQuantity);
            Assert.Equal(1, reloaded.Value.Lines.Single(line => line.SaleLineId == lineTwo.SaleLineId).ReturnedQuantity);
            Assert.Equal(beforeRollbackReturns, reloaded.Value.PreviousReturns.Count);

            // RET002_22: the return schema persists no PAN, CVV or card number.
            Assert.Equal(0, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id " +
                "JOIN sys.schemas s ON s.schema_id=t.schema_id " +
                "WHERE s.name=N'sales' AND t.name IN(N'SaleReturns',N'SaleReturnLines') " +
                "AND (c.name LIKE N'%PAN%' OR c.name LIKE N'%CVV%' OR c.name LIKE N'%CardNumber%');"));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropTemporaryDatabaseAsync(serverConnection.ConnectionString, databaseName);
        }
    }

    private static async Task<DraftScenario> CreateDraftAsync(
        IReadOnlyList<(long ProductId, int Quantity)> requestedLines,
        IProductCatalogReader catalog,
        IStockAvailabilityReader availability,
        CancellationToken cancellationToken)
    {
        var store = new InMemorySaleDraftStore();
        var created = await new CreateSaleDraftHandler(
            new InMemorySaleDraftIdGenerator(), store).HandleAsync(cancellationToken);
        Assert.True(created.IsSuccess);
        var draft = created.Value;
        foreach (var requested in requestedLines)
        {
            var added = await new AddProductToSaleDraftHandler(store, catalog, availability).HandleAsync(
                new AddProductToSaleDraftCommand(
                    draft.Id,
                    SellableProductLookup.ByProductId(ProductId.Create(requested.ProductId).Value),
                    SaleQuantity.Create(requested.Quantity).Value,
                    draft.Version),
                cancellationToken);
            Assert.True(added.IsSuccess, added.IsFailure ? added.Error.Code : null);
            draft = added.Value;
        }
        return new DraftScenario(store, draft);
    }

    private static async Task<SaleFinalizationPreparation> PrepareAsync(
        Guid operationId,
        DraftScenario scenario,
        IStockAvailabilityReader availability,
        CancellationToken cancellationToken)
    {
        var prepared = await new PrepareSaleFinalizationHandler(
            scenario.Store,
            availability,
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
        int quantity) => new(
            Guid.NewGuid(),
            StockMovementType.Opening,
            productId,
            locationId,
            batchId,
            quantity,
            "Ouverture RET-002",
            "IntegrationTest",
            "RET002");

    private static async Task<int> BalanceAsync(
        string connectionString,
        long? batchId,
        long? productId = null) => batchId is not null
        ? await ScalarAsync<int>(
            connectionString,
            "SELECT AvailableQuantity FROM inventory.StockBalances WHERE StockBatchId=@batchId;",
            new SqlParameter("@batchId", SqlDbType.BigInt) { Value = batchId.Value })
        : await ScalarAsync<int>(
            connectionString,
            "SELECT AvailableQuantity FROM inventory.StockBalances WHERE StockBatchId IS NULL AND ProductId=@productId;",
            new SqlParameter("@productId", SqlDbType.BigInt) { Value = productId!.Value });

    private static Guid DeterministicGuid(Guid operationId, string purpose)
    {
        var bytes = Encoding.UTF8.GetBytes($"{operationId:N}|{purpose}");
        return new Guid(SHA256.HashData(bytes).AsSpan(0, 16));
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
            ApplicationName = "Pharmacy.RET002.Tests",
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
            $"IF DB_ID(@name) IS NOT NULL BEGIN ALTER DATABASE {QuoteIdentifier(databaseName)} " +
            $"SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {QuoteIdentifier(databaseName)}; END;";
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
            ?? throw new InvalidOperationException("Scalar query returned no value.");
        return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
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

    private static string QuoteIdentifier(string value) =>
        $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static void ValidateTemporaryDatabaseName(string databaseName)
    {
        if (!Regex.IsMatch(
                databaseName,
                "^Pharmacy_RET002_Test_[0-9a-f]{12}$",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("Refus d’opérer sur une base hors préfixe RET-002.");
        }
    }

    private sealed record DraftScenario(InMemorySaleDraftStore Store, SaleDraftView Draft);

    private sealed class FixedClock(DateOnly currentDate, DateTime utcNow) :
        ISalesClock,
        IInventoryDateProvider
    {
        public DateOnly CurrentDate { get; } = currentDate;
        public DateTime UtcNow { get; } = utcNow;
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
}
