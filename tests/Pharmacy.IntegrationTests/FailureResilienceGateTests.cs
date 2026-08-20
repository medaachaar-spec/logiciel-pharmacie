using System.ComponentModel;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Catalog.Models;
using Pharmacy.Application.Customers.Models;
using Pharmacy.Application.Operations.Contracts;
using Pharmacy.Application.Operations.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Desktop.Components.Operations;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Catalog;
using Pharmacy.Infrastructure.Inventory;
using Pharmacy.Infrastructure.Operations;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Security;
using Pharmacy.Infrastructure.Purchasing;
using Pharmacy.Infrastructure.Sales;
using Pharmacy.Infrastructure.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class FailureResilienceGateTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly CsvImportMapping ImportMapping = new(
        "Reference", "Designation", "PPV", DefaultCategoryCode: "GEN");

    [Fact]
    public async Task FAIL001_AuthenticationSqlOutageIsControlled()
    {
        var service = new SqlAuthenticationService(
            TimeoutFactory(), CreateHasher(), new FixedRecoveryCodeGenerator(),
            new CurrentSecuritySession(), new FixedClock());

        var result = await service.HasUsersAsync(TestContext.Current.CancellationToken);

        AssertControlledFailure(result, "security.database.unavailable");
    }

    [Fact]
    public async Task FAIL002_CatalogReadAndWriteSqlOutageIsControlled()
    {
        var factory = TimeoutFactory();
        var read = await new SqlProductCatalogReader(factory).SearchAsync(
            new CatalogProductSearchCriteria(), TestContext.Current.CancellationToken);
        var write = await new SqlCatalogStore(factory).CreateAsync(
            new CreateCatalogProductCommand("Produit FAIL002", null, 1, "FAIL-002", 10m),
            TestContext.Current.CancellationToken);

        AssertControlledFailure(read, "catalog.database.unavailable");
        AssertControlledFailure(write, "catalog.database.unavailable");
    }

    [Fact]
    public async Task FAIL003_ImportSqlOutageIsControlledAndNeverAuditedAsSuccess()
    {
        var audit = new RecordingAudit();
        var service = new SqlControlledImportService(
            TimeoutFactory(), Authorizer(SecurityPermissions.ImportExecute), audit, new FixedClock());

        var result = await service.ConfirmAsync(new ConfirmImportRequest(
            Guid.NewGuid(), "fail003.csv",
            Encoding.UTF8.GetBytes("Reference,Designation,PPV\r\nFAIL-003,Produit,10.00\r\n"),
            ControlledImportType.Catalogue, ImportMapping), TestContext.Current.CancellationToken);

        AssertControlledFailure(result, "operations.database.unavailable");
        Assert.Equal(0, audit.SuccessCount);
        Assert.Equal(1, audit.FailureCount);
    }

    [Fact]
    public async Task FAIL004_PurchasingSqlOutageIsControlled()
    {
        var result = await new SqlPurchasingReader(Win32Factory(258))
            .SearchSuppliersAsync(null, cancellationToken: TestContext.Current.CancellationToken);

        AssertControlledFailure(result, "purchasing.receipt.database_unavailable");
    }

    [Fact]
    public async Task FAIL005_InventorySqlOutageIsControlled()
    {
        var result = await new SqlInventoryStore(Win32Factory(10060)).CreateLocationAsync(
            new(Guid.NewGuid(), "FAIL005", "FAIL 005"), TestContext.Current.CancellationToken);

        AssertControlledFailure(result, "inventory.database.unavailable");
    }

    [Fact]
    public async Task FAIL006_FAIL008_SalesAndReturnsSqlOutageIsControlled()
    {
        var reader = new SqlSalesReader(TimeoutFactory());
        var sales = await reader.GetRecentAsync(5, TestContext.Current.CancellationToken);
        var returns = await reader.GetPreparationAsync(1, TestContext.Current.CancellationToken);

        AssertControlledFailure(sales, "sales.database.unavailable");
        AssertControlledFailure(returns, "sales.database.unavailable");
    }

    [Fact]
    public async Task FAIL007_FAIL009_SuspensionAndCreditSqlOutageHaveNoFallback()
    {
        var store = new SqlCustomerCreditCashStore(TimeoutFactory(), new FixedClock());
        var suspensions = await store.GetOpenAsync(TestContext.Current.CancellationToken);
        var credit = await store.SearchAsync(
            new CustomerSearchQuery(null), TestContext.Current.CancellationToken);

        AssertControlledFailure(suspensions, "sales.database.unavailable");
        AssertControlledFailure(credit, "customers.database.unavailable");
    }

    [Fact]
    public async Task FAIL010_BackupSqlOutageIsControlledAndNeverAuditedAsSuccess()
    {
        var audit = new RecordingAudit();
        var service = new SqlBackupRecoveryService(
            TimeoutFactory(), Authorizer(SecurityPermissions.BackupView), audit, new FixedClock());

        var result = await service.GetConfigurationAsync(TestContext.Current.CancellationToken);

        AssertControlledFailure(result, "operations.database.unavailable");
        Assert.Equal(0, audit.SuccessCount);
    }

    [Theory]
    [InlineData(258)]
    [InlineData(10060)]
    public void FAIL011_NestedEfAndNetworkFailuresAreClassifiedAcrossModules(int nativeErrorCode)
    {
        var networkException = new Exception(
            "outer",
            new DbUpdateException("ef", new Win32Exception(nativeErrorCode, "network")));
        var providerException = new InvalidOperationException(
            "provider",
            new DbUpdateException("ef", new TimeoutException("timeout")));

        Assert.True(CatalogSqlFailure.IsDatabaseUnavailable(networkException));
        Assert.True(InventorySqlFailure.IsDatabaseUnavailable(networkException));
        Assert.True(PurchasingSqlFailure.IsUnavailable(networkException));
        Assert.True(SalesSqlFailure.IsDatabaseUnavailable(networkException));
        Assert.True(CatalogSqlFailure.IsDatabaseUnavailable(providerException));
        Assert.True(InventorySqlFailure.IsDatabaseUnavailable(providerException));
        Assert.True(PurchasingSqlFailure.IsUnavailable(providerException));
        Assert.True(SalesSqlFailure.IsDatabaseUnavailable(providerException));
    }

    [Fact]
    public async Task FAIL012_FAIL013_FAIL016_DesktopRecoversAfterFailureAndSuppressesDoubleClick()
    {
        var imports = new BlockingThenSuccessfulImportService();
        var coordinator = new OperationsCoordinator(
            new NeverBackupRecoveryService(), imports, new NeverDeviceDiagnostics());
        var content = Encoding.UTF8.GetBytes("Reference,Designation,PPV\r\nFAIL-016,Produit,10.00\r\n");

        var first = coordinator.ConfirmImportAsync(
            "fail016.csv", content, ControlledImportType.Catalogue, ImportMapping,
            TestContext.Current.CancellationToken);
        await imports.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await coordinator.ConfirmImportAsync(
            "fail016.csv", content, ControlledImportType.Catalogue, ImportMapping,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, imports.CallCount);
        imports.Release.TrySetResult();
        await first;
        Assert.False(coordinator.IsBusy);
        Assert.Contains("operations.database.unavailable", coordinator.Error, StringComparison.Ordinal);

        await coordinator.ConfirmImportAsync(
            "fail016.csv", content, ControlledImportType.Catalogue, ImportMapping,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, imports.CallCount);
        Assert.False(coordinator.IsBusy);
        Assert.Null(coordinator.Error);
        Assert.NotNull(coordinator.ImportReport);

        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Pharmacy.Desktop", "Components", "Operations", "OperationsCoordinator.cs"));
        Assert.Contains("Interlocked.CompareExchange(ref operationInProgress, 1, 0) != 0", source, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(ref operationInProgress, 0)", source, StringComparison.Ordinal);
    }

    private static void AssertControlledFailure<T>(Result<T> result, string expectedCode)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.DoesNotContain("Server=", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PasswordHash", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recovery", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" at ", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ThrowingDbContextFactory TimeoutFactory() => new(
        new Exception(
            "Server=private.invalid;Password=must-not-leak",
            new DbUpdateException("EF persistence wrapper", new TimeoutException("network timeout"))));

    private static ThrowingDbContextFactory Win32Factory(int nativeErrorCode) => new(
        new Exception(
            "Server=private.invalid;Password=must-not-leak",
            new DbUpdateException("EF persistence wrapper", new Win32Exception(nativeErrorCode, "network failure"))));

    private static PermissionAuthorizer Authorizer(params string[] permissions)
    {
        var clock = new FixedClock();
        var session = new CurrentSecuritySession();
        session.Establish(new UserSessionView(
            2500, "owner.fail", "Owner FAIL", ["OWNER"],
            permissions.ToHashSet(StringComparer.Ordinal), Now, Now), Now);
        return new PermissionAuthorizer(session, clock);
    }

    private static IPasswordHasher<UserAccountEntity> CreateHasher() =>
        new PasswordHasher<UserAccountEntity>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = 10_000
        }));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class ThrowingDbContextFactory(Exception failure) : IDbContextFactory<PharmacyDbContext>
    {
        public PharmacyDbContext CreateDbContext() => throw failure;

        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<PharmacyDbContext>(failure);
    }

    private sealed class FixedClock : ISecurityClock, ISalesClock
    {
        public DateOnly CurrentDate => DateOnly.FromDateTime(Now);
        public DateTime UtcNow => Now;
    }

    private sealed class FixedRecoveryCodeGenerator : IRecoveryCodeGenerator
    {
        public IReadOnlyList<string> Generate(int count) =>
            Enumerable.Range(1, count).Select(index => $"FAIL-CODE-{index:00}").ToArray();
    }

    private sealed class RecordingAudit : IBusinessAuditRecorder
    {
        public int SuccessCount { get; private set; }
        public int FailureCount { get; private set; }

        public Task RecordSuccessAsync(
            string module, string entityType, string? entityId, string action, Guid? operationId,
            CancellationToken cancellationToken = default)
        {
            SuccessCount++;
            return Task.CompletedTask;
        }

        public Task RecordFailureAsync(
            string module, string entityType, string? entityId, string action, Guid? operationId,
            string reasonCode, CancellationToken cancellationToken = default)
        {
            FailureCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingThenSuccessfulImportService : IControlledImportService
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount { get; private set; }

        public Task<Result<ImportPreviewView>> PreviewAsync(
            PreviewImportRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<Result<ImportReportView>> ConfirmAsync(
            ConfirmImportRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (CallCount == 1)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
                return Result<ImportReportView>.Failure(
                    new Error("operations.database.unavailable", "SQL Server est indisponible pour cette opération."));
            }

            return Result<ImportReportView>.Success(new ImportReportView(
                1, request.OperationId, request.FileName, new string('A', 64), request.Type,
                1, 1, 0, 0, 0, false, Now, []));
        }
    }

    private sealed class NeverBackupRecoveryService : IBackupRecoveryService
    {
        private static Task<Result<T>> Unexpected<T>() => throw new NotSupportedException();
        public Task<Result<BackupConfigurationView>> GetConfigurationAsync(CancellationToken cancellationToken = default) => Unexpected<BackupConfigurationView>();
        public Task<Result<IReadOnlyList<BackupFileView>>> ListAsync(CancellationToken cancellationToken = default) => Unexpected<IReadOnlyList<BackupFileView>>();
        public Task<Result<BackupExecutionView>> CreateAsync(CreateBackupRequest request, CancellationToken cancellationToken = default) => Unexpected<BackupExecutionView>();
        public Task<Result<BackupFileView>> VerifyAsync(VerifyBackupRequest request, CancellationToken cancellationToken = default) => Unexpected<BackupFileView>();
        public Task<Result<RestoreTestView>> RestoreTestAsync(RestoreTestRequest request, CancellationToken cancellationToken = default) => Unexpected<RestoreTestView>();
    }

    private sealed class NeverDeviceDiagnostics : IDeviceDiagnostics
    {
        public Task<Result<DeviceDiagnosticView>> CheckAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
