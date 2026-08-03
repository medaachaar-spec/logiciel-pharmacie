using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Catalog;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class SqlServerPersistenceIntegrationTests
{
    private const string TestDatabasePrefix = "Pharmacy_Sprint09_Test_";
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public async Task RealSqlServerValidatesMigrationSchemaConstraintsConcurrencyRollbackAndReapplication()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var serverConnection = BuildServerConnection();
        var databaseName = TestDatabasePrefix + Guid.NewGuid().ToString("N")[..12];
        ValidateTemporaryDatabaseName(databaseName);
        var databaseConnection = new SqlConnectionStringBuilder(serverConnection.ConnectionString)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Pharmacy.Sprint09.Tests",
            Pooling = false
        };

        await CreateDatabaseAsync(serverConnection.ConnectionString, databaseName);

        try
        {
            await ValidateServerIdentityAsync(databaseConnection.ConnectionString);

            await using (var context = CreateContext(databaseConnection.ConnectionString))
            {
                await context.Database.MigrateAsync(cancellationToken);
                Assert.Empty(await context.Database.GetPendingMigrationsAsync(cancellationToken));
                Assert.Single(await context.Database.GetAppliedMigrationsAsync(cancellationToken), migration =>
                    migration.EndsWith("_InitialCatalogPersistenceV01", StringComparison.Ordinal));
            }

            var expectedTables = new[]
            {
                "ProductBarcodes",
                "ProductCategories",
                "ProductLocalSettings",
                "ProductPriceHistories",
                "Products"
            };
            Assert.Equal(expectedTables, await CatalogTablesAsync(databaseConnection.ConnectionString));
            Assert.Equal(4, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sys.columns c " +
                "JOIN sys.tables t ON t.object_id = c.object_id " +
                "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
                "WHERE s.name = N'catalog' AND c.system_type_id = 189;"));
            Assert.Equal(4, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM sys.foreign_keys fk " +
                "JOIN sys.tables t ON t.object_id = fk.parent_object_id " +
                "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
                "WHERE s.name = N'catalog' AND fk.delete_referential_action = 0;"));

            var schemaFingerprintBefore = await SchemaFingerprintAsync(databaseConnection.ConnectionString);
            Assert.NotEmpty(schemaFingerprintBefore);

            var (categoryId, productId, originalVersion) = await InsertSyntheticCatalogGraphAsync(
                databaseConnection.ConnectionString);
            Assert.True(categoryId > 0);
            Assert.True(productId > 0);
            Assert.NotEmpty(originalVersion);

            await AssertSqlFailureAsync(
                databaseConnection.ConnectionString,
                "INSERT INTO [catalog].[ProductBarcodes] " +
                "([ProductId], [Value], [NormalizedValue], [IsPrimary], [BarcodeType], [IsActive], [CreatedAtUtc]) " +
                "VALUES (@productId, N'S09-DUPLICATE', N'S09-000001', 0, N'Synthetic', 1, SYSUTCDATETIME());",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = productId },
                2601,
                2627);
            await AssertSqlFailureAsync(
                databaseConnection.ConnectionString,
                "INSERT INTO [catalog].[ProductBarcodes] " +
                "([ProductId], [Value], [NormalizedValue], [IsPrimary], [BarcodeType], [IsActive], [CreatedAtUtc]) " +
                "VALUES (@productId, N'S09-PRIMARY-2', N'S09-PRIMARY-2', 1, N'Synthetic', 1, SYSUTCDATETIME());",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = productId },
                2601,
                2627);
            await AssertSqlFailureAsync(
                databaseConnection.ConnectionString,
                "UPDATE [catalog].[ProductLocalSettings] SET [CurrentSalePrice] = -0.01 WHERE [ProductId] = @productId;",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = productId },
                547);
            await AssertSqlFailureAsync(
                databaseConnection.ConnectionString,
                "INSERT INTO [catalog].[ProductBarcodes] " +
                "([ProductId], [Value], [NormalizedValue], [IsPrimary], [BarcodeType], [IsActive], [CreatedAtUtc]) " +
                "VALUES (9223372036854775807, N'S09-INVALID-FK', N'S09-INVALID-FK', 0, N'Synthetic', 1, SYSUTCDATETIME());",
                null,
                547);
            await AssertSqlFailureAsync(
                databaseConnection.ConnectionString,
                "DELETE FROM [catalog].[Products] WHERE [Id] = @productId;",
                new SqlParameter("@productId", SqlDbType.BigInt) { Value = productId },
                547);

            await ValidateOptimisticConcurrencyAsync(
                databaseConnection.ConnectionString,
                productId,
                originalVersion);
            Assert.Equal(
                12.3456m,
                await ScalarAsync<decimal>(
                    databaseConnection.ConnectionString,
                    "SELECT [CurrentSalePrice] FROM [catalog].[ProductLocalSettings] WHERE [ProductId] = @productId;",
                    new SqlParameter("@productId", SqlDbType.BigInt) { Value = productId }));

            var idempotentScript = File.ReadAllText(Path.Combine(
                RepositoryRoot,
                "database",
                "scripts",
                "001_initial_catalog_foundation_idempotent.sql"));
            await ExecuteSqlScriptAsync(databaseConnection.ConnectionString, idempotentScript);
            await ExecuteSqlScriptAsync(databaseConnection.ConnectionString, idempotentScript);
            Assert.Equal(1, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM [dbo].[__EFMigrationsHistory] " +
                "WHERE [MigrationId] LIKE N'%_InitialCatalogPersistenceV01';"));

            await using (var rollbackContext = CreateContext(databaseConnection.ConnectionString))
            {
                var migrator = rollbackContext.GetService<IMigrator>();
                await migrator.MigrateAsync("0", cancellationToken);
            }

            Assert.Empty(await CatalogTablesAsync(databaseConnection.ConnectionString));
            Assert.Equal(0, await ScalarAsync<int>(
                databaseConnection.ConnectionString,
                "SELECT COUNT(*) FROM [dbo].[__EFMigrationsHistory];"));

            await using (var reapplyContext = CreateContext(databaseConnection.ConnectionString))
            {
                await reapplyContext.Database.MigrateAsync(cancellationToken);
                Assert.Empty(await reapplyContext.Database.GetPendingMigrationsAsync(cancellationToken));
            }

            Assert.Equal(expectedTables, await CatalogTablesAsync(databaseConnection.ConnectionString));
            Assert.Equal(schemaFingerprintBefore, await SchemaFingerprintAsync(databaseConnection.ConnectionString));
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

        var builder = new SqlConnectionStringBuilder(raw)
        {
            InitialCatalog = "master",
            ApplicationName = "Pharmacy.Sprint09.Tests",
            Pooling = false
        };
        return builder;
    }

    private static PharmacyDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PharmacyDbContext>();
        options.UsePharmacySqlServer(connectionString);
        return new PharmacyDbContext(options.Options);
    }

    private static async Task ValidateServerIdentityAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT CONVERT(int, SERVERPROPERTY('ProductMajorVersion')), " +
            "CONVERT(nvarchar(128), SERVERPROPERTY('Edition')), " +
            "CONVERT(int, SERVERPROPERTY('EngineEdition'));";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(17, reader.GetInt32(0));
        Assert.Contains(
            new[] { "Developer", "Express" },
            expected => reader.GetString(1).Contains(expected, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(reader.GetInt32(2), new[] { 2, 4 });
    }

    private static async Task<(long CategoryId, long ProductId, byte[] RowVersion)>
        InsertSyntheticCatalogGraphAsync(string connectionString)
    {
        var now = DateTime.UtcNow;
        var category = new ProductCategoryEntity
        {
            Code = "SPRINT09-SYNTHETIC",
            Name = "Catégorie synthétique Sprint 09",
            IsActive = true,
            SortOrder = 10
        };
        var product = new ProductEntity
        {
            Name = "Produit synthétique Sprint 09",
            PackageDescription = "Conditionnement fictif",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var settings = new ProductLocalSettingsEntity
        {
            Product = product,
            Category = category,
            InternalCode = "S09-INTERNAL-0001",
            IsActive = true,
            IsSellable = true,
            IsPurchasable = true,
            QuantityScale = 0,
            MinimumStock = 0m,
            MaximumStock = 50m,
            CurrentSalePrice = 10.5000m,
            CurrencyCode = "MAD"
        };
        product.LocalSettings = settings;
        product.Barcodes.Add(new ProductBarcodeEntity
        {
            Product = product,
            Value = "S09-000001",
            NormalizedValue = "S09-000001",
            IsPrimary = true,
            BarcodeType = "Synthetic",
            IsActive = true,
            CreatedAtUtc = now
        });
        product.PriceHistory.Add(new ProductPriceHistoryEntity
        {
            Product = product,
            OldPrice = null,
            NewPrice = 10.5000m,
            CurrencyCode = "MAD",
            ChangeOrigin = "InitialOnboarding",
            Reason = "Scénario synthétique Sprint 09",
            EffectiveAtUtc = now
        });

        await using var context = CreateContext(connectionString);
        context.ProductCategories.Add(category);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        return (category.Id, product.Id, settings.RowVersion.ToArray());
    }

    private static async Task ValidateOptimisticConcurrencyAsync(
        string connectionString,
        long productId,
        byte[] originalVersion)
    {
        await using var firstContext = CreateContext(connectionString);
        await using var secondContext = CreateContext(connectionString);
        var first = await firstContext.ProductLocalSettings.SingleAsync(settings =>
            settings.ProductId == productId);
        var second = await secondContext.ProductLocalSettings.SingleAsync(settings =>
            settings.ProductId == productId);

        Assert.Equal(originalVersion, first.RowVersion);
        Assert.Equal(originalVersion, second.RowVersion);

        first.CurrentSalePrice = 12.3456m;
        await firstContext.SaveChangesAsync();
        Assert.NotEqual(originalVersion, first.RowVersion);

        second.CurrentSalePrice = 19.9999m;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            secondContext.SaveChangesAsync());

        await using var verificationContext = CreateContext(connectionString);
        var persisted = await verificationContext.ProductLocalSettings
            .AsNoTracking()
            .SingleAsync(settings => settings.ProductId == productId);
        Assert.Equal(12.3456m, persisted.CurrentSalePrice);
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        ValidateTemporaryDatabaseName(databaseName);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(DBNull.Value, await DatabaseIdAsync(connection, databaseName));
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE {QuoteIdentifier(databaseName)};";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropTemporaryDatabaseAsync(string connectionString, string databaseName)
    {
        ValidateTemporaryDatabaseName(databaseName);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                $"IF DB_ID(@databaseName) IS NOT NULL BEGIN " +
                $"ALTER DATABASE {QuoteIdentifier(databaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE {QuoteIdentifier(databaseName)}; END;";
            command.Parameters.Add(new SqlParameter("@databaseName", SqlDbType.NVarChar, 128)
            {
                Value = databaseName
            });
            await command.ExecuteNonQueryAsync();
        }

        Assert.Equal(DBNull.Value, await DatabaseIdAsync(connection, databaseName));
    }

    private static async Task<object> DatabaseIdAsync(SqlConnection connection, string databaseName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_ID(@databaseName);";
        command.Parameters.Add(new SqlParameter("@databaseName", SqlDbType.NVarChar, 128)
        {
            Value = databaseName
        });
        return await command.ExecuteScalarAsync() ?? DBNull.Value;
    }

    private static async Task<string[]> CatalogTablesAsync(string connectionString)
    {
        const string sql =
            "SELECT t.name FROM sys.tables t " +
            "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
            "WHERE s.name = N'catalog' ORDER BY t.name;";
        var rows = await QueryRowsAsync(connectionString, sql, reader => reader.GetString(0));
        return rows.ToArray();
    }

    private static async Task<string> SchemaFingerprintAsync(string connectionString)
    {
        const string sql =
            "SELECT s.name, t.name, c.column_id, c.name, ty.name, c.max_length, " +
            "c.precision, c.scale, c.is_nullable " +
            "FROM sys.columns c " +
            "JOIN sys.tables t ON t.object_id = c.object_id " +
            "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
            "JOIN sys.types ty ON ty.user_type_id = c.user_type_id " +
            "WHERE s.name = N'catalog' " +
            "ORDER BY s.name, t.name, c.column_id;";
        var rows = await QueryRowsAsync(
            connectionString,
            sql,
            reader => string.Join(
                "|",
                Enumerable.Range(0, reader.FieldCount).Select(index => reader.GetValue(index).ToString())));
        return string.Join(Environment.NewLine, rows);
    }

    private static async Task<IReadOnlyList<T>> QueryRowsAsync<T>(
        string connectionString,
        string sql,
        Func<SqlDataReader, T> selector)
    {
        var rows = new List<T>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add(selector(reader));
        }

        return rows;
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
        var result = await command.ExecuteScalarAsync();

        if (result is null or DBNull)
        {
            throw new InvalidOperationException("La requête scalaire n’a retourné aucune valeur.");
        }

        return (T)Convert.ChangeType(
            result,
            typeof(T),
            System.Globalization.CultureInfo.InvariantCulture)!;
    }

    private static async Task AssertSqlFailureAsync(
        string connectionString,
        string sql,
        SqlParameter? parameter,
        params int[] expectedNumbers)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        if (parameter is not null)
        {
            command.Parameters.Add(parameter);
        }

        var exception = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());
        Assert.Contains(exception.Number, expectedNumbers);
    }

    private static async Task ExecuteSqlScriptAsync(string connectionString, string script)
    {
        var batches = Regex.Split(
            script,
            @"^\s*GO\s*$(?:\r?\n)?",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var batch in batches.Where(batch => !string.IsNullOrWhiteSpace(batch)))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static string QuoteIdentifier(string name)
    {
        return $"[{name.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    private static void ValidateTemporaryDatabaseName(string databaseName)
    {
        if (!Regex.IsMatch(
                databaseName,
                "^Pharmacy_Sprint09_Test_[0-9a-f]{12}$",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "Refus d’opérer sur une base qui ne respecte pas le préfixe Sprint 09.");
        }
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startPath);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
