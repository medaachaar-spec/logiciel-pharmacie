using Pharmacy.Application.Customers.Commands;
using Pharmacy.Application.Customers.Contracts;
using Pharmacy.Application.Customers.Models;
using Pharmacy.Application.Customers.Queries;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Finalization.Models;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class CustomerCreditApplicationTests
{
    [Fact]
    public async Task CRD020_BackendRefusesCreditSettlementAndQueriesBeforeStoreMutation()
    {
        var store = new RecordingCustomerStore();
        var authorizer = new RecordingAuthorizer(Result.Failure(SecurityErrors.PermissionDenied));
        var commands = new CustomerCommandHandlers(store, store, authorizer);
        var queries = new CustomerQueryHandlers(store, authorizer);

        var payment = await commands.RegisterCreditPaymentAsync(
            Payment(Guid.NewGuid(), 10m),
            TestContext.Current.CancellationToken);
        var search = await queries.SearchAsync(
            new CustomerSearchQuery(null),
            TestContext.Current.CancellationToken);

        Assert.Equal(SecurityErrors.PermissionDenied.Code, payment.Error.Code);
        Assert.Equal(SecurityErrors.PermissionDenied.Code, search.Error.Code);
        Assert.Equal(0, store.PaymentCalls);
        Assert.Equal(0, store.SearchCalls);
        Assert.Contains(authorizer.Requests, request =>
            request.Permission == SecurityPermissions.CustomersManageCredit && request.Recent);
        Assert.Contains(authorizer.Requests, request =>
            request.Permission == SecurityPermissions.CustomersView && !request.Recent);
    }

    [Fact]
    public async Task CRD020_SensitiveCreditSettlementRequiresRecentReauthentication()
    {
        var store = new RecordingCustomerStore();
        var authorizer = new RecordingAuthorizer(Result.Failure(SecurityErrors.ReauthenticationRequired));
        var commands = new CustomerCommandHandlers(store, store, authorizer);

        var result = await commands.RegisterCreditPaymentAsync(
            Payment(Guid.NewGuid(), 10m),
            TestContext.Current.CancellationToken);

        Assert.Equal(SecurityErrors.ReauthenticationRequired.Code, result.Error.Code);
        Assert.Equal(0, store.PaymentCalls);
        Assert.Single(authorizer.Requests);
        Assert.True(authorizer.Requests[0].Recent);
    }

    [Fact]
    public async Task CRD020_CreditSaleRequiresSalesCreateBeforePreparationOrMutation()
    {
        var never = new NeverSalesBackend();
        var authorizer = new RecordingAuthorizer(Result.Failure(SecurityErrors.PermissionDenied));
        var handler = new FinalizeSaleHandler(
            new PrepareSaleFinalizationHandler(never, never, never),
            never,
            authorizer);

        var result = await handler.HandleAsync(
            new FinalizeSaleCommand(
                Guid.NewGuid(),
                SaleDraftId.New(),
                1,
                SalePaymentMethod.Cash,
                40m,
                false,
                42,
                60m),
            TestContext.Current.CancellationToken);

        Assert.Equal(SecurityErrors.PermissionDenied.Code, result.Error.Code);
        Assert.False(never.WasCalled);
        Assert.Equal((SecurityPermissions.SalesCreate, false), Assert.Single(authorizer.Requests));
    }

    [Fact]
    public async Task CRD021_CustomerCreditActionsProduceTraceableAuditsWithoutSensitiveCardData()
    {
        var store = new RecordingCustomerStore();
        var audit = new RecordingAudit();
        var commands = new CustomerCommandHandlers(
            store,
            store,
            new RecordingAuthorizer(Result.Success()),
            audit);

        var createOperation = Guid.NewGuid();
        var paymentOperation = Guid.NewGuid();
        Assert.True((await commands.CreateAsync(
            new CreateCustomerRequest(createOperation, "Client QA", null, null, true),
            TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await commands.RegisterCreditPaymentAsync(
            Payment(paymentOperation, 10m, SalePaymentMethod.ExternalCard, true),
            TestContext.Current.CancellationToken)).IsSuccess);

        Assert.Equal(
            ["customer.create", "credit.allow", "credit.payment"],
            audit.Events.Select(item => item.Action));
        Assert.Equal(createOperation, audit.Events[0].OperationId);
        Assert.Equal(paymentOperation, audit.Events[2].OperationId);
        Assert.DoesNotContain(audit.Events, item =>
            item.Action.Contains("PAN", StringComparison.OrdinalIgnoreCase)
            || item.Action.Contains("CVV", StringComparison.OrdinalIgnoreCase)
            || item.Action.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CRD004_012_013_022_InvalidSettlementPayloadsStopBeforePersistence()
    {
        var store = new RecordingCustomerStore();
        var commands = new CustomerCommandHandlers(
            store,
            store,
            new RecordingAuthorizer(Result.Success()));
        var invalidRequests = new[]
        {
            Payment(Guid.Empty, 10m),
            Payment(Guid.NewGuid(), 0m),
            Payment(Guid.NewGuid(), -1m),
            Payment(Guid.NewGuid(), 10m, SalePaymentMethod.Cash, true),
            Payment(Guid.NewGuid(), 10m, SalePaymentMethod.ExternalCard, false),
            Payment(Guid.NewGuid(), 10m, (SalePaymentMethod)999, false)
        };

        foreach (var request in invalidRequests)
        {
            var result = await commands.RegisterCreditPaymentAsync(
                request,
                TestContext.Current.CancellationToken);
            Assert.True(result.IsFailure);
        }

        Assert.Equal(0, store.PaymentCalls);
    }

    private static RegisterCreditPaymentRequest Payment(
        Guid operationId,
        decimal amount,
        SalePaymentMethod method = SalePaymentMethod.Cash,
        bool externalCardConfirmed = false) =>
        new(operationId, 42, amount, method, externalCardConfirmed);

    private static CustomerDetailView Detail(decimal balance = 100m) => new(
        42,
        "CLIENT-QA-CREDIT-01",
        "Client QA crédit",
        null,
        null,
        true,
        true,
        Money.Create(balance).Value,
        new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc),
        [1],
        Array.Empty<CustomerLedgerEntryView>());

    private sealed class RecordingCustomerStore : ICustomerStore, ICreditPaymentStore
    {
        public int PaymentCalls { get; private set; }
        public int SearchCalls { get; private set; }

        public Task<Result<CustomerDetailView>> CreateAsync(
            CreateCustomerRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<CustomerDetailView>.Success(Detail()));

        public Task<Result<CustomerDetailView>> UpdateAsync(
            UpdateCustomerRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<CustomerDetailView>.Success(Detail()));

        public Task<Result<IReadOnlyList<CustomerSummaryView>>> SearchAsync(
            CustomerSearchQuery query,
            CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            return Task.FromResult(Result<IReadOnlyList<CustomerSummaryView>>.Success(
                Array.Empty<CustomerSummaryView>()));
        }

        public Task<Result<CustomerDetailView>> GetByIdAsync(
            long customerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<CustomerDetailView>.Success(Detail()));

        public Task<Result<CustomerDetailView>> RegisterPaymentAsync(
            RegisterCreditPaymentRequest request,
            CancellationToken cancellationToken = default)
        {
            PaymentCalls++;
            return Task.FromResult(Result<CustomerDetailView>.Success(Detail(90m)));
        }
    }

    private sealed class RecordingAuthorizer(Result result) : IPermissionAuthorizer
    {
        public List<(string Permission, bool Recent)> Requests { get; } = new();

        public Result Authorize(string permission, bool requireRecentReauthentication = false)
        {
            Requests.Add((permission, requireRecentReauthentication));
            return result;
        }
    }

    private sealed class NeverSalesBackend :
        ISaleDraftStore,
        IStockAvailabilityReader,
        ISaleFinalizationPreparationStore,
        ISaleFinalizer
    {
        public bool WasCalled { get; private set; }

        public Task<Result<VersionedSaleDraft>> CreateAsync(SaleDraft draft, CancellationToken cancellationToken = default) =>
            Unexpected<VersionedSaleDraft>();

        public Task<Result<VersionedSaleDraft>> GetAsync(SaleDraftId draftId, CancellationToken cancellationToken = default) =>
            Unexpected<VersionedSaleDraft>();

        public Task<Result<VersionedSaleDraft>> SaveAsync(SaleDraft draft, long expectedVersion, CancellationToken cancellationToken = default) =>
            Unexpected<VersionedSaleDraft>();

        public Task<Result<StockAvailability>> GetAvailabilityAsync(ProductId productId, SaleQuantity requestedQuantity, CancellationToken cancellationToken = default) =>
            Unexpected<StockAvailability>();

        public Task<Result<SaleFinalizationPreparation>> GetOrAddAsync(SaleFinalizationPreparation preparation, CancellationToken cancellationToken = default) =>
            Unexpected<SaleFinalizationPreparation>();

        public Task<Result<FinalizedSaleView>> FinalizeAsync(FinalizeSaleRequest request, CancellationToken cancellationToken = default) =>
            Unexpected<FinalizedSaleView>();

        private Task<Result<T>> Unexpected<T>()
        {
            WasCalled = true;
            throw new InvalidOperationException("Le backend ne doit pas être appelé après un refus RBAC.");
        }
    }

    private sealed class RecordingAudit : IBusinessAuditRecorder
    {
        public List<(string Action, Guid? OperationId)> Events { get; } = new();

        public Task RecordSuccessAsync(
            string module,
            string entityType,
            string? entityId,
            string action,
            Guid? operationId,
            CancellationToken cancellationToken = default)
        {
            Events.Add((action, operationId));
            return Task.CompletedTask;
        }
    }
}
