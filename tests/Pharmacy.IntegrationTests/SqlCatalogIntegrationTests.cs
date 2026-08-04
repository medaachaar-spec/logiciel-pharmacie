using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Catalog.Models;
using Pharmacy.Application.Catalog.Queries;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Catalog;
using Pharmacy.Infrastructure.Persistence;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class SqlCatalogIntegrationTests
{
    private const string TestDatabasePrefix = "Pharmacy_Sprint10_Test_";
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public async Task RealSqlCatalogValidatesUseCasesTransactionsConcurrencySearchAndPos()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var serverConnection = BuildServerConnection();
        var databaseName = TestDatabasePrefix + Guid.NewGuid().ToString("N")[..12];
        ValidateTemporaryDatabaseName(databaseName);
        var databaseConnection = new SqlConnectionStringBuilder(serverConnection.ConnectionString)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Pharmacy.Sprint10.Tests",
            Pooling = false
        };
        await CreateDatabaseAsync(serverConnection.ConnectionString, databaseName);

        try
        {
            var interceptor = new CountingCommandInterceptor();
            var factory = new TestDbContextFactory(databaseConnection.ConnectionString, interceptor);

            await using (var migrationContext = await factory.CreateDbContextAsync(cancellationToken))
            {
                await migrationContext.Database.MigrateAsync(cancellationToken);
                Assert.Empty(await migrationContext.Database.GetPendingMigrationsAsync(cancellationToken));
                Assert.Contains(await migrationContext.Database.GetAppliedMigrationsAsync(cancellationToken),
                    migration => migration.EndsWith("_CatalogPersistenceEnhancementsV01", StringComparison.Ordinal));
            }

            var reader = new SqlProductCatalogReader(factory);
            var store = new SqlCatalogStore(factory);
            var categoryResult = await store.CreateAsync(
                new CreateCatalogCategoryCommand("S10-DEMO", "Catégorie synthétique Sprint 10"),
                cancellationToken);
            Assert.True(categoryResult.IsSuccess);

            var createHandler = new CreateCatalogProductHandler(store);
            var created = await createHandler.HandleAsync(
                new CreateCatalogProductCommand(
                    "Produit SQL Sprint 10",
                    "Boîte synthétique 20 unités",
                    categoryResult.Value.CategoryId,
                    "S10-INTERNAL-0001",
                    12.5000m,
                    [new CatalogBarcodeInput("000S10-DEMO", true)]),
                cancellationToken);
            Assert.True(created.IsSuccess);
            Assert.Equal(12.5000m, created.Value.CurrentSalePrice);
            Assert.Single(created.Value.Barcodes);
            Assert.Equal("000S10-DEMO", created.Value.Barcodes[0].NormalizedValue);
            Assert.Single(created.Value.PriceHistory);

            interceptor.Reset();
            var listByName = await reader.SearchAsync(
                new CatalogProductSearchCriteria("produit sql", Limit: 10),
                cancellationToken);
            Assert.True(listByName.IsSuccess);
            Assert.Single(listByName.Value);
            Assert.Equal(1, interceptor.ReaderCommands);
            Assert.Single((await reader.SearchAsync(
                new CatalogProductSearchCriteria("boîte synthétique", Limit: 10),
                cancellationToken)).Value);
            Assert.Single((await reader.SearchAsync(
                new CatalogProductSearchCriteria("S10-INTERNAL", Limit: 10),
                cancellationToken)).Value);
            Assert.Single((await reader.SearchAsync(
                new CatalogProductSearchCriteria("000s10-demo", Limit: 10),
                cancellationToken)).Value);

            IProductCatalogReader salesReader = reader;
            var byId = await salesReader.FindByIdAsync(
                ProductId.Create(created.Value.ProductId).Value,
                cancellationToken);
            var byReference = await salesReader.FindByCodeAsync("s10-internal-0001", cancellationToken);
            var byBarcode = await salesReader.FindByCodeAsync("000s10-demo", cancellationToken);
            var unknown = await salesReader.FindByCodeAsync("S10-UNKNOWN", cancellationToken);
            var salesSearch = await salesReader.SearchAsync(
                new ProductSearchQuery("boîte synthétique"),
                cancellationToken);
            Assert.True(byId.IsSuccess);
            Assert.True(byReference.IsSuccess);
            Assert.True(byBarcode.IsSuccess);
            Assert.True(unknown.IsFailure);
            Assert.Equal("sales.product.not_found", unknown.Error.Code);
            Assert.Single(salesSearch.Value);
            Assert.Equal(12.5000m, byBarcode.Value.UnitSalePrice.Amount);

            var duplicateReference = await createHandler.HandleAsync(
                new CreateCatalogProductCommand(
                    "Autre produit synthétique", null, categoryResult.Value.CategoryId,
                    "S10-INTERNAL-0001", 1m),
                cancellationToken);
            Assert.True(duplicateReference.IsFailure);
            Assert.Equal("catalog.reference.duplicate", duplicateReference.Error.Code);

            var duplicateBarcode = await createHandler.HandleAsync(
                new CreateCatalogProductCommand(
                    "Autre produit synthétique", null, categoryResult.Value.CategoryId,
                    "S10-INTERNAL-0002", 1m,
                    [new CatalogBarcodeInput("000s10-demo")]),
                cancellationToken);
            Assert.True(duplicateBarcode.IsFailure);
            Assert.Equal("catalog.barcode.duplicate", duplicateBarcode.Error.Code);

            var missingCategory = await createHandler.HandleAsync(
                new CreateCatalogProductCommand(
                    "Produit sans catégorie", null, long.MaxValue,
                    "S10-INTERNAL-MISSING", 2m),
                cancellationToken);
            Assert.True(missingCategory.IsFailure);
            Assert.Equal("catalog.category.not_found", missingCategory.Error.Code);
            Assert.Equal(0, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM catalog.ProductLocalSettings WHERE InternalCode = N'S10-INTERNAL-MISSING';"));

            var negativePrice = await new ChangeCatalogProductPriceHandler(store).HandleAsync(
                new ChangeCatalogProductPriceCommand(
                    created.Value.ProductId, -1m, null, created.Value.LocalSettingsRowVersion),
                cancellationToken);
            Assert.True(negativePrice.IsFailure);
            Assert.Equal("catalog.price.invalid", negativePrice.Error.Code);

            var staleDetails = created.Value;
            var changedPrice = await new ChangeCatalogProductPriceHandler(store).HandleAsync(
                new ChangeCatalogProductPriceCommand(
                    staleDetails.ProductId, 19.9900m, "Test synthétique", staleDetails.LocalSettingsRowVersion),
                cancellationToken);
            Assert.True(changedPrice.IsSuccess);
            Assert.Equal(19.9900m, changedPrice.Value.CurrentSalePrice);
            Assert.Equal(2, changedPrice.Value.PriceHistory.Count);

            var conflict = await new ChangeCatalogProductPriceHandler(store).HandleAsync(
                new ChangeCatalogProductPriceCommand(
                    staleDetails.ProductId, 29.9900m, "Conflit synthétique", staleDetails.LocalSettingsRowVersion),
                cancellationToken);
            Assert.True(conflict.IsFailure);
            Assert.Equal("catalog.product.concurrency_conflict", conflict.Error.Code);
            var afterConflict = (await reader.GetByIdAsync(staleDetails.ProductId, cancellationToken)).Value;
            Assert.Equal(19.9900m, afterConflict.CurrentSalePrice);
            Assert.Equal(2, afterConflict.PriceHistory.Count);

            var deactivated = await new SetCatalogProductActivationHandler(store).HandleAsync(
                new SetCatalogProductActivationCommand(
                    afterConflict.ProductId, false, false,
                    afterConflict.ProductRowVersion, afterConflict.LocalSettingsRowVersion),
                cancellationToken);
            Assert.True(deactivated.IsSuccess);
            Assert.True((await salesReader.FindByIdAsync(
                ProductId.Create(afterConflict.ProductId).Value,
                cancellationToken)).IsFailure);
            Assert.Single((await reader.SearchAsync(
                new CatalogProductSearchCriteria(
                    Status: CatalogProductStatusFilter.Inactive,
                    Limit: 10),
                cancellationToken)).Value);

            var reactivated = await new SetCatalogProductActivationHandler(store).HandleAsync(
                new SetCatalogProductActivationCommand(
                    deactivated.Value.ProductId, true, true,
                    deactivated.Value.ProductRowVersion, deactivated.Value.LocalSettingsRowVersion),
                cancellationToken);
            Assert.True(reactivated.IsSuccess);

            var barcode = Assert.Single(reactivated.Value.Barcodes);
            var barcodeDisabled = await new UpdateCatalogBarcodeHandler(store).HandleAsync(
                new UpdateCatalogBarcodeCommand(
                    reactivated.Value.ProductId, barcode.BarcodeId, barcode.Value,
                    barcode.IsPrimary, false, barcode.RowVersion, barcode.BarcodeType),
                cancellationToken);
            Assert.True(barcodeDisabled.IsSuccess);
            Assert.True((await salesReader.FindByCodeAsync("000S10-DEMO", cancellationToken)).IsFailure);
            Assert.True((await salesReader.FindByCodeAsync("S10-INTERNAL-0001", cancellationToken)).IsSuccess);

            var staleCategory = categoryResult.Value;
            var categoryUpdated = await store.UpdateAsync(
                new UpdateCatalogCategoryCommand(
                    staleCategory.CategoryId, staleCategory.Code, "Catégorie modifiée Sprint 10", 1,
                    false, false, false, staleCategory.RowVersion),
                cancellationToken);
            Assert.True(categoryUpdated.IsSuccess);
            var categoryConflict = await store.UpdateAsync(
                new UpdateCatalogCategoryCommand(
                    staleCategory.CategoryId, staleCategory.Code, "Écrasement interdit", 2,
                    false, false, false, staleCategory.RowVersion),
                cancellationToken);
            Assert.True(categoryConflict.IsFailure);
            Assert.Equal("catalog.category.concurrency_conflict", categoryConflict.Error.Code);
            var categoryDisabled = await store.SetActivationAsync(
                new SetCatalogCategoryActivationCommand(
                    categoryUpdated.Value.CategoryId, false, categoryUpdated.Value.RowVersion),
                cancellationToken);
            Assert.True(categoryDisabled.IsSuccess);
            Assert.Equal(1, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM catalog.ProductCategories WHERE Id = @id;",
                new SqlParameter("@id", SqlDbType.BigInt) { Value = staleCategory.CategoryId }));

            Assert.Equal(0, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id " +
                "WHERE s.name = N'inventory' OR t.name LIKE N'%Stock%';"));

            var seedScript = await File.ReadAllTextAsync(Path.Combine(
                RepositoryRoot, "database", "dev", "seed_synthetic_catalog_v01.sql"),
                cancellationToken);
            var wrongTarget = await Assert.ThrowsAsync<SqlException>(() =>
                ExecuteSqlAsync(databaseConnection.ConnectionString, seedScript));
            Assert.Equal(51010, wrongTarget.Number);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropTemporaryDatabaseAsync(serverConnection.ConnectionString, databaseName);
        }
    }

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
            ApplicationName = "Pharmacy.Sprint10.Tests",
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

    private static async Task ExecuteSqlAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string QuoteIdentifier(string name) =>
        $"[{name.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static void ValidateTemporaryDatabaseName(string databaseName)
    {
        if (!Regex.IsMatch(
                databaseName,
                "^Pharmacy_Sprint10_Test_[0-9a-f]{12}$",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("Refus d’opérer sur une base qui ne respecte pas le préfixe Sprint 10.");
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class TestDbContextFactory : IDbContextFactory<PharmacyDbContext>
    {
        private readonly DbContextOptions<PharmacyDbContext> options;

        public TestDbContextFactory(string connectionString, DbCommandInterceptor interceptor)
        {
            var builder = new DbContextOptionsBuilder<PharmacyDbContext>();
            builder.UsePharmacySqlServer(connectionString);
            builder.AddInterceptors(interceptor);
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

    private sealed class CountingCommandInterceptor : DbCommandInterceptor
    {
        private int readerCommands;

        public int ReaderCommands => Volatile.Read(ref readerCommands);

        public void Reset() => Volatile.Write(ref readerCommands, 0);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref readerCommands);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
