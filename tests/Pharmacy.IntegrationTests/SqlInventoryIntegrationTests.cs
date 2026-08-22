using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Inventory.Queries;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Catalog;
using Pharmacy.Infrastructure.Inventory;
using Pharmacy.Infrastructure.Persistence;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class SqlInventoryIntegrationTests
{
    private const string TestDatabasePrefix = "Pharmacy_Sprint11_Test_";

    [Fact]
    public async Task RealSqlInventoryValidatesAtomicMovementsFefoPosConcurrencyAndInventoryFinalization()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var serverConnection = BuildServerConnection();
        var databaseName = TestDatabasePrefix + Guid.NewGuid().ToString("N")[..12];
        ValidateTemporaryDatabaseName(databaseName);
        var databaseConnection = new SqlConnectionStringBuilder(serverConnection.ConnectionString)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Pharmacy.Sprint11.Tests",
            Pooling = false
        };
        await CreateDatabaseAsync(serverConnection.ConnectionString, databaseName);

        try
        {
            var factory = new TestDbContextFactory(databaseConnection.ConnectionString);

            await using (var migrationContext = await factory.CreateDbContextAsync(cancellationToken))
            {
                await migrationContext.Database.MigrateAsync(cancellationToken);
                Assert.Empty(await migrationContext.Database.GetPendingMigrationsAsync(cancellationToken));
                Assert.Contains(await migrationContext.Database.GetAppliedMigrationsAsync(cancellationToken), migration =>
                    migration.EndsWith("_InventoryPersistenceV01", StringComparison.Ordinal));
            }

            Assert.Equal(6, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'inventory';"));
            Assert.Equal(0, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id " +
                "JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'inventory' AND TYPE_NAME(c.user_type_id) IN (N'float',N'real');"));

            var catalogStore = new SqlCatalogStore(factory);
            var category = await catalogStore.CreateAsync(
                new CreateCatalogCategoryCommand("S11-TEST", "Catégorie test Inventory", DefaultTracksLots: true, DefaultTracksExpiry: true),
                cancellationToken);
            Assert.True(category.IsSuccess);
            var product = await new CreateCatalogProductHandler(catalogStore, TestPermissionAuthorizer.AllowAll).HandleAsync(
                new CreateCatalogProductCommand(
                    "Produit lots Sprint 11", "Boîte test", category.Value.CategoryId,
                    "S11-INVENTORY-001", 10m, TracksLots: true, TracksExpiry: true),
                cancellationToken);
            Assert.True(product.IsSuccess);

            var dateProvider = new FixedDateProvider(new DateOnly(2026, 8, 4));
            var store = new SqlInventoryStore(factory);
            var reader = new SqlInventoryReader(factory);
            var commands = new InventoryCommandHandlers(store, dateProvider);
            var queries = new InventoryQueryHandlers(reader, dateProvider);

            var locationOperation = Guid.NewGuid();
            var locationCommand = new CreateStockLocationCommand(
                locationOperation, "S11-MAIN", "Emplacement principal test", true);
            var location = await commands.CreateLocationAsync(locationCommand, cancellationToken);
            var locationReplay = await commands.CreateLocationAsync(locationCommand, cancellationToken);
            var locationConflict = await commands.CreateLocationAsync(
                locationCommand with { Name = "Autre signification" }, cancellationToken);
            Assert.True(location.IsSuccess);
            Assert.Equal(location.Value.LocationId, locationReplay.Value.LocationId);
            Assert.Equal("inventory.operation.conflict", locationConflict.Error.Code);
            Assert.Equal(8, Convert.FromBase64String(location.Value.RowVersion).Length);

            var near = await commands.CreateBatchAsync(
                new CreateStockBatchCommand(Guid.NewGuid(), product.Value.ProductId, "S11-NEAR", new DateOnly(2026, 9, 1)),
                cancellationToken);
            var far = await commands.CreateBatchAsync(
                new CreateStockBatchCommand(Guid.NewGuid(), product.Value.ProductId, "S11-FAR", new DateOnly(2026, 12, 31)),
                cancellationToken);
            var expired = await commands.CreateBatchAsync(
                new CreateStockBatchCommand(Guid.NewGuid(), product.Value.ProductId, "S11-EXPIRED", new DateOnly(2026, 8, 3)),
                cancellationToken);
            Assert.All(new[] { near, far, expired }, result => Assert.True(result.IsSuccess));

            var openingOperation = Guid.NewGuid();
            var opening = Opening(
                openingOperation, product.Value.ProductId, location.Value.LocationId, near.Value.BatchId, 4);
            var firstOpening = await commands.RecordOpeningAsync(opening, cancellationToken);
            var replayOpening = await commands.RecordOpeningAsync(opening, cancellationToken);
            var conflictingReplay = await commands.RecordOpeningAsync(opening with { Quantity = 5 }, cancellationToken);
            Assert.True(firstOpening.IsSuccess);
            Assert.Equal(firstOpening.Value.MovementId, replayOpening.Value.MovementId);
            Assert.Equal("inventory.operation.conflict", conflictingReplay.Error.Code);

            Assert.True((await commands.RecordOpeningAsync(
                Opening(Guid.NewGuid(), product.Value.ProductId, location.Value.LocationId, far.Value.BatchId, 8),
                cancellationToken)).IsSuccess);
            Assert.True((await commands.RecordOpeningAsync(
                Opening(Guid.NewGuid(), product.Value.ProductId, location.Value.LocationId, expired.Value.BatchId, 7),
                cancellationToken)).IsSuccess);

            var overview = await queries.SearchStockAsync(
                new("S11-INVENTORY", location.Value.LocationId), cancellationToken);
            var summary = Assert.Single(overview.Value);
            Assert.Equal(19, summary.TotalQuantity);
            Assert.Equal(12, summary.SellableQuantity);
            Assert.Equal(7, summary.ExpiredQuantity);

            var availabilityReader = new SqlInventoryStockAvailabilityReader(factory, dateProvider);
            var availability = await availabilityReader.GetAvailabilityAsync(
                ProductId.Create(product.Value.ProductId).Value,
                SaleQuantity.Create(13).Value,
                cancellationToken);
            Assert.True(availability.IsSuccess);
            Assert.Equal(12, availability.Value.AvailableQuantity);
            Assert.False(availability.Value.IsAvailable);

            var fefo = await queries.SuggestFefoAsync(product.Value.ProductId, 10, cancellationToken);
            Assert.True(fefo.IsSuccess);
            Assert.Collection(
                fefo.Value.Allocations,
                line => { Assert.Equal("S11-NEAR", line.LotNumber); Assert.Equal(4, line.Quantity); },
                line => { Assert.Equal("S11-FAR", line.LotNumber); Assert.Equal(6, line.Quantity); });
            Assert.DoesNotContain(fefo.Value.Allocations, line => line.LotNumber == "S11-EXPIRED");

            var detailsBefore = await queries.GetProductDetailsAsync(product.Value.ProductId, location.Value.LocationId, cancellationToken);
            var farBalance = Assert.Single(detailsBefore.Value.Balances, balance => balance.BatchId == far.Value.BatchId);
            var increased = await commands.IncreaseAsync(
                Change(Guid.NewGuid(), StockMovementType.ManualIncrease, product.Value.ProductId,
                    location.Value.LocationId, far.Value.BatchId, 2, farBalance.BalanceRowVersion),
                cancellationToken);
            var staleWrite = await commands.IncreaseAsync(
                Change(Guid.NewGuid(), StockMovementType.ManualIncrease, product.Value.ProductId,
                    location.Value.LocationId, far.Value.BatchId, 1, farBalance.BalanceRowVersion),
                cancellationToken);
            Assert.True(increased.IsSuccess);
            Assert.Equal("inventory.concurrency_conflict", staleWrite.Error.Code);

            var movementsBeforeInsufficient = await ScalarAsync<int>(
                databaseConnection.ConnectionString, "SELECT COUNT(*) FROM inventory.StockMovements;");
            var currentDetails = await queries.GetProductDetailsAsync(product.Value.ProductId, location.Value.LocationId, cancellationToken);
            farBalance = Assert.Single(currentDetails.Value.Balances, balance => balance.BatchId == far.Value.BatchId);
            var insufficient = await commands.DecreaseAsync(
                Change(Guid.NewGuid(), StockMovementType.ManualDecrease, product.Value.ProductId,
                    location.Value.LocationId, far.Value.BatchId, 999, farBalance.BalanceRowVersion),
                cancellationToken);
            Assert.Equal("inventory.stock.insufficient", insufficient.Error.Code);
            Assert.Equal(movementsBeforeInsufficient, await ScalarAsync<int>(
                databaseConnection.ConnectionString, "SELECT COUNT(*) FROM inventory.StockMovements;"));
            Assert.Equal(10, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT AvailableQuantity FROM inventory.StockBalances WHERE StockBatchId=@batchId;",
                new SqlParameter("@batchId", SqlDbType.BigInt) { Value = far.Value.BatchId!.Value }));

            var createdSession = await commands.CreateSessionAsync(
                new CreateInventorySessionCommand(Guid.NewGuid(), "S11-COUNT-001", location.Value.LocationId),
                cancellationToken);
            var session = await commands.StartSessionAsync(
                new StartInventorySessionCommand(Guid.NewGuid(), createdSession.Value.SessionId, createdSession.Value.RowVersion),
                cancellationToken);
            Assert.True(session.IsSuccess, session.IsFailure ? session.Error.Code : null);
            Assert.Equal(3, session.Value.Lines.Count);

            foreach (var originalLine in session.Value.Lines)
            {
                var currentLine = session.Value.Lines.Single(line => line.CountLineId == originalLine.CountLineId);
                var counted = currentLine.LotNumber == "S11-NEAR"
                    ? currentLine.ExpectedQuantity + 2
                    : currentLine.ExpectedQuantity;
                session = await commands.SaveCountLineAsync(
                    new SaveInventoryCountLineCommand(
                        Guid.NewGuid(), session.Value.SessionId, currentLine.CountLineId, counted,
                        session.Value.RowVersion, currentLine.RowVersion),
                    cancellationToken);
                Assert.True(session.IsSuccess);
            }

            var finalizationOperation = Guid.NewGuid();
            var finalizeCommand = new FinalizeInventorySessionCommand(
                finalizationOperation, session.Value.SessionId, session.Value.RowVersion);
            var finalized = await commands.FinalizeSessionAsync(finalizeCommand, cancellationToken);
            var replayFinalization = await commands.FinalizeSessionAsync(finalizeCommand, cancellationToken);
            var doubleFinalization = await commands.FinalizeSessionAsync(
                finalizeCommand with { OperationId = Guid.NewGuid() }, cancellationToken);
            Assert.True(finalized.IsSuccess);
            Assert.Equal(InventorySessionStatus.Completed, finalized.Value.Status);
            Assert.True(replayFinalization.IsSuccess);
            Assert.Equal("inventory.session.already_completed", doubleFinalization.Error.Code);
            Assert.Contains(
                (await queries.GetMovementsAsync(product.Value.ProductId, location.Value.LocationId, cancellationToken: cancellationToken)).Value,
                movement => movement.MovementType == StockMovementType.InventoryIncrease && movement.Quantity == 2);

            Assert.Equal(0, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM inventory.StockBalances b WHERE b.AvailableQuantity <> " +
                "(SELECT COALESCE(SUM(CASE WHEN m.MovementType IN (N'Opening',N'ManualIncrease',N'InventoryIncrease') THEN m.Quantity ELSE -m.Quantity END),0) " +
                "FROM inventory.StockMovements m WHERE m.StockLocationId=b.StockLocationId AND m.ProductId=b.ProductId " +
                "AND ((m.StockBatchId=b.StockBatchId) OR (m.StockBatchId IS NULL AND b.StockBatchId IS NULL)));"));

            var negative = await Assert.ThrowsAsync<SqlException>(() => ExecuteSqlAsync(
                databaseConnection.ConnectionString,
                "UPDATE inventory.StockBalances SET AvailableQuantity=-1 WHERE StockBatchId=@batchId;",
                new SqlParameter("@batchId", SqlDbType.BigInt) { Value = near.Value.BatchId!.Value }));
            Assert.Equal(547, negative.Number);

            var cancelledDraft = await commands.CreateSessionAsync(
                new CreateInventorySessionCommand(Guid.NewGuid(), "S11-CANCEL-001", location.Value.LocationId),
                cancellationToken);
            var cancelled = await commands.CancelSessionAsync(
                new CancelInventorySessionCommand(Guid.NewGuid(), cancelledDraft.Value.SessionId, cancelledDraft.Value.RowVersion),
                cancellationToken);
            Assert.Equal(InventorySessionStatus.Cancelled, cancelled.Value.Status);

            await using (var rollbackContext = await factory.CreateDbContextAsync(cancellationToken))
            {
                await rollbackContext.GetService<IMigrator>().MigrateAsync(
                    "20260803215422_CatalogPersistenceEnhancementsV01",
                    cancellationToken);
            }

            Assert.Equal(0, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'inventory';"));
            Assert.Equal(5, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'catalog';"));

            await using (var reapplyContext = await factory.CreateDbContextAsync(cancellationToken))
            {
                await reapplyContext.Database.MigrateAsync(cancellationToken);
                Assert.Empty(await reapplyContext.Database.GetPendingMigrationsAsync(cancellationToken));
            }

            Assert.Equal(6, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'inventory';"));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropTemporaryDatabaseAsync(serverConnection.ConnectionString, databaseName);
        }
    }

    private static RecordStockMovementCommand Opening(
        Guid operationId, long productId, long locationId, long? batchId, int quantity) =>
        new(operationId, StockMovementType.Opening, productId, locationId, batchId, quantity,
            "Ouverture test Sprint 11", "IntegrationTest", "S11");

    private static RecordStockMovementCommand Change(
        Guid operationId,
        StockMovementType type,
        long productId,
        long locationId,
        long? batchId,
        int quantity,
        string rowVersion) =>
        new(operationId, type, productId, locationId, batchId, quantity,
            "Ajustement test Sprint 11", "IntegrationTest", "S11", rowVersion);

    private static SqlConnectionStringBuilder BuildServerConnection()
    {
        var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "La variable PHARMACY_TEST_SQLSERVER_CONNECTION est requise pour les preuves SQL Server réelles.");
        }

        return new SqlConnectionStringBuilder(raw)
        {
            InitialCatalog = "master",
            ApplicationName = "Pharmacy.Sprint11.Tests",
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
        return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteSqlAsync(
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

    private static string QuoteIdentifier(string name) =>
        $"[{name.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static void ValidateTemporaryDatabaseName(string databaseName)
    {
        if (!Regex.IsMatch(
                databaseName,
                "^Pharmacy_Sprint11_Test_[0-9a-f]{12}$",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("Refus d’opérer sur une base qui ne respecte pas le préfixe Sprint 11.");
        }
    }

    private sealed class FixedDateProvider : IInventoryDateProvider
    {
        public FixedDateProvider(DateOnly currentDate) => CurrentDate = currentDate;
        public DateOnly CurrentDate { get; }
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

        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}
