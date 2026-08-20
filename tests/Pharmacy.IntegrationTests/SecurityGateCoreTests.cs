using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pharmacy.Application.Customers.Commands;
using Pharmacy.Application.Customers.Contracts;
using Pharmacy.Application.Customers.Models;
using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Inventory.Models;
using Pharmacy.Application.Operations.Models;
using Pharmacy.Application.Purchasing.Commands;
using Pharmacy.Application.Purchasing.Contracts;
using Pharmacy.Application.Purchasing.Models;
using Pharmacy.Application.Reporting.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Operations;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Security;
using Pharmacy.Infrastructure.Reporting;
using Pharmacy.Infrastructure.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class SecurityGateCoreTests
{
    private static readonly DateTime Now = new(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SEC020_DenyByDefaultInventoryAdjustCallsNoStore()
    {
        var authorizer = LimitedAuthorizer();
        var handlers = new InventoryCommandHandlers(new NeverInventoryStore(), new FixedInventoryDate(), authorizer);

        var adjust = await handlers.CreateLocationAsync(
            new CreateStockLocationCommand(Guid.NewGuid(), "QA", "QA"),
            TestContext.Current.CancellationToken);
        var finalize = await handlers.FinalizeSessionAsync(
            new FinalizeInventorySessionCommand(Guid.NewGuid(), 1, Convert.ToBase64String(new byte[8])),
            TestContext.Current.CancellationToken);

        Assert.Equal("security.authorization.denied", adjust.Error.Code);
        Assert.Equal("security.authorization.denied", finalize.Error.Code);
    }

    [Fact]
    public async Task SEC021_DenyByDefaultReportingViewExportAndSettingsCallNoDatabase()
    {
        var authorizer = LimitedAuthorizer();
        var reporting = new SqlReportingService(new ThrowingFactory(), authorizer, new NoOpAuditRecorder(), new FixedClock(Now));
        var query = new ReportingQuery(new ReportingPeriod(Now.AddDays(-1), Now), 10);

        var view = await reporting.GetSnapshotAsync(query, TestContext.Current.CancellationToken);
        var export = await reporting.ExportCsvAsync(
            new ReportingExportRequest(Guid.NewGuid(), ReportingExportKind.Sales, query),
            TestContext.Current.CancellationToken);
        var settings = await reporting.UpdateSettingsAsync(
            new UpdateAlertSettingsRequest(Guid.NewGuid(), true, false, null, new byte[8]),
            TestContext.Current.CancellationToken);

        Assert.Equal("security.authorization.denied", view.Error.Code);
        Assert.Equal("security.authorization.denied", export.Error.Code);
        Assert.Equal("security.authorization.denied", settings.Error.Code);
    }

    [Fact]
    public async Task SEC022_DenyByDefaultSecurityManagementCallsNoDatabase()
    {
        var session = LimitedSession();
        var clock = new FixedClock(Now);
        var administration = new SqlSecurityAdministrationService(
            new ThrowingFactory(),
            CreateHasher(),
            new FixedRecoveryGenerator(),
            new PermissionAuthorizer(session, clock),
            session,
            clock);

        var users = await administration.GetUsersAsync(TestContext.Current.CancellationToken);
        var roles = await administration.GetRolesAsync(TestContext.Current.CancellationToken);
        var createUser = await administration.CreateUserAsync(
            new CreateSecurityUserRequest(Guid.NewGuid(), "sec022", "SEC 022", "Strong-Password-SEC022!", [1]),
            TestContext.Current.CancellationToken);
        var createRole = await administration.CreateRoleAsync(
            new CreateSecurityRoleRequest(Guid.NewGuid(), "SEC022", "SEC 022", []),
            TestContext.Current.CancellationToken);

        Assert.Equal("security.authorization.denied", users.Error.Code);
        Assert.Equal("security.authorization.denied", roles.Error.Code);
        Assert.Equal("security.authorization.denied", createUser.Error.Code);
        Assert.Equal("security.authorization.denied", createRole.Error.Code);
    }

    [Fact]
    public async Task SEC023_DenyByDefaultPurchasingCustomersBackupAndImportCallNoMutation()
    {
        var authorizer = LimitedAuthorizer();
        var token = TestContext.Current.CancellationToken;
        var purchasing = new PurchasingCommandHandlers(new NeverPurchasingStore(), authorizer);
        var receipt = await purchasing.FinalizeReceiptAsync(new FinalizeGoodsReceiptCommand(
            Guid.NewGuid(), 1, null, 1, "BL-SEC023", new DateOnly(2026, 8, 17), null,
            [new FinalizeGoodsReceiptLineCommand(1, null, 1, 1m, null, null, null)]), token);

        var customers = new CustomerCommandHandlers(new NeverCustomerStore(), new NeverCreditPaymentStore(), authorizer);
        var customer = await customers.CreateAsync(
            new CreateCustomerRequest(Guid.NewGuid(), "Client SEC023", null, null, false), token);
        var credit = await customers.RegisterCreditPaymentAsync(
            new RegisterCreditPaymentRequest(Guid.NewGuid(), 1, 1m, SalePaymentMethod.Cash, false), token);

        var backup = new SqlBackupRecoveryService(new ThrowingFactory(), authorizer, new NoOpAuditRecorder(), new FixedClock(Now));
        var backupResult = await backup.CreateAsync(new CreateBackupRequest(Guid.NewGuid(), SqlBackupType.Full, false), token);
        var import = new SqlControlledImportService(new ThrowingFactory(), authorizer, new NoOpAuditRecorder(), new FixedClock(Now));
        var importResult = await import.ConfirmAsync(new ConfirmImportRequest(
            Guid.NewGuid(), "sec023.csv", "Reference;Designation\nP1;Produit"u8.ToArray(), ControlledImportType.Catalogue,
            new CsvImportMapping("Reference", "Designation")), token);

        Assert.Equal("security.authorization.denied", receipt.Error.Code);
        Assert.Equal("security.authorization.denied", customer.Error.Code);
        Assert.Equal("security.authorization.denied", credit.Error.Code);
        Assert.Equal("security.authorization.denied", backupResult.Error.Code);
        Assert.Equal("security.authorization.denied", importResult.Error.Code);
    }

    [Fact]
    public void SEC024_AuthorizedPermissionsAreExactAndUnexpectedPermissionsRemainDenied()
    {
        var authorizer = LimitedAuthorizer();
        var allowed = new[]
        {
            SecurityPermissions.CatalogView,
            SecurityPermissions.InventoryView,
            SecurityPermissions.SalesCreate,
            SecurityPermissions.SalesViewHistory
        };

        Assert.All(allowed, permission => Assert.True(authorizer.Authorize(permission).IsSuccess));
        Assert.All(SecurityPermissions.All.Except(allowed, StringComparer.Ordinal), permission =>
            Assert.Equal("security.authorization.denied", authorizer.Authorize(permission).Error.Code));
    }

    private static PermissionAuthorizer LimitedAuthorizer() =>
        new(LimitedSession(), new FixedClock(Now));

    private static CurrentSecuritySession LimitedSession()
    {
        var session = new CurrentSecuritySession();
        var permissions = new[]
        {
            SecurityPermissions.CatalogView,
            SecurityPermissions.InventoryView,
            SecurityPermissions.SalesCreate,
            SecurityPermissions.SalesViewHistory
        }.ToHashSet(StringComparer.Ordinal);
        session.Establish(new UserSessionView(
            23, "util.qa", "Utilisateur QA", ["ROLE-QA-LIMITE"], permissions, Now, Now), Now);
        return session;
    }

    private static IPasswordHasher<UserAccountEntity> CreateHasher() =>
        new PasswordHasher<UserAccountEntity>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = 210_000
        }));

    private sealed class FixedClock(DateTime now) : ISecurityClock
    {
        public DateTime UtcNow { get; } = now;
    }

    private sealed class FixedInventoryDate : IInventoryDateProvider
    {
        public DateOnly CurrentDate { get; } = new(2026, 8, 17);
    }

    private sealed class FixedRecoveryGenerator : IRecoveryCodeGenerator
    {
        public IReadOnlyList<string> Generate(int count) =>
            Enumerable.Range(1, count).Select(index => $"SEC-CORE-{index:0000}").ToArray();
    }

    private sealed class NoOpAuditRecorder : IBusinessAuditRecorder
    {
        public Task RecordSuccessAsync(string module, string entityType, string? entityId, string action, Guid? operationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordFailureAsync(string module, string entityType, string? entityId, string action, Guid? operationId, string reasonCode, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ThrowingFactory : IDbContextFactory<PharmacyDbContext>
    {
        public PharmacyDbContext CreateDbContext() => throw new InvalidOperationException("Aucun accès SQL ne doit suivre un refus RBAC.");
        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Aucun accès SQL ne doit suivre un refus RBAC.");
    }

    private sealed class NeverCustomerStore : ICustomerStore
    {
        private static Task<Result<T>> Unexpected<T>() => throw new InvalidOperationException("Le store client ne doit pas être appelé.");
        public Task<Result<CustomerDetailView>> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default) => Unexpected<CustomerDetailView>();
        public Task<Result<CustomerDetailView>> UpdateAsync(UpdateCustomerRequest request, CancellationToken cancellationToken = default) => Unexpected<CustomerDetailView>();
        public Task<Result<IReadOnlyList<CustomerSummaryView>>> SearchAsync(CustomerSearchQuery query, CancellationToken cancellationToken = default) => Unexpected<IReadOnlyList<CustomerSummaryView>>();
        public Task<Result<CustomerDetailView>> GetByIdAsync(long customerId, CancellationToken cancellationToken = default) => Unexpected<CustomerDetailView>();
    }

    private sealed class NeverCreditPaymentStore : ICreditPaymentStore
    {
        public Task<Result<CustomerDetailView>> RegisterPaymentAsync(RegisterCreditPaymentRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Le store crédit ne doit pas être appelé.");
    }

    private sealed class NeverPurchasingStore : IPurchasingStore
    {
        private static Task<Result<T>> Unexpected<T>() => throw new InvalidOperationException("Le store achats ne doit pas être appelé.");
        public Task<Result<SupplierView>> CreateSupplierAsync(CreateSupplierCommand command, CancellationToken cancellationToken = default) => Unexpected<SupplierView>();
        public Task<Result<SupplierView>> UpdateSupplierAsync(UpdateSupplierCommand command, CancellationToken cancellationToken = default) => Unexpected<SupplierView>();
        public Task<Result<SupplierView>> SetSupplierActivationAsync(SetSupplierActivationCommand command, CancellationToken cancellationToken = default) => Unexpected<SupplierView>();
        public Task<Result<PurchaseOrderView>> CreateOrderAsync(CreatePurchaseOrderCommand command, CancellationToken cancellationToken = default) => Unexpected<PurchaseOrderView>();
        public Task<Result<PurchaseOrderView>> AddOrderLineAsync(AddPurchaseOrderLineCommand command, CancellationToken cancellationToken = default) => Unexpected<PurchaseOrderView>();
        public Task<Result<PurchaseOrderView>> UpdateOrderLineAsync(UpdatePurchaseOrderLineCommand command, CancellationToken cancellationToken = default) => Unexpected<PurchaseOrderView>();
        public Task<Result<PurchaseOrderView>> RemoveOrderLineAsync(RemovePurchaseOrderLineCommand command, CancellationToken cancellationToken = default) => Unexpected<PurchaseOrderView>();
        public Task<Result<PurchaseOrderView>> ConfirmOrderAsync(ConfirmPurchaseOrderCommand command, CancellationToken cancellationToken = default) => Unexpected<PurchaseOrderView>();
        public Task<Result<PurchaseOrderView>> CancelOrderAsync(CancelPurchaseOrderCommand command, CancellationToken cancellationToken = default) => Unexpected<PurchaseOrderView>();
        public Task<Result<GoodsReceiptView>> FinalizeReceiptAsync(FinalizeGoodsReceiptCommand command, CancellationToken cancellationToken = default) => Unexpected<GoodsReceiptView>();
    }

    private sealed class NeverInventoryStore : IInventoryStore
    {
        private static Task<Result<T>> Unexpected<T>() => throw new InvalidOperationException("Le store stock ne doit pas être appelé.");
        public Task<Result<StockLocationView>> CreateLocationAsync(CreateStockLocationCommand command, CancellationToken cancellationToken = default) => Unexpected<StockLocationView>();
        public Task<Result<StockLocationView>> SetLocationActivationAsync(SetStockLocationActivationCommand command, CancellationToken cancellationToken = default) => Unexpected<StockLocationView>();
        public Task<Result<StockBatchView>> CreateBatchAsync(CreateStockBatchCommand command, DateOnly currentDate, CancellationToken cancellationToken = default) => Unexpected<StockBatchView>();
        public Task<Result<StockMovementView>> RecordMovementAsync(RecordStockMovementCommand command, DateOnly currentDate, CancellationToken cancellationToken = default) => Unexpected<StockMovementView>();
        public Task<Result<InventorySessionView>> CreateSessionAsync(CreateInventorySessionCommand command, CancellationToken cancellationToken = default) => Unexpected<InventorySessionView>();
        public Task<Result<InventorySessionView>> StartSessionAsync(StartInventorySessionCommand command, CancellationToken cancellationToken = default) => Unexpected<InventorySessionView>();
        public Task<Result<InventorySessionView>> SaveCountLineAsync(SaveInventoryCountLineCommand command, CancellationToken cancellationToken = default) => Unexpected<InventorySessionView>();
        public Task<Result<InventorySessionView>> FinalizeSessionAsync(FinalizeInventorySessionCommand command, CancellationToken cancellationToken = default) => Unexpected<InventorySessionView>();
        public Task<Result<InventorySessionView>> CancelSessionAsync(CancelInventorySessionCommand command, CancellationToken cancellationToken = default) => Unexpected<InventorySessionView>();
    }
}
