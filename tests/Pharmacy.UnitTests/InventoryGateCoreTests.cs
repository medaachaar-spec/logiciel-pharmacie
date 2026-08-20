using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Inventory.Models;
using Pharmacy.Application.Inventory.Queries;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Sales;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class InventoryGateCoreTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string RowVersion = Convert.ToBase64String(new byte[8]);

    [Fact]
    public async Task STK0606_InvalidCountedQuantityAndIdentifiersAreRejectedBeforeTheStore()
    {
        var store = new RecordingStore();
        var commands = new InventoryCommandHandlers(store, new FixedDate());
        var token = TestContext.Current.CancellationToken;

        Assert.Equal(InventoryErrors.InvalidQuantity.Code, (await commands.SaveCountLineAsync(
            new(Guid.NewGuid(), 1, 1, -1, RowVersion, RowVersion), token)).Error.Code);
        Assert.Equal(InventoryErrors.InvalidQuantity.Code, (await commands.SaveCountLineAsync(
            new(Guid.NewGuid(), 1, 0, 1, RowVersion, RowVersion), token)).Error.Code);
        Assert.Equal(InventoryErrors.InvalidQuantity.Code, (await commands.SaveCountLineAsync(
            new(Guid.NewGuid(), 1, 1, 1, "bad", RowVersion), token)).Error.Code);
        Assert.False(int.TryParse("2147483648", out _));
        Assert.False(int.TryParse("1.5", out _));
        Assert.False(int.TryParse("quantité", out _));
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task STK0624_BackendRbacProtectsInventoryReadsAdjustmentsAndFinalization()
    {
        var token = TestContext.Current.CancellationToken;
        var reader = new RecordingReader();
        var deniedQueries = new InventoryQueryHandlers(reader, new FixedDate(), Authorizer([]));
        Assert.Equal("security.authorization.denied", (await deniedQueries.ListSessionsAsync(cancellationToken: token)).Error.Code);
        Assert.Equal(0, reader.CallCount);

        var allowedQueries = new InventoryQueryHandlers(reader, new FixedDate(), Authorizer([SecurityPermissions.InventoryView]));
        Assert.True((await allowedQueries.ListSessionsAsync(cancellationToken: token)).IsSuccess);
        Assert.Equal(1, reader.CallCount);

        var deniedStore = new RecordingStore();
        var deniedCommands = new InventoryCommandHandlers(deniedStore, new FixedDate(), Authorizer([SecurityPermissions.InventoryView]));
        Assert.Equal("security.authorization.denied", (await deniedCommands.CreateSessionAsync(
            new(Guid.NewGuid(), "STK-RBAC", 1), token)).Error.Code);
        Assert.Equal("security.authorization.denied", (await deniedCommands.FinalizeSessionAsync(
            new(Guid.NewGuid(), 1, RowVersion), token)).Error.Code);
        Assert.Equal(0, deniedStore.CallCount);
    }

    [Fact]
    public async Task STK0625_FinalizationRequiresRecentReauthentication()
    {
        var token = TestContext.Current.CancellationToken;
        var staleStore = new RecordingStore();
        var stale = new InventoryCommandHandlers(
            staleStore,
            new FixedDate(),
            Authorizer([SecurityPermissions.InventoryFinalizeCount], Now.AddMinutes(-10)));
        Assert.Equal("security.authorization.reauthentication_required", (await stale.FinalizeSessionAsync(
            new(Guid.NewGuid(), 1, RowVersion), token)).Error.Code);
        Assert.Equal(0, staleStore.CallCount);

        var recentStore = new RecordingStore();
        var recent = new InventoryCommandHandlers(
            recentStore,
            new FixedDate(),
            Authorizer([SecurityPermissions.InventoryFinalizeCount], Now));
        Assert.True((await recent.FinalizeSessionAsync(new(Guid.NewGuid(), 1, RowVersion), token)).IsSuccess);
        Assert.Equal(1, recentStore.CallCount);
    }

    [Fact]
    public async Task STK0626_CreateFinalizeAndCancelProduceActorAwareBusinessAuditEvents()
    {
        var token = TestContext.Current.CancellationToken;
        var session = new CurrentSecuritySession();
        session.Establish(new UserSessionView(
            1606, "qa.inventory", "QA Inventory", ["OWNER"],
            new HashSet<string>(StringComparer.Ordinal), Now, Now), Now);
        var writer = new CapturingAuditWriter();
        var audit = new BusinessAuditRecorder(writer, session, new FixedSecurityClock());
        var commands = new InventoryCommandHandlers(new RecordingStore(), new FixedDate(), null, audit);

        Assert.True((await commands.CreateSessionAsync(new(Guid.NewGuid(), "STK-AUDIT", 1), token)).IsSuccess);
        Assert.True((await commands.FinalizeSessionAsync(new(Guid.NewGuid(), 1, RowVersion), token)).IsSuccess);
        Assert.True((await commands.CancelSessionAsync(new(Guid.NewGuid(), 1, RowVersion), token)).IsSuccess);

        Assert.Equal(["inventory.create", "inventory.finalize", "inventory.cancel"], writer.Events.Select(item => item.Action));
        Assert.All(writer.Events, item =>
        {
            Assert.Equal("inventory", item.Module);
            Assert.Equal("InventorySession", item.EntityType);
            Assert.Equal(1606, item.ActorUserId);
            Assert.Equal("QA Inventory", item.ActorDisplayName);
            Assert.Equal("Success", item.Result);
        });
    }

    private static IPermissionAuthorizer Authorizer(
        IReadOnlyCollection<string> permissions,
        DateTime? lastReauthenticated = null)
    {
        var session = new CurrentSecuritySession();
        var reauthenticated = lastReauthenticated ?? Now;
        session.Establish(new UserSessionView(
            1606,
            "qa.inventory",
            "QA Inventory",
            ["OWNER"],
            new HashSet<string>(permissions, StringComparer.Ordinal),
            Now,
            reauthenticated), Now);
        return new PermissionAuthorizer(session, new FixedSecurityClock());
    }

    private sealed class FixedSecurityClock : ISecurityClock { public DateTime UtcNow => Now; }
    private sealed class FixedDate : IInventoryDateProvider { public DateOnly CurrentDate => new(2026, 8, 20); }

    private sealed class CapturingAuditWriter : IAuditWriter
    {
        public List<AppendAuditEventRequest> Events { get; } = [];
        public Task<Result> AppendAsync(AppendAuditEventRequest request, CancellationToken cancellationToken = default)
        {
            Events.Add(request);
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class RecordingStore : IInventoryStore
    {
        public int CallCount { get; private set; }
        private Task<Result<T>> Success<T>(T value)
        {
            CallCount++;
            return Task.FromResult(Result<T>.Success(value));
        }
        private static InventorySessionView Session(InventorySessionStatus status = InventorySessionStatus.InProgress) =>
            new(1, "STK-SESSION", 1, "MAIN", status, Now, status == InventorySessionStatus.InProgress ? null : Now,
                null, RowVersion, []);

        public Task<Result<StockLocationView>> CreateLocationAsync(CreateStockLocationCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<StockLocationView>> SetLocationActivationAsync(SetStockLocationActivationCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<StockBatchView>> CreateBatchAsync(CreateStockBatchCommand command, DateOnly currentDate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<StockMovementView>> RecordMovementAsync(RecordStockMovementCommand command, DateOnly currentDate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<InventorySessionView>> CreateSessionAsync(CreateInventorySessionCommand command, CancellationToken cancellationToken = default) => Success(Session(InventorySessionStatus.Draft));
        public Task<Result<InventorySessionView>> StartSessionAsync(StartInventorySessionCommand command, CancellationToken cancellationToken = default) => Success(Session());
        public Task<Result<InventorySessionView>> SaveCountLineAsync(SaveInventoryCountLineCommand command, CancellationToken cancellationToken = default) => Success(Session());
        public Task<Result<InventorySessionView>> FinalizeSessionAsync(FinalizeInventorySessionCommand command, CancellationToken cancellationToken = default) => Success(Session(InventorySessionStatus.Completed));
        public Task<Result<InventorySessionView>> CancelSessionAsync(CancelInventorySessionCommand command, CancellationToken cancellationToken = default) => Success(Session(InventorySessionStatus.Cancelled));
    }

    private sealed class RecordingReader : IInventoryReader
    {
        public int CallCount { get; private set; }
        private Task<Result<T>> Called<T>(T value)
        {
            CallCount++;
            return Task.FromResult(Result<T>.Success(value));
        }
        public Task<Result<IReadOnlyList<StockLocationView>>> ListLocationsAsync(CancellationToken cancellationToken = default) => Called<IReadOnlyList<StockLocationView>>([]);
        public Task<Result<IReadOnlyList<StockOverviewItem>>> SearchStockAsync(StockSearchCriteria criteria, DateOnly currentDate, CancellationToken cancellationToken = default) => Called<IReadOnlyList<StockOverviewItem>>([]);
        public Task<Result<ProductStockDetails>> GetProductDetailsAsync(long productId, long? locationId, DateOnly currentDate, CancellationToken cancellationToken = default) => Called(new ProductStockDetails(productId, "P", "Produit", true, true, []));
        public Task<Result<IReadOnlyList<StockMovementView>>> GetMovementsAsync(long? productId, long? locationId, int limit, CancellationToken cancellationToken = default) => Called<IReadOnlyList<StockMovementView>>([]);
        public Task<Result<IReadOnlyList<StockBatchView>>> GetExpiringBatchesAsync(DateOnly currentDate, int horizonDays, CancellationToken cancellationToken = default) => Called<IReadOnlyList<StockBatchView>>([]);
        public Task<Result<IReadOnlyList<StockBatchView>>> GetExpiredBatchesAsync(DateOnly currentDate, CancellationToken cancellationToken = default) => Called<IReadOnlyList<StockBatchView>>([]);
        public Task<Result<FefoSuggestionView>> SuggestFefoAsync(ProductId productId, int requestedQuantity, DateOnly currentDate, CancellationToken cancellationToken = default) => Called(new FefoSuggestionView(productId.Value, requestedQuantity, []));
        public Task<Result<IReadOnlyList<InventorySessionView>>> ListSessionsAsync(int limit, CancellationToken cancellationToken = default) => Called<IReadOnlyList<InventorySessionView>>([]);
        public Task<Result<InventorySessionView>> GetSessionAsync(long sessionId, CancellationToken cancellationToken = default) => Called(new InventorySessionView(sessionId, "S", 1, "MAIN", InventorySessionStatus.Draft, Now, null, null, RowVersion, []));
        public Task<InventoryDiagnosticsView> GetDiagnosticsAsync(DateOnly currentDate, CancellationToken cancellationToken = default) => Task.FromResult(new InventoryDiagnosticsView(true, true, "Test", "Current", 1, 0, 0, 0, null));
    }
}
