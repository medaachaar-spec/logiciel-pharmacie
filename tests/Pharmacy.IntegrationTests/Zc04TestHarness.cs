using System.Data;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Purchasing.Contracts;
using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Commands;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Finalization.Models;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Sales;
using Pharmacy.Domain.Security;
using Pharmacy.Infrastructure.Catalog;
using Pharmacy.Infrastructure.Inventory;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Security;
using Pharmacy.Infrastructure.Sales;
using Pharmacy.Infrastructure.Sales.InMemory;
using Pharmacy.Infrastructure.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

/// <summary>
/// Shared disposable-SQL harness for ZCODE-04A adversarial tests.
/// Mirrors the repository's existing isolated disposable SQL test infrastructure:
/// every database is created fresh from <c>master</c> with the guarded
/// <c>Pharmacy_ZC04_Test_</c> prefix and dropped in a finally block.
/// The pilot instance (.\PHARMACY) and Pharmacy_Development are never referenced.
/// </summary>
internal static class Zc04
{
    public const string DatabasePrefix = "Pharmacy_ZC04_Test_";
    private static readonly Regex SafeName =
        new("^Pharmacy_ZC04_Test_[0-9a-f]{12}$", RegexOptions.CultureInvariant);

    public static SqlConnectionStringBuilder BuildServerConnection()
    {
        var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "SQL TEST ENVIRONMENT REQUIRED: PHARMACY_TEST_SQLSERVER_CONNECTION est absente.");
        }

        return new SqlConnectionStringBuilder(raw)
        {
            InitialCatalog = "master",
            ApplicationName = "Pharmacy.ZC04.Tests",
            Pooling = false
        };
    }

    public static string NewDatabaseName() =>
        DatabasePrefix + Guid.NewGuid().ToString("N")[..12];

    public static string BuildDatabaseConnection(string serverConnection, string databaseName)
    {
        ValidateDisposableName(databaseName);
        return new SqlConnectionStringBuilder(serverConnection)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Pharmacy.ZC04.Tests",
            Pooling = false
        }.ConnectionString;
    }

    public static void ValidateDisposableName(string databaseName)
    {
        if (!SafeName.IsMatch(databaseName))
        {
            throw new InvalidOperationException(
                "Refus d’opérer sur une base qui ne respecte pas le préfixe jetable ZC04.");
        }
    }

    public static async Task CreateDatabaseAsync(string serverConnection, string databaseName)
    {
        ValidateDisposableName(databaseName);
        await using var connection = new SqlConnection(serverConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{Quote(databaseName)}];";
        await command.ExecuteNonQueryAsync();
    }

    public static async Task DropDatabaseAsync(string serverConnection, string databaseName)
    {
        ValidateDisposableName(databaseName);
        await using var connection = new SqlConnection(serverConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"IF DB_ID(@name) IS NOT NULL BEGIN " +
            $"ALTER DATABASE [{Quote(databaseName)}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{Quote(databaseName)}]; END;";
        command.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = databaseName });
        await command.ExecuteNonQueryAsync();
    }

    public static async Task MigrateAsync(string databaseConnection)
    {
        var factory = new Factory(databaseConnection);
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    public static async Task<T> ScalarAsync<T>(string connectionString, string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("La requête scalaire ZC04 n’a retourné aucune valeur.");
        return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    public static async Task<T?> ScalarOrNullAsync<T>(string connectionString, string sql, params SqlParameter[] parameters)
        where T : struct
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync();
        return value is null || value is DBNull ? null : (T?)Convert.ChangeType(
            value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    public static async Task ExecuteAsync(string connectionString, string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static string Quote(string value) => value.Replace("]", "]]", StringComparison.Ordinal);

    public static PasswordHasher<UserAccountEntity> CreateHasher() => new(Options.Create(new PasswordHasherOptions
    {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
        IterationCount = 210_000
    }));

    /// <summary>Bootstraps the OWNER account and logs it into its own isolated session.</summary>
    public static async Task<OwnerContext> BootstrapOwnerAsync(
        Factory factory,
        Zc04Clock clock,
        string login,
        string password)
    {
        var session = new CurrentSecuritySession();
        var generator = new SequentialRecoveryGenerator();
        var hasher = CreateHasher();
        var authentication = new SqlAuthenticationService(factory, hasher, generator, session, clock);
        var bootstrap = await authentication.BootstrapOwnerAsync(
            new BootstrapOwnerRequest(Guid.NewGuid(), login, "Propriétaire ZC04", password),
            TestContext.Current.CancellationToken);
        Assert.True(bootstrap.IsSuccess, bootstrap.IsFailure ? bootstrap.Error.Code : null);
        var loginResult = await authentication.LoginAsync(
            new LoginRequest(login, password, Guid.NewGuid()),
            TestContext.Current.CancellationToken);
        Assert.True(loginResult.IsSuccess, loginResult.IsFailure ? loginResult.Error.Code : null);
        var authorizer = new PermissionAuthorizer(
            session,
            clock,
            new SqlSecuritySessionValidator(factory));
        var administration = new SqlSecurityAdministrationService(factory, hasher, generator, authorizer, session, clock);
        return new OwnerContext(session, authentication, authorizer, administration);
    }

    public sealed record OwnerContext(
        CurrentSecuritySession Session,
        SqlAuthenticationService Authentication,
        PermissionAuthorizer Authorizer,
        SqlSecurityAdministrationService Administration);

    public sealed class Factory(string connectionString) : IDbContextFactory<PharmacyDbContext>
    {
        private readonly DbContextOptions<PharmacyDbContext> options = CreateOptions(connectionString);
        public PharmacyDbContext CreateDbContext() => new(options);
        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
        private static DbContextOptions<PharmacyDbContext> CreateOptions(string connectionString)
        {
            var builder = new DbContextOptionsBuilder<PharmacyDbContext>();
            builder.UsePharmacySqlServer(connectionString);
            return builder.Options;
        }
    }

    /// <summary>Single mutable clock implementing every clock contract used by the app.</summary>
    public sealed class Zc04Clock(DateTime utcNow) :
        ISecurityClock, ISalesClock, IInventoryDateProvider, IPurchasingClock
    {
        private DateTime current = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        public DateTime UtcNow => current;
        public DateOnly CurrentDate => DateOnly.FromDateTime(current);
        public void Advance(TimeSpan duration) => current = current.Add(duration);
        public void Set(DateTime utc) => current = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
    }

    public sealed class SequentialRecoveryGenerator : IRecoveryCodeGenerator
    {
        private int generation;
        public IReadOnlyList<string> Generate(int count)
        {
            var current = Interlocked.Increment(ref generation);
            return Enumerable.Range(1, count).Select(index => $"ZC4{current:00}-CODE-{index:0000}").ToArray();
        }
    }

    public sealed class NoOpAuditRecorder : IBusinessAuditRecorder
    {
        public Task RecordSuccessAsync(string module, string entityType, string? entityId, string action, Guid? operationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordFailureAsync(string module, string entityType, string? entityId, string action, Guid? operationId, string reasonCode, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public sealed class AsyncBarrier(int participantCount)
    {
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public Task SignalAndWaitAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref arrivals) == participantCount)
            {
                completion.TrySetResult();
            }

            return completion.Task.WaitAsync(cancellationToken);
        }
    }
}

/// <summary>Allow-all authorizer shared by ZC04 seeding helpers (seeding acts as an administrator).</summary>
internal sealed class IntegrationAllowAllAuthorizer : IPermissionAuthorizer
{
    public static readonly IntegrationAllowAllAuthorizer Instance = new();
    public Result Authorize(string permission, bool requireRecentReauthentication = false) => Result.Success();
}

/// <summary>
/// Creates a real, fully persisted sale through the real Application handler chain
/// (draft -> preparation -> finalization) under the authorization of the supplied
/// session, plus the catalog/inventory seeding it requires.
/// </summary>
internal static class SeededSaleFactory
{
    public sealed record SeededProduct(long ProductId, decimal UnitPrice, long LocationId);

    public static async Task<SeededProduct> SeedProductWithStockAsync(
        Zc04.Factory factory,
        Zc04.Zc04Clock clock,
        string tag,
        int quantity = 10,
        decimal price = 10m,
        CancellationToken token = default)
    {
        var catalogStore = new SqlCatalogStore(factory);
        var category = await catalogStore.CreateAsync(
            new CreateCatalogCategoryCommand(
                $"CAT-{tag}",
                $"Catégorie {tag}",
                DefaultTracksLots: true,
                DefaultTracksExpiry: true), token);
        Assert.True(category.IsSuccess, category.IsFailure ? category.Error.Code : null);
        var product = await new CreateCatalogProductHandler(catalogStore, IntegrationAllowAllAuthorizer.Instance)
            .HandleAsync(new CreateCatalogProductCommand(
                $"Produit {tag}", "Boîte ZC04", category.Value.CategoryId, $"REF-{tag}", price), token);
        Assert.True(product.IsSuccess, product.IsFailure ? product.Error.Code : null);

        var inventoryStore = new SqlInventoryStore(factory);
        var inventory = new InventoryCommandHandlers(
            inventoryStore, clock, IntegrationAllowAllAuthorizer.Instance, new Zc04.NoOpAuditRecorder());
        var location = await inventory.CreateLocationAsync(
            new CreateStockLocationCommand(Guid.NewGuid(), $"LOC-{tag}", $"Emplacement {tag}", true), token);
        Assert.True(location.IsSuccess, location.IsFailure ? location.Error.Code : null);
        var opening = await inventory.RecordOpeningAsync(
            new RecordStockMovementCommand(
                Guid.NewGuid(), StockMovementType.Opening, product.Value.ProductId,
                location.Value.LocationId, null, quantity,
                $"Ouverture {tag}", "ZCODE-04A", tag),
            token);
        Assert.True(opening.IsSuccess, opening.IsFailure ? opening.Error.Code : null);
        return new SeededProduct(product.Value.ProductId, price, location.Value.LocationId);
    }

    /// <summary>Finalizes a one-line cash sale of <paramref name="quantity"/> units under the supplied session.</summary>
    public static async Task<Result<FinalizedSaleView>> FinalizeSaleAsSessionAsync(
        Zc04.Factory factory,
        Zc04.Zc04Clock clock,
        CurrentSecuritySession session,
        SeededProduct seeded,
        int quantity,
        string tag,
        CancellationToken token = default,
        IBusinessAuditRecorder? auditRecorder = null)
    {
        var catalogReader = new SqlProductCatalogReader(factory);
        var availabilityReader = new SqlInventoryStockAvailabilityReader(factory, clock);
        var draftStore = new InMemorySaleDraftStore();
        var draft = await new CreateSaleDraftHandler(new InMemorySaleDraftIdGenerator(), draftStore)
            .HandleAsync(token);
        Assert.True(draft.IsSuccess, draft.IsFailure ? draft.Error.Code : null);
        var added = await new AddProductToSaleDraftHandler(draftStore, catalogReader, availabilityReader)
            .HandleAsync(new AddProductToSaleDraftCommand(
                draft.Value.Id,
                SellableProductLookup.ByProductId(ProductId.Create(seeded.ProductId).Value),
                SaleQuantity.Create(quantity).Value,
                draft.Value.Version), token);
        Assert.True(added.IsSuccess, added.IsFailure ? added.Error.Code : null);

        var preparationHandler = new PrepareSaleFinalizationHandler(
            draftStore, availabilityReader, new InMemorySaleFinalizationPreparationStore());
        var preparation = await preparationHandler.HandleAsync(new PrepareSaleFinalizationCommand(
            Guid.NewGuid(), draft.Value.Id, added.Value.Version), token);
        Assert.True(preparation.IsSuccess, preparation.IsFailure ? preparation.Error.Code : null);

        var authorizer = new PermissionAuthorizer(
            session,
            clock,
            new SqlSecuritySessionValidator(factory));
        var handler = new FinalizeSaleHandler(
            preparationHandler,
            new SqlSaleFinalizer(factory, clock),
            authorizer,
            auditRecorder ?? new Zc04.NoOpAuditRecorder(),
            session);
        var total = seeded.UnitPrice * quantity;
        return await handler.HandleAsync(new FinalizeSaleCommand(
            Guid.NewGuid(), draft.Value.Id, added.Value.Version,
            SalePaymentMethod.Cash, total, false), token);
    }
}
