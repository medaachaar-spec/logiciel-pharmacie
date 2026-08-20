using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Inventory.Models;
using Pharmacy.Application.Inventory.Queries;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Pharmacy.Infrastructure.Catalog;
using Pharmacy.Infrastructure.Inventory;
using Pharmacy.Infrastructure.Persistence;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class SqlInventoryGateIntegrationTests(SqlInventoryGateFixture fixture) : IClassFixture<SqlInventoryGateFixture>
{
    private long locationId;
    private CancellationToken Token => TestContext.Current.CancellationToken;
    private long LocationId => locationId != 0 ? locationId : throw new InvalidOperationException("Emplacement STK-06 non initialisé.");
    private InventoryCommandHandlers Commands(IDbContextFactory<PharmacyDbContext>? factory = null) =>
        new(new SqlInventoryStore(factory ?? fixture.Factory), fixture.Clock);
    private InventoryQueryHandlers Queries(IDbContextFactory<PharmacyDbContext>? factory = null) =>
        new(new SqlInventoryReader(factory ?? fixture.Factory), fixture.Clock);

    [Fact]
    public async Task STK0601_CreatingDraftSessionPersistsWithoutSnapshotOrStockMutation()
    {
        var prepared = await PreparedProductAsync("CREATE", [("LOT-A", 10, Future)]);
        var before = await fixture.StockCountsAsync(prepared.ProductId);
        var created = await Commands().CreateSessionAsync(new(Guid.NewGuid(), Ref("CREATE"), LocationId), Token);

        Assert.True(created.IsSuccess);
        Assert.Equal(InventorySessionStatus.Draft, created.Value.Status);
        Assert.Empty(created.Value.Lines);
        Assert.Equal(before, await fixture.StockCountsAsync(prepared.ProductId));
        Assert.Equal(0, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM inventory.InventoryCountLines WHERE InventorySessionId=@id;", P("@id", created.Value.SessionId)));
    }

    [Fact]
    public async Task STK0602_StartingSessionCapturesExactExpectedQuantities()
    {
        var scenario = await StartedAsync("SNAP", [("LOT-A", 10, Future), ("LOT-B", 4, Future.AddMonths(1))]);
        Assert.Equal(InventorySessionStatus.InProgress, scenario.Session.Status);
        Assert.Equal([4, 10], scenario.Session.Lines.Select(line => line.ExpectedQuantity).Order());
        Assert.All(scenario.Session.Lines, line => Assert.Null(line.CountedQuantity));
    }

    [Fact]
    public async Task STK0603_EqualCountProducesZeroVariance()
    {
        var scenario = await StartedAsync("ZERO", [("LOT-A", 10, Future)]);
        var saved = await CountAsync(scenario.Session, scenario.Session.Lines[0], 10);
        Assert.Equal(0, saved.Lines[0].DifferenceQuantity);
    }

    [Fact]
    public async Task STK0604_LowerCountProducesNegativeVariance()
    {
        var scenario = await StartedAsync("NEG", [("LOT-A", 10, Future)]);
        var saved = await CountAsync(scenario.Session, scenario.Session.Lines[0], 7);
        Assert.Equal(-3, saved.Lines[0].DifferenceQuantity);
    }

    [Fact]
    public async Task STK0605_HigherCountProducesPositiveVariance()
    {
        var scenario = await StartedAsync("POS", [("LOT-A", 10, Future)]);
        var saved = await CountAsync(scenario.Session, scenario.Session.Lines[0], 13);
        Assert.Equal(3, saved.Lines[0].DifferenceQuantity);
    }

    [Fact]
    public async Task STK0606B_UnknownCountLineIsRejectedWithoutRawSqlFailure()
    {
        var scenario = await StartedAsync("BADLINE", [("LOT-A", 10, Future)]);
        var line = Assert.Single(scenario.Session.Lines);
        var result = await Commands().SaveCountLineAsync(new(
            Guid.NewGuid(), scenario.Session.SessionId, line.CountLineId + 999_999, 8,
            scenario.Session.RowVersion, line.RowVersion), Token);
        Assert.Equal(InventoryErrors.InvalidOperation.Code, result.Error.Code);
        Assert.Equal(10, await fixture.StockAsync(scenario.ProductId));
    }

    [Fact]
    public async Task STK0607_TwoBatchesRemainDistinctThroughoutCounting()
    {
        var scenario = await StartedAsync("LOTS", [("LOT-A", 5, Future), ("LOT-B", 7, Future.AddMonths(1))]);
        Assert.Equal(2, scenario.Session.Lines.Select(line => line.BatchId).Distinct().Count());
        Assert.Equal(["LOT-A", "LOT-B"], scenario.Session.Lines.Select(line => line.LotNumber).Order());
        var counted = await CountAllAsync(scenario.Session, line => line.LotNumber == "LOT-A" ? 4 : 9);
        Assert.True((await Commands().FinalizeSessionAsync(new(Guid.NewGuid(), counted.SessionId, counted.RowVersion), Token)).IsSuccess);
        var balances = (await Queries().GetProductDetailsAsync(scenario.ProductId, LocationId, Token)).Value.Balances;
        Assert.Equal(4, balances.Single(item => item.LotNumber == "LOT-A").AvailableQuantity);
        Assert.Equal(9, balances.Single(item => item.LotNumber == "LOT-B").AvailableQuantity);
        Assert.Equal(13, balances.Sum(item => item.AvailableQuantity));
        var movements = (await Queries().GetMovementsAsync(scenario.ProductId, LocationId, cancellationToken: Token)).Value
            .Where(item => item.SourceType == "InventorySession" && item.SourceReference == counted.Reference)
            .ToArray();
        Assert.Contains(movements, item => item.LotNumber == "LOT-A" && item.MovementType == StockMovementType.InventoryDecrease && item.Quantity == 1);
        Assert.Contains(movements, item => item.LotNumber == "LOT-B" && item.MovementType == StockMovementType.InventoryIncrease && item.Quantity == 2);
    }

    [Fact]
    public async Task STK0608_CountingZeroFinalizesBalanceAtZero()
    {
        var scenario = await StartedAsync("COUNT0", [("LOT-A", 5, Future)]);
        var counted = await CountAllAsync(scenario.Session, _ => 0);
        var finalized = await Commands().FinalizeSessionAsync(new(Guid.NewGuid(), counted.SessionId, counted.RowVersion), Token);
        Assert.True(finalized.IsSuccess);
        Assert.Equal(0, await fixture.StockAsync(scenario.ProductId));
    }

    [Fact]
    public async Task STK0609_ZeroVarianceFinalizationCreatesNoInventoryMovement()
    {
        var scenario = await StartedAsync("NOMOVE", [("LOT-A", 5, Future)]);
        var before = await fixture.MovementCountAsync(scenario.ProductId);
        var counted = await CountAllAsync(scenario.Session, line => line.ExpectedQuantity);
        Assert.True((await Commands().FinalizeSessionAsync(new(Guid.NewGuid(), counted.SessionId, counted.RowVersion), Token)).IsSuccess);
        Assert.Equal(before, await fixture.MovementCountAsync(scenario.ProductId));
    }

    [Fact]
    public async Task STK0610_FinalizationAppliesVarianceExactlyOnce()
    {
        var scenario = await StartedAsync("FINAL", [("LOT-A", 10, Future)]);
        var counted = await CountAllAsync(scenario.Session, _ => 12);
        var finalized = await Commands().FinalizeSessionAsync(new(Guid.NewGuid(), counted.SessionId, counted.RowVersion), Token);
        Assert.True(finalized.IsSuccess);
        Assert.Equal(12, await fixture.StockAsync(scenario.ProductId));
        Assert.Equal(1, await fixture.InventoryMovementCountAsync(finalized.Value.SessionId));
    }

    [Fact]
    public async Task STK0611_SecondFinalizationWithDifferentOperationIsRejected()
    {
        var scenario = await StartedAsync("DOUBLE", [("LOT-A", 10, Future)]);
        var counted = await CountAllAsync(scenario.Session, _ => 9);
        Assert.True((await Commands().FinalizeSessionAsync(new(Guid.NewGuid(), counted.SessionId, counted.RowVersion), Token)).IsSuccess);
        var second = await Commands().FinalizeSessionAsync(new(Guid.NewGuid(), counted.SessionId, counted.RowVersion), Token);
        Assert.Equal(InventoryErrors.SessionAlreadyCompleted.Code, second.Error.Code);
        Assert.Equal(9, await fixture.StockAsync(scenario.ProductId));
    }

    [Fact]
    public async Task STK0612_ReplayingSameFinalizationOperationIsIdempotent()
    {
        var scenario = await StartedAsync("REPLAY", [("LOT-A", 10, Future)]);
        var counted = await CountAllAsync(scenario.Session, _ => 11);
        var command = new FinalizeInventorySessionCommand(Guid.NewGuid(), counted.SessionId, counted.RowVersion);
        var first = await Commands().FinalizeSessionAsync(command, Token);
        var replay = await Commands().FinalizeSessionAsync(command, Token);
        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(1, await fixture.InventoryMovementCountAsync(first.Value.SessionId));
    }

    [Fact]
    public async Task STK0614_IncompleteSessionCannotBeFinalized()
    {
        var scenario = await StartedAsync("INCOMP", [("LOT-A", 10, Future), ("LOT-B", 4, Future.AddMonths(1))]);
        var partial = await CountAsync(scenario.Session, scenario.Session.Lines[0], 10);
        var result = await Commands().FinalizeSessionAsync(new(Guid.NewGuid(), partial.SessionId, partial.RowVersion), Token);
        Assert.Equal(InventoryErrors.SessionInvalidStatus.Code, result.Error.Code);
        Assert.Equal(14, await fixture.StockAsync(scenario.ProductId));
    }

    [Fact]
    public async Task STK0615_FinalizedSessionIsImmutable()
    {
        var scenario = await StartedAsync("IMMUT", [("LOT-A", 10, Future)]);
        var counted = await CountAllAsync(scenario.Session, _ => 10);
        var completed = (await Commands().FinalizeSessionAsync(new(Guid.NewGuid(), counted.SessionId, counted.RowVersion), Token)).Value;
        var line = completed.Lines[0];
        Assert.Equal(InventoryErrors.SessionAlreadyCompleted.Code, (await Commands().SaveCountLineAsync(
            new(Guid.NewGuid(), completed.SessionId, line.CountLineId, 8, completed.RowVersion, line.RowVersion), Token)).Error.Code);
        Assert.Equal(InventoryErrors.SessionAlreadyCompleted.Code, (await Commands().CancelSessionAsync(
            new(Guid.NewGuid(), completed.SessionId, completed.RowVersion), Token)).Error.Code);
    }

    [Fact]
    public async Task STK0616_CancellingSessionDoesNotMutateStockOrMovements()
    {
        var scenario = await StartedAsync("CANCEL", [("LOT-A", 10, Future)]);
        var before = await fixture.StockCountsAsync(scenario.ProductId);
        var cancelled = await Commands().CancelSessionAsync(new(Guid.NewGuid(), scenario.Session.SessionId, scenario.Session.RowVersion), Token);
        Assert.True(cancelled.IsSuccess);
        Assert.Equal(InventorySessionStatus.Cancelled, cancelled.Value.Status);
        Assert.Equal(before, await fixture.StockCountsAsync(scenario.ProductId));
    }

    [Fact]
    public async Task STK0617_ConcurrentFinalizationHasOneEffectiveStockMutation()
    {
        var scenario = await StartedAsync("RACE", [("LOT-A", 10, Future)]);
        var counted = await CountAllAsync(scenario.Session, _ => 12);
        var command = new FinalizeInventorySessionCommand(Guid.NewGuid(), counted.SessionId, counted.RowVersion);
        var results = await Task.WhenAll(
            Commands(fixture.CreateFactory()).FinalizeSessionAsync(command, Token),
            Commands(fixture.CreateFactory()).FinalizeSessionAsync(command, Token));
        Assert.Contains(results, result => result.IsSuccess);
        Assert.Equal(12, await fixture.StockAsync(scenario.ProductId));
        Assert.Equal(1, await fixture.InventoryMovementCountAsync(counted.SessionId));
    }

    [Fact]
    public async Task STK0618_ExternalStockChangeAfterSnapshotCausesConcurrencyConflict()
    {
        var scenario = await StartedAsync("EXT", [("LOT-A", 10, Future)]);
        var counted = await CountAllAsync(scenario.Session, _ => 10);
        var balance = (await Queries().GetProductDetailsAsync(scenario.ProductId, LocationId, Token)).Value.Balances[0];
        Assert.True((await Commands().DecreaseAsync(Change(scenario, balance, 2, StockMovementType.ManualDecrease), Token)).IsSuccess);
        var result = await Commands().FinalizeSessionAsync(new(Guid.NewGuid(), counted.SessionId, counted.RowVersion), Token);
        Assert.Equal(InventoryErrors.ConcurrencyConflict.Code, result.Error.Code);
        Assert.Equal(8, await fixture.StockAsync(scenario.ProductId));
    }

    [Fact]
    public async Task STK0619_TwoOpenSessionsAreAllowedButStaleSecondFinalizationConflicts()
    {
        var prepared = await PreparedProductAsync("TWO", [("LOT-A", 10, Future)]);
        var first = await CreateAndStartAsync("TWO-A");
        var second = await CreateAndStartAsync("TWO-B");
        var firstCounted = await CountAllAsync(first, _ => 11);
        Assert.True((await Commands().FinalizeSessionAsync(new(Guid.NewGuid(), firstCounted.SessionId, firstCounted.RowVersion), Token)).IsSuccess);
        var secondCounted = await CountAllAsync(second, line => line.ExpectedQuantity);
        Assert.Equal(InventoryErrors.ConcurrencyConflict.Code, (await Commands().FinalizeSessionAsync(
            new(Guid.NewGuid(), secondCounted.SessionId, secondCounted.RowVersion), Token)).Error.Code);
        Assert.Equal(11, await fixture.StockAsync(prepared.ProductId));
    }

    [Fact]
    public async Task STK0620_ExpiredLotsRemainInPhysicalInventoryCounts()
    {
        var scenario = await StartedAsync("EXPIRED", [("LOT-OLD", 6, fixture.Clock.CurrentDate.AddDays(-1))]);
        var line = Assert.Single(scenario.Session.Lines);
        Assert.Equal(6, line.ExpectedQuantity);
        Assert.Equal("LOT-OLD", line.LotNumber);
        var overview = Assert.Single((await Queries().SearchStockAsync(new(LocationId: LocationId), Token)).Value,
            item => item.ProductId == scenario.ProductId);
        Assert.Equal((6, 0, 6), (overview.TotalQuantity, overview.SellableQuantity, overview.ExpiredQuantity));
        var counted = await CountAllAsync(scenario.Session, _ => 5);
        Assert.True((await Commands().FinalizeSessionAsync(new(Guid.NewGuid(), counted.SessionId, counted.RowVersion), Token)).IsSuccess);
        overview = Assert.Single((await Queries().SearchStockAsync(new(LocationId: LocationId), Token)).Value,
            item => item.ProductId == scenario.ProductId);
        Assert.Equal((5, 0, 5), (overview.TotalQuantity, overview.SellableQuantity, overview.ExpiredQuantity));
        Assert.Contains((await Queries().GetMovementsAsync(scenario.ProductId, LocationId, cancellationToken: Token)).Value,
            movement => movement.LotNumber == "LOT-OLD" && movement.MovementType == StockMovementType.InventoryDecrease && movement.Quantity == 1);
    }

    [Fact]
    public async Task STK0621_DisabledProductRemainsCountableForHistoricalStockIntegrity()
    {
        var prepared = await PreparedProductAsync("DISABLED", [("LOT-A", 5, Future)]);
        var deactivated = await new SetCatalogProductActivationHandler(new SqlCatalogStore(fixture.Factory)).HandleAsync(new(
            prepared.ProductId, false, false, prepared.ProductRowVersion, prepared.LocalSettingsRowVersion), Token);
        Assert.True(deactivated.IsSuccess);
        var session = await CreateAndStartAsync("DISABLED");
        var line = Assert.Single(session.Lines, item => item.ProductId == prepared.ProductId);
        var counted = await CountAllAsync(session, item => item.CountLineId == line.CountLineId ? 4 : item.ExpectedQuantity);
        Assert.True((await Commands().FinalizeSessionAsync(new(Guid.NewGuid(), counted.SessionId, counted.RowVersion), Token)).IsSuccess);
        Assert.Equal(4, await fixture.StockAsync(prepared.ProductId));
    }

    [Fact]
    public async Task STK0622_FinalizationCannotCreateNegativeStock()
    {
        var scenario = await StartedAsync("NONNEG", [("LOT-A", 2, Future)]);
        var counted = await CountAllAsync(scenario.Session, _ => 0);
        Assert.True((await Commands().FinalizeSessionAsync(new(Guid.NewGuid(), counted.SessionId, counted.RowVersion), Token)).IsSuccess);
        Assert.Equal(0, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM inventory.StockBalances WHERE AvailableQuantity < 0;"));
    }

    [Fact]
    public async Task STK0623_AdjustmentMovementContainsExactSessionTraceability()
    {
        var scenario = await StartedAsync("TRACE", [("LOT-A", 10, Future)]);
        var counted = await CountAllAsync(scenario.Session, _ => 7);
        var completed = (await Commands().FinalizeSessionAsync(new(Guid.NewGuid(), counted.SessionId, counted.RowVersion), Token)).Value;
        var movement = Assert.Single((await Queries().GetMovementsAsync(scenario.ProductId, LocationId, cancellationToken: Token)).Value,
            item => item.SourceType == "InventorySession" && item.SourceReference == completed.Reference);
        Assert.Equal(StockMovementType.InventoryDecrease, movement.MovementType);
        Assert.Equal((3, 10, 7), (movement.Quantity, movement.QuantityBefore, movement.QuantityAfter));
        Assert.NotEqual(Guid.Empty, movement.OperationId);
    }

    [Fact]
    public async Task STK0627_CompletedSessionAndStockReloadFromFreshDbContexts()
    {
        var scenario = await StartedAsync("RELOAD", [("LOT-A", 10, Future)]);
        var counted = await CountAllAsync(scenario.Session, _ => 14);
        var factoryB = fixture.CreateFactory();
        var reloadedOpen = await Queries(factoryB).GetSessionAsync(counted.SessionId, Token);
        Assert.Equal(InventorySessionStatus.InProgress, reloadedOpen.Value.Status);
        Assert.Equal(14, Assert.Single(reloadedOpen.Value.Lines).CountedQuantity);
        Assert.True((await Commands(factoryB).FinalizeSessionAsync(new(
            Guid.NewGuid(), reloadedOpen.Value.SessionId, reloadedOpen.Value.RowVersion), Token)).IsSuccess);

        var factoryC = fixture.CreateFactory();
        var loaded = await Queries(factoryC).GetSessionAsync(counted.SessionId, Token);
        var details = await Queries(factoryC).GetProductDetailsAsync(scenario.ProductId, LocationId, Token);
        Assert.Equal(InventorySessionStatus.Completed, loaded.Value.Status);
        Assert.Equal(14, Assert.Single(details.Value.Balances).AvailableQuantity);
        Assert.Equal(1, await fixture.InventoryMovementCountAsync(counted.SessionId));
    }

    [Fact]
    public async Task STK0628_UnavailableSqlReturnsControlledFailureWithoutLocalFallback()
    {
        await EnsureTestLocationAsync();
        var result = await Commands(new NestedUnavailableFactory()).CreateSessionAsync(
            new(Guid.NewGuid(), Ref("OFFLINE"), LocationId), Token);
        Assert.Equal(InventoryErrors.DatabaseUnavailable.Code, result.Error.Code);
    }

    [Fact]
    public async Task STK0629_FailureDuringMovementInsertRollsBackWholeFinalization()
    {
        var scenario = await StartedAsync("ROLLBACK", [("LOT-A", 10, Future)]);
        var counted = await CountAllAsync(scenario.Session, _ => 12);
        var result = await Commands(fixture.CreateFactory(new FailInventoryMovementInsertInterceptor())).FinalizeSessionAsync(
            new(Guid.NewGuid(), counted.SessionId, counted.RowVersion), Token);
        Assert.Equal(InventoryErrors.DatabaseUnavailable.Code, result.Error.Code);
        Assert.Equal(10, await fixture.StockAsync(scenario.ProductId));
        Assert.Equal(0, await fixture.InventoryMovementCountAsync(counted.SessionId));
        Assert.Equal(InventorySessionStatus.InProgress, (await Queries().GetSessionAsync(counted.SessionId, Token)).Value.Status);
    }

    [Fact]
    public async Task STK0631_NetZeroExternalMovementsAfterSnapshotStillCauseConcurrencyConflict()
    {
        var scenario = await StartedAsync("NETZERO", [("LOT-A", 10, Future)]);
        var counted = await CountAllAsync(scenario.Session, _ => 10);
        var balance = (await Queries().GetProductDetailsAsync(scenario.ProductId, LocationId, Token)).Value.Balances[0];
        Assert.True((await Commands().IncreaseAsync(Change(scenario, balance, 1, StockMovementType.ManualIncrease), Token)).IsSuccess);
        balance = (await Queries().GetProductDetailsAsync(scenario.ProductId, LocationId, Token)).Value.Balances[0];
        Assert.True((await Commands().DecreaseAsync(Change(scenario, balance, 1, StockMovementType.ManualDecrease), Token)).IsSuccess);
        Assert.Equal(10, await fixture.StockAsync(scenario.ProductId));
        var result = await Commands().FinalizeSessionAsync(new(Guid.NewGuid(), counted.SessionId, counted.RowVersion), Token);
        Assert.Equal(InventoryErrors.ConcurrencyConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task STK0632_NewBalanceAfterSnapshotCausesConcurrencyConflict()
    {
        var scenario = await StartedAsync("NEWBAL", [("LOT-A", 10, Future)]);
        var counted = await CountAllAsync(scenario.Session, _ => 10);
        _ = await PreparedProductAsync("NEWBAL-LATE", [("LOT-LATE", 2, Future)]);
        var result = await Commands().FinalizeSessionAsync(new(Guid.NewGuid(), counted.SessionId, counted.RowVersion), Token);
        Assert.Equal(InventoryErrors.ConcurrencyConflict.Code, result.Error.Code);
    }

    private DateOnly Future => fixture.Clock.CurrentDate.AddYears(1);

    private async Task<PreparedProduct> PreparedProductAsync(string tag, IReadOnlyList<(string Lot, int Quantity, DateOnly Expiry)> batches)
    {
        await EnsureTestLocationAsync();
        var code = Code(tag);
        var product = await new CreateCatalogProductHandler(new SqlCatalogStore(fixture.Factory)).HandleAsync(new(
            $"Produit STK {tag}", "Comptage réel", fixture.CategoryId, code, 20m,
            TracksLots: true, TracksExpiry: true), Token);
        Assert.True(product.IsSuccess, product.IsFailure ? product.Error.Code : null);
        var commands = Commands();
        foreach (var batch in batches)
        {
            var created = await commands.CreateBatchAsync(new(Guid.NewGuid(), product.Value.ProductId, batch.Lot, batch.Expiry), Token);
            Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
            var opening = await commands.RecordOpeningAsync(new(
                Guid.NewGuid(), StockMovementType.Opening, product.Value.ProductId, LocationId,
                created.Value.BatchId, batch.Quantity, "Ouverture STK-06", "STK06Gate", tag), Token);
            Assert.True(opening.IsSuccess, opening.IsFailure ? opening.Error.Code : null);
        }
        return new(product.Value.ProductId, product.Value.ProductRowVersion, product.Value.LocalSettingsRowVersion);
    }

    private async Task<StartedScenario> StartedAsync(string tag, IReadOnlyList<(string Lot, int Quantity, DateOnly Expiry)> batches)
    {
        var product = await PreparedProductAsync(tag, batches);
        return new(product.ProductId, await CreateAndStartAsync(tag));
    }

    private async Task<InventorySessionView> CreateAndStartAsync(string tag)
    {
        var created = await Commands().CreateSessionAsync(new(Guid.NewGuid(), Ref(tag), LocationId), Token);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
        var started = await Commands().StartSessionAsync(new(Guid.NewGuid(), created.Value.SessionId, created.Value.RowVersion), Token);
        Assert.True(started.IsSuccess, started.IsFailure ? started.Error.Code : null);
        return started.Value;
    }

    private async Task<InventorySessionView> CountAsync(InventorySessionView session, InventoryCountLineView line, int quantity)
    {
        var current = session.Lines.Single(item => item.CountLineId == line.CountLineId);
        var result = await Commands().SaveCountLineAsync(new(
            Guid.NewGuid(), session.SessionId, current.CountLineId, quantity, session.RowVersion, current.RowVersion), Token);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    private async Task<InventorySessionView> CountAllAsync(InventorySessionView session, Func<InventoryCountLineView, int> count)
    {
        foreach (var original in session.Lines.ToArray())
        {
            var current = session.Lines.Single(item => item.CountLineId == original.CountLineId);
            session = await CountAsync(session, current, count(current));
        }
        return session;
    }

    private RecordStockMovementCommand Change(StartedScenario scenario, StockBatchView balance, int quantity, StockMovementType type) =>
        new(Guid.NewGuid(), type, scenario.ProductId, LocationId, balance.BatchId, quantity,
            "Changement concurrent STK-06", "STK06Gate", scenario.Session.Reference, balance.BalanceRowVersion);

    private async Task EnsureTestLocationAsync()
    {
        if (locationId != 0) return;
        var suffix = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var created = await Commands().CreateLocationAsync(new(
            Guid.NewGuid(), $"STK-{suffix}", $"Emplacement test {suffix}"), Token);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
        locationId = created.Value.LocationId;
    }

    private static SqlParameter P(string name, long value) => new(name, SqlDbType.BigInt) { Value = value };
    private static string Ref(string tag) => $"STK-{new string(tag.Where(char.IsLetterOrDigit).Take(12).ToArray()).ToUpperInvariant()}-{Guid.NewGuid():N}"[..28];
    private static string Code(string tag) => $"S{new string(tag.Where(char.IsLetterOrDigit).Take(10).ToArray()).ToUpperInvariant()}-{Guid.NewGuid():N}"[..24];
    private sealed record PreparedProduct(long ProductId, string ProductRowVersion, string LocalSettingsRowVersion);
    private sealed record StartedScenario(long ProductId, InventorySessionView Session);

    private sealed class NestedUnavailableFactory : IDbContextFactory<PharmacyDbContext>
    {
        public PharmacyDbContext CreateDbContext() => throw Failure();
        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromException<PharmacyDbContext>(Failure());
        private static Exception Failure() => new Exception("EF wrapper", new InvalidOperationException("SQL unavailable"));
    }

    private sealed class FailInventoryMovementInsertInterceptor : DbCommandInterceptor
    {
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) => Fail(command, result);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) => ValueTask.FromResult(Fail(command, result));
        private static InterceptionResult<DbDataReader> Fail(DbCommand command, InterceptionResult<DbDataReader> result)
        {
            if (command.CommandText.Contains("INSERT", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("[inventory].[StockMovements]", StringComparison.Ordinal))
                throw new InvalidOperationException("Synthetic STK0629 SQL failure before commit.");
            return result;
        }
    }
}

public sealed class SqlInventoryGateFixture : IAsyncLifetime
{
    private const string Prefix = "Pharmacy_STK06_Gate_Test_";
    private string serverConnection = string.Empty;
    private string databaseConnection = string.Empty;
    private string databaseName = string.Empty;

    public GateClock Clock { get; } = new(new DateOnly(2026, 8, 20));
    public IDbContextFactory<PharmacyDbContext> Factory { get; private set; } = null!;
    public long CategoryId { get; private set; }
    public long LocationId { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException("PHARMACY_TEST_SQLSERVER_CONNECTION est requise.");
        serverConnection = new SqlConnectionStringBuilder(raw)
        {
            InitialCatalog = "master",
            Pooling = false,
            ApplicationName = "Pharmacy.STK06.Gate.Tests"
        }.ConnectionString;
        databaseName = Prefix + Guid.NewGuid().ToString("N")[..12];
        ValidateDatabaseName(databaseName);
        await ExecuteServerAsync($"CREATE DATABASE [{databaseName}];");
        databaseConnection = new SqlConnectionStringBuilder(serverConnection) { InitialCatalog = databaseName }.ConnectionString;
        Factory = CreateFactory();
        await using (var context = await Factory.CreateDbContextAsync())
        {
            await context.Database.MigrateAsync();
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        }
        var category = await new SqlCatalogStore(Factory).CreateAsync(new CreateCatalogCategoryCommand(
            "STK06-GATE", "Catégorie STK-06", DefaultTracksLots: true, DefaultTracksExpiry: true), CancellationToken.None);
        Assert.True(category.IsSuccess);
        CategoryId = category.Value.CategoryId;
        var location = await new InventoryCommandHandlers(new SqlInventoryStore(Factory), Clock).CreateLocationAsync(
            new(Guid.NewGuid(), "STK06-GATE", "Emplacement STK-06", true), CancellationToken.None);
        Assert.True(location.IsSuccess);
        LocationId = location.Value.LocationId;
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

    public async Task<(int Stock, int Movements)> StockCountsAsync(long productId) =>
        (await StockAsync(productId), await MovementCountAsync(productId));
    public Task<int> StockAsync(long productId) => ScalarAsync<int>(
        "SELECT COALESCE(SUM(AvailableQuantity),0) FROM inventory.StockBalances WHERE ProductId=@id;", new SqlParameter("@id", productId));
    public Task<int> MovementCountAsync(long productId) => ScalarAsync<int>(
        "SELECT COUNT(*) FROM inventory.StockMovements WHERE ProductId=@id;", new SqlParameter("@id", productId));
    public Task<int> InventoryMovementCountAsync(long sessionId) => ScalarAsync<int>(
        "SELECT COUNT(*) FROM inventory.StockMovements m JOIN inventory.InventorySessions s ON s.Reference=m.SourceReference WHERE s.Id=@id AND m.SourceType=N'InventorySession';",
        new SqlParameter("@id", sessionId));

    public async Task<T> ScalarAsync<T>(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(databaseConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Valeur SQL absente.");
        return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
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
        if (!Regex.IsMatch(value, "^Pharmacy_STK06_Gate_Test_[0-9a-f]{12}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Nom de base temporaire STK-06 refusé.");
    }

    public sealed class GateClock(DateOnly date) : IInventoryDateProvider { public DateOnly CurrentDate { get; } = date; }
    private sealed class FactoryImpl(DbContextOptions<PharmacyDbContext> options) : IDbContextFactory<PharmacyDbContext>
    {
        public PharmacyDbContext CreateDbContext() => new(options);
        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
