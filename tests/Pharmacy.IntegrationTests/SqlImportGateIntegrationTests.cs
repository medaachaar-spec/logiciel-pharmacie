using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pharmacy.Application.Operations.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Infrastructure.Operations;
using Pharmacy.Infrastructure.Persistence;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class SqlImportGateIntegrationTests(SqlImportGateFixture fixture, ITestOutputHelper output) : IClassFixture<SqlImportGateFixture>
{
    private static readonly DateTime Now = new(2026, 8, 17, 11, 0, 0, DateTimeKind.Utc);
    private static readonly CsvImportMapping Mapping = new(
        "Reference", "Designation", "PPV", "PPH", "CodeBarres", "Categorie", DefaultCategoryCode: "GEN");

    [Fact]
    public async Task IMP003_PreviewDoesNotMutateSqlAndCancellationBeforeConfirmationRemainsNoWrite()
    {
        var prefix = UniquePrefix("PREVIEW");
        var content = Csv(prefix, 3);
        var before = await fixture.CatalogCountsAsync();

        var preview = await CreateService().PreviewAsync(Request("preview.csv", content), Token);
        Assert.True(preview.IsSuccess);
        Assert.True(preview.Value.CanConfirm);
        Assert.Equal(3, preview.Value.TotalRows);

        var rebuiltService = CreateService();
        Assert.NotNull(rebuiltService);
        Assert.Equal(before, await fixture.CatalogCountsAsync());
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM catalog.ProductLocalSettings WHERE InternalCode LIKE N'{prefix}%';"));
    }

    [Fact]
    public async Task IMP004_ConfirmationCreatesExactlyTheThreePreviewedProducts()
    {
        var prefix = UniquePrefix("CREATE");
        var operationId = Guid.NewGuid();
        var content = Utf8(
            "Reference,Designation,PPV,PPH,CodeBarres,Categorie\r\n" +
            $"{prefix}-00001,Paracétamol,10.00,7.00,,GEN\r\n" +
            $"{prefix}-00002,Crème pédiatrique,20.00,14.00,,GEN\r\n" +
            $"{prefix}-00003,دواء للأطفال,30.00,21.00,,GEN\r\n");
        var result = await CreateService().ConfirmAsync(Confirm(operationId, "create.csv", content), Token);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal((3, 0, 0, 0), (result.Value.CreatedCount, result.Value.UpdatedCount, result.Value.IgnoredCount, result.Value.RejectedCount));
        Assert.Equal(3, await fixture.CountProductsAsync(prefix));
        Assert.Equal(3, await fixture.CountPriceHistoryAsync(prefix));
        Assert.Equal(1, await fixture.CountJobsAsync(operationId));
        Assert.Equal(3, await fixture.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM catalog.Products p JOIN catalog.ProductLocalSettings s ON s.ProductId=p.Id WHERE s.InternalCode LIKE N'{prefix}%' AND p.Name IN (N'Paracétamol',N'Crème pédiatrique',N'دواء للأطفال');"));
    }

    [Fact]
    public async Task IMP011_OneInvalidRowMakesTheWholeFileAtomicWithZeroProducts()
    {
        var prefix = UniquePrefix("ATOMIC");
        var operationId = Guid.NewGuid();
        var content = Utf8(
            "Reference,Designation,PPV,PPH,CodeBarres,Categorie\r\n" +
            $"{prefix}-001,Valide 1,10,7,,GEN\r\n" +
            $"{prefix}-002,Invalide,ABC,7,,GEN\r\n" +
            $"{prefix}-003,Valide 3,30,21,,GEN\r\n");

        var result = await CreateService().ConfirmAsync(Confirm(operationId, "atomic.csv", content), Token);
        Assert.Equal("import.validation.failed", result.Error.Code);
        Assert.Equal(0, await fixture.CountProductsAsync(prefix));
        Assert.Equal(0, await fixture.CountJobsAsync(operationId));
    }

    [Fact]
    public async Task IMP017_ReimportAndOperationReplayAreIdempotentAndPayloadCollisionIsRefused()
    {
        var prefix = UniquePrefix("IDEMP");
        var content = Csv(prefix, 3);
        var firstOperation = Guid.NewGuid();
        var secondOperation = Guid.NewGuid();
        var service = CreateService();

        var first = await service.ConfirmAsync(Confirm(firstOperation, "idempotent.csv", content), Token);
        var replay = await service.ConfirmAsync(Confirm(firstOperation, "idempotent.csv", content), Token);
        var second = await service.ConfirmAsync(Confirm(secondOperation, "idempotent.csv", content), Token);
        var collision = await service.ConfirmAsync(Confirm(firstOperation, "idempotent.csv", Csv(prefix + "-CHANGED", 1)), Token);

        Assert.Equal(3, first.Value.CreatedCount);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Value.WasReplay);
        Assert.Equal((0, 0, 3), (second.Value.CreatedCount, second.Value.UpdatedCount, second.Value.IgnoredCount));
        Assert.Equal("import.idempotence.conflict", collision.Error.Code);
        Assert.Equal(3, await fixture.CountProductsAsync(prefix));
        Assert.Equal(3, await fixture.CountPriceHistoryAsync(prefix));
    }

    [Fact]
    public async Task IMP018_ChangedValueForExistingReferenceIsIgnoredByCreateOnlyPolicy()
    {
        var prefix = UniquePrefix("CHANGE");
        var initial = Csv(prefix, 1, 10m);
        var changed = Csv(prefix, 1, 11m);

        Assert.True((await CreateService().ConfirmAsync(Confirm(Guid.NewGuid(), "initial.csv", initial), Token)).IsSuccess);
        var second = await CreateService().ConfirmAsync(Confirm(Guid.NewGuid(), "changed.csv", changed), Token);

        Assert.True(second.IsSuccess);
        Assert.Equal((0, 0, 1), (second.Value.CreatedCount, second.Value.UpdatedCount, second.Value.IgnoredCount));
        Assert.Equal(10m, await fixture.ScalarAsync<decimal>(
            $"SELECT CurrentSalePrice FROM catalog.ProductLocalSettings WHERE InternalCode=N'{prefix}-00001';"));
        Assert.Equal(1, await fixture.CountProductsAsync(prefix));
        Assert.Equal(1, await fixture.CountPriceHistoryAsync(prefix));
    }

    [Fact]
    public async Task IMP019_ConcurrentImportsNeverCreateDuplicateOrPartialCatalogueState()
    {
        var prefix = UniquePrefix("RACE");
        var content = Csv(prefix, 1);
        var firstTask = CreateService().ConfirmAsync(Confirm(Guid.NewGuid(), "race.csv", content), Token);
        var secondTask = CreateService().ConfirmAsync(Confirm(Guid.NewGuid(), "race.csv", content), Token);
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(1, await fixture.CountProductsAsync(prefix));
        Assert.Equal(1, await fixture.CountPriceHistoryAsync(prefix));
        Assert.Single(results, result => result.IsSuccess && result.Value.CreatedCount == 1);
        Assert.All(results.Where(result => result.IsFailure), result => Assert.Equal("import.persistence.conflict", result.Error.Code));
        Assert.All(results.Where(result => result.IsSuccess && result.Value.CreatedCount == 0), result => Assert.Equal(1, result.Value.IgnoredCount));
    }

    [Fact]
    public async Task IMP027_SqlInjectionTextIsPersistedAsDataAndSchemaRemainsIntact()
    {
        var prefix = UniquePrefix("SQLI");
        const string hostileText = "'; DROP TABLE catalog.Products;--";
        var content = Utf8(
            "Reference,Designation,PPV,PPH,CodeBarres,Categorie\r\n" +
            $"{prefix}-001,{hostileText},10,7,,GEN\r\n");
        var result = await CreateService().ConfirmAsync(Confirm(Guid.NewGuid(), "sql-injection.csv", content), Token);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(hostileText, await fixture.ScalarAsync<string>(
            $"SELECT p.Name FROM catalog.Products p JOIN catalog.ProductLocalSettings s ON s.ProductId=p.Id WHERE s.InternalCode=N'{prefix}-001';"));
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'catalog' AND t.name=N'Products';"));
    }

    [Fact]
    public async Task IMP028_EightThousandRowsPreviewAndImportHaveExactCountsWithoutDuplicates()
    {
        var prefix = UniquePrefix("BULK");
        var content = Csv(prefix, 8_000);
        var service = CreateService();
        var previewTimer = Stopwatch.StartNew();
        var preview = await service.PreviewAsync(Request("bulk-8000.csv", content), Token);
        previewTimer.Stop();
        var importTimer = Stopwatch.StartNew();
        var imported = await service.ConfirmAsync(Confirm(Guid.NewGuid(), "bulk-8000.csv", content), Token);
        importTimer.Stop();

        Assert.True(preview.IsSuccess);
        Assert.Equal(8_000, preview.Value.TotalRows);
        Assert.Equal(100, preview.Value.Rows.Count);
        Assert.True(imported.IsSuccess, imported.IsFailure ? imported.Error.Message : null);
        Assert.Equal(8_000, imported.Value.CreatedCount);
        Assert.Equal(8_000, await fixture.CountProductsAsync(prefix));
        output.WriteLine($"IMP028_METRICS rows=8000 preview_ms={previewTimer.ElapsedMilliseconds} import_ms={importTimer.ElapsedMilliseconds}");
    }

    [Fact]
    public async Task IMP029_InjectedPersistenceFailureRollsBackProductsAndImportJob()
    {
        var prefix = UniquePrefix("ROLLBACK");
        var operationId = Guid.NewGuid();
        var service = CreateService(fixture.CreateFactory(new FailImportJobCommandInterceptor()));
        var result = await service.ConfirmAsync(Confirm(operationId, "rollback.csv", Csv(prefix, 3)), Token);

        Assert.Equal("import.persistence.conflict", result.Error.Code);
        Assert.Equal(0, await fixture.CountProductsAsync(prefix));
        Assert.Equal(0, await fixture.CountPriceHistoryAsync(prefix));
        Assert.Equal(0, await fixture.CountJobsAsync(operationId));
    }

    [Fact]
    public async Task IMP030_BackendRbacRefusesBeforeParsingAndAllowsTheRealPermission()
    {
        var deniedAudit = new CapturingAuditRecorder();
        var deniedService = CreateService(permissions: [], audit: deniedAudit);
        var deniedPreview = await deniedService.PreviewAsync(
            new PreviewImportRequest("not-even-csv.txt", [], ControlledImportType.Catalogue, Mapping), Token);
        var deniedConfirm = await deniedService.ConfirmAsync(
            new ConfirmImportRequest(Guid.NewGuid(), "not-even-csv.txt", [], ControlledImportType.Catalogue, Mapping), Token);

        Assert.Equal("security.authorization.denied", deniedPreview.Error.Code);
        Assert.Equal("security.authorization.denied", deniedConfirm.Error.Code);
        Assert.Single(deniedAudit.Failures);
        Assert.Equal("security.authorization.denied", deniedAudit.Failures[0].ReasonCode);

        var allowed = await CreateService().PreviewAsync(Request("allowed.csv", Csv(UniquePrefix("RBAC"), 1)), Token);
        Assert.True(allowed.IsSuccess);
    }

    [Fact]
    public async Task IMP031_ConfirmationAndFailureAreAuditedWithoutFilePayloadOrSecrets()
    {
        var prefix = UniquePrefix("AUDIT");
        var operationId = Guid.NewGuid();
        var audit = new CapturingAuditRecorder();
        var service = CreateService(audit: audit);
        var success = await service.ConfirmAsync(Confirm(operationId, "audit.csv", Csv(prefix, 2)), Token);
        var failedOperation = Guid.NewGuid();
        var failure = await service.ConfirmAsync(Confirm(failedOperation, "failure.csv", Utf8(
            "Reference,Designation,PPV,PPH,CodeBarres,Categorie\r\nBAD,Produit,ABC,7,,GEN\r\n")), Token);

        Assert.True(success.IsSuccess);
        var successAudit = Assert.Single(audit.Successes);
        Assert.Equal(("import", "ImportJob", "import.confirm", operationId),
            (successAudit.Module, successAudit.EntityType, successAudit.Action, successAudit.OperationId));
        Assert.DoesNotContain("audit.csv", successAudit.ToString(), StringComparison.Ordinal);
        Assert.Equal("import.validation.failed", failure.Error.Code);
        var failureAudit = Assert.Single(audit.Failures);
        Assert.Equal(("import", "ImportJob", "import.confirm", failedOperation, "import.validation.failed"),
            (failureAudit.Module, failureAudit.EntityType, failureAudit.Action, failureAudit.OperationId, failureAudit.ReasonCode));
        Assert.Equal(2, await fixture.ScalarAsync<int>(
            $"SELECT ReadCount FROM operations.ImportJobs WHERE OperationId='{operationId}';"));
    }

    [Fact]
    public async Task IMP032_NewServiceInstanceReadsCommittedResultThroughOperationReplay()
    {
        var prefix = UniquePrefix("RELOAD");
        var operationId = Guid.NewGuid();
        var content = Csv(prefix, 2);
        var first = await CreateService().ConfirmAsync(Confirm(operationId, "reload.csv", content), Token);
        var afterRestart = await CreateService().ConfirmAsync(Confirm(operationId, "reload.csv", content), Token);

        Assert.True(first.IsSuccess);
        Assert.True(afterRestart.IsSuccess);
        Assert.True(afterRestart.Value.WasReplay);
        Assert.Equal(first.Value.ImportJobId, afterRestart.Value.ImportJobId);
        Assert.Equal(2, await fixture.CountProductsAsync(prefix));
        Assert.Equal(1, await fixture.CountJobsAsync(operationId));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SqlControlledImportService CreateService(
        IDbContextFactory<PharmacyDbContext>? factory = null,
        IReadOnlyCollection<string>? permissions = null,
        CapturingAuditRecorder? audit = null)
    {
        var clock = new FixedClock();
        var session = new CurrentSecuritySession();
        session.Establish(new UserSessionView(
            1801, "owner.imp.sql", "Owner IMP SQL", ["OWNER"],
            new HashSet<string>(permissions ?? [SecurityPermissions.ImportView, SecurityPermissions.ImportExecute], StringComparer.Ordinal),
            Now, Now), Now);
        return new SqlControlledImportService(factory ?? fixture.Factory, new PermissionAuthorizer(session, clock), audit ?? new CapturingAuditRecorder(), clock);
    }

    private static PreviewImportRequest Request(string name, byte[] content)
        => new(name, content, ControlledImportType.Catalogue, Mapping);

    private static ConfirmImportRequest Confirm(Guid operationId, string name, byte[] content)
        => new(operationId, name, content, ControlledImportType.Catalogue, Mapping);

    private static byte[] Csv(string prefix, int count, decimal salePrice = 10m)
    {
        var csv = new StringBuilder("Reference,Designation,PPV,PPH,CodeBarres,Categorie\r\n");
        var invariantSalePrice = salePrice.ToString("0.00", CultureInfo.InvariantCulture);
        for (var index = 1; index <= count; index++)
            csv.Append($"{prefix}-{index:00000},Produit {index},{invariantSalePrice},7.00,,GEN\r\n");
        return Utf8(csv.ToString());
    }

    private static byte[] Utf8(string value) => new UTF8Encoding(false, true).GetBytes(value);
    private static string UniquePrefix(string purpose) => $"IMP-{purpose}-{Guid.NewGuid():N}";

    private sealed class FixedClock : ISecurityClock { public DateTime UtcNow => Now; }

    private sealed record AuditSuccess(string Module, string EntityType, string? EntityId, string Action, Guid? OperationId);
    private sealed record AuditFailure(string Module, string EntityType, string? EntityId, string Action, Guid? OperationId, string ReasonCode);

    private sealed class CapturingAuditRecorder : IBusinessAuditRecorder
    {
        public List<AuditSuccess> Successes { get; } = [];
        public List<AuditFailure> Failures { get; } = [];
        public Task RecordSuccessAsync(string module, string entityType, string? entityId, string action, Guid? operationId, CancellationToken cancellationToken = default)
        {
            Successes.Add(new(module, entityType, entityId, action, operationId));
            return Task.CompletedTask;
        }
        public Task RecordFailureAsync(string module, string entityType, string? entityId, string action, Guid? operationId, string reasonCode, CancellationToken cancellationToken = default)
        {
            Failures.Add(new(module, entityType, entityId, action, operationId, reasonCode));
            return Task.CompletedTask;
        }
    }

    private sealed class FailImportJobCommandInterceptor : DbCommandInterceptor
    {
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
            => ThrowIfImportJob(command, result);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(ThrowIfImportJob(command, result));

        private static InterceptionResult<DbDataReader> ThrowIfImportJob(DbCommand command, InterceptionResult<DbDataReader> result)
        {
            if (command.CommandText.Contains("[operations].[ImportJobs]", StringComparison.Ordinal))
                throw new DbUpdateException("Synthetic IMP029 persistence failure before commit.");
            return result;
        }
    }
}

public sealed class SqlImportGateFixture : IAsyncLifetime
{
    private const string Prefix = "Pharmacy_IMP_Test_";
    private string serverConnection = string.Empty;
    private string databaseConnection = string.Empty;
    private string databaseName = string.Empty;

    public IDbContextFactory<PharmacyDbContext> Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException("PHARMACY_TEST_SQLSERVER_CONNECTION est requise.");
        var server = new SqlConnectionStringBuilder(raw) { InitialCatalog = "master", Pooling = false, ApplicationName = "Pharmacy.IMP.Gate.Tests" };
        serverConnection = server.ConnectionString;
        databaseName = Prefix + Guid.NewGuid().ToString("N")[..12];
        ValidateDatabaseName(databaseName);
        await ExecuteServerAsync($"CREATE DATABASE [{databaseName}];");
        databaseConnection = new SqlConnectionStringBuilder(serverConnection) { InitialCatalog = databaseName }.ConnectionString;
        Factory = CreateFactory();
        await using var context = await Factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        await ExecuteAsync(
            "INSERT INTO catalog.ProductCategories(Code,Name,IsActive,DefaultTracksLots,DefaultTracksExpiry,DefaultDiscountAllowed,SortOrder) VALUES(N'GEN',N'Générique IMP',1,0,0,0,0);");
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

    public async Task<(int Products, int Barcodes, int PriceHistory, int Jobs)> CatalogCountsAsync()
        => (
            await ScalarAsync<int>("SELECT COUNT(*) FROM catalog.Products;"),
            await ScalarAsync<int>("SELECT COUNT(*) FROM catalog.ProductBarcodes;"),
            await ScalarAsync<int>("SELECT COUNT(*) FROM catalog.ProductPriceHistories;"),
            await ScalarAsync<int>("SELECT COUNT(*) FROM operations.ImportJobs;"));

    public Task<int> CountProductsAsync(string prefix)
        => ScalarAsync<int>($"SELECT COUNT(*) FROM catalog.ProductLocalSettings WHERE InternalCode LIKE N'{prefix}%';");

    public Task<int> CountPriceHistoryAsync(string prefix)
        => ScalarAsync<int>($"SELECT COUNT(*) FROM catalog.ProductPriceHistories h JOIN catalog.ProductLocalSettings s ON s.ProductId=h.ProductId WHERE s.InternalCode LIKE N'{prefix}%';");

    public Task<int> CountJobsAsync(Guid operationId)
        => ScalarAsync<int>($"SELECT COUNT(*) FROM operations.ImportJobs WHERE OperationId='{operationId}';");

    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(databaseConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        Assert.NotNull(value);
        return (T)Convert.ChangeType(value, typeof(T));
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(databaseConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
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
        if (!Regex.IsMatch(value, "^Pharmacy_IMP_Test_[0-9a-f]{12}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Nom de base temporaire IMP refusé.");
    }

    private sealed class FactoryImpl(DbContextOptions<PharmacyDbContext> options) : IDbContextFactory<PharmacyDbContext>
    {
        public PharmacyDbContext CreateDbContext() => new(options);
        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
