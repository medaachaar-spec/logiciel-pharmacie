using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Inventory.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class InventoryCoreTests
{
    private static readonly CancellationToken TestCancellation = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void MovementQuantityMustBeStrictlyPositive(int value)
    {
        var result = StockMovementQuantity.Create(value);

        Assert.True(result.IsFailure);
        Assert.Equal("inventory.quantity.invalid", result.Error.Code);
    }

    [Fact]
    public void StockQuantityAllowsZeroButNeverNegative()
    {
        Assert.Equal(0, StockQuantity.Create(0).Value.Value);
        Assert.Equal("inventory.quantity.invalid", StockQuantity.Create(-1).Error.Code);
    }

    [Fact]
    public void DecreaseRefusesNegativeStockWithoutPartialState()
    {
        var initial = StockBalanceState.Create(3).Value;
        var result = initial.Apply(
            StockMovementType.ManualDecrease,
            StockMovementQuantity.Create(4).Value);

        Assert.True(result.IsFailure);
        Assert.Equal("inventory.stock.insufficient", result.Error.Code);
        Assert.Equal(3, initial.Quantity.Value);
    }

    [Fact]
    public void IncreaseAndDecreaseProduceDeterministicBalances()
    {
        var initial = StockBalanceState.Create(10).Value;
        var increased = initial.Apply(
            StockMovementType.ManualIncrease,
            StockMovementQuantity.Create(5).Value);
        var decreased = increased.Value.Apply(
            StockMovementType.InventoryDecrease,
            StockMovementQuantity.Create(4).Value);

        Assert.Equal(15, increased.Value.Quantity.Value);
        Assert.Equal(11, decreased.Value.Quantity.Value);
        Assert.Equal(10, initial.Quantity.Value);
    }

    [Fact]
    public void QuantityOverflowIsRejected()
    {
        var maximum = StockQuantity.Create(int.MaxValue).Value;
        var result = maximum.Increase(StockMovementQuantity.Create(1).Value);

        Assert.True(result.IsFailure);
        Assert.Equal("inventory.quantity.invalid", result.Error.Code);
    }

    [Fact]
    public void TrackingPolicyRequiresConfiguredLotAndExpiry()
    {
        var noLot = InventoryTrackingPolicy.Validate(true, false, null, null);
        var noExpiry = InventoryTrackingPolicy.Validate(
            true,
            true,
            LotNumber.Create(" LOT-01 ").Value,
            null);

        Assert.Equal("inventory.lot.required", noLot.Error.Code);
        Assert.Equal("inventory.expiry.required", noExpiry.Error.Code);
        Assert.Equal("LOT-01", LotNumber.Create(" LOT-01 ").Value.Value);
    }

    [Fact]
    public void ExpiryUsesInjectedBusinessDateBoundary()
    {
        var date = new ExpiryDate(new DateOnly(2026, 8, 4));

        Assert.False(date.IsExpired(new DateOnly(2026, 8, 4)));
        Assert.True(date.IsExpired(new DateOnly(2026, 8, 5)));
    }

    [Fact]
    public void MovementDirectionsAreExplicitAndExhaustive()
    {
        Assert.True(StockMovementType.Opening.IsIncrease());
        Assert.True(StockMovementType.InventoryIncrease.IsIncrease());
        Assert.True(StockMovementType.PurchaseReceipt.IsIncrease());
        Assert.True(StockMovementType.ManualDecrease.IsDecrease());
        Assert.True(StockMovementType.InventoryDecrease.IsDecrease());
        Assert.True(StockMovementType.Sale.IsDecrease());
        Assert.False(StockMovementType.ManualIncrease.IsDecrease());
        Assert.Equal(7, Enum.GetValues<StockMovementType>().Length);
    }

    [Fact]
    public async Task ApplicationRejectsInvalidMovementBeforeCallingStore()
    {
        var store = new RecordingStore();
        var handler = new InventoryCommandHandlers(store, new FixedDateProvider());
        var result = await handler.IncreaseAsync(
            new RecordStockMovementCommand(
                Guid.NewGuid(), StockMovementType.ManualIncrease, 1, 1, null, 0,
                "Test", "UnitTest"),
            TestCancellation);

        Assert.True(result.IsFailure);
        Assert.Equal("inventory.quantity.invalid", result.Error.Code);
        Assert.Equal(0, store.MovementCalls);
    }

    [Fact]
    public async Task ApplicationNormalizesMovementAndUsesInjectedDate()
    {
        var store = new RecordingStore();
        var handler = new InventoryCommandHandlers(store, new FixedDateProvider());
        var result = await handler.IncreaseAsync(
            new RecordStockMovementCommand(
                Guid.NewGuid(), StockMovementType.Opening, 5, 2, null, 3,
                "  Correction contrôlée  ", "  UnitTest  ", "  REF-1  "),
            TestCancellation);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, store.MovementCalls);
        Assert.Equal(StockMovementType.ManualIncrease, store.LastMovement!.MovementType);
        Assert.Equal("Correction contrôlée", store.LastMovement.Reason);
        Assert.Equal("UnitTest", store.LastMovement.SourceType);
        Assert.Equal("REF-1", store.LastMovement.SourceReference);
        Assert.Equal(new DateOnly(2026, 8, 4), store.LastDate);
    }

    [Fact]
    public void StableErrorCodesRemainUnique()
    {
        var codes = typeof(InventoryErrors).GetProperties()
            .Select(property => Assert.IsType<Error>(property.GetValue(null)))
            .Select(error => error.Code)
            .ToArray();

        Assert.Equal(16, codes.Length);
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.StartsWith("inventory.", code, StringComparison.Ordinal));
    }

    private sealed class FixedDateProvider : IInventoryDateProvider
    {
        public DateOnly CurrentDate => new(2026, 8, 4);
    }

    private sealed class RecordingStore : IInventoryStore
    {
        public int MovementCalls { get; private set; }
        public RecordStockMovementCommand? LastMovement { get; private set; }
        public DateOnly LastDate { get; private set; }

        public Task<Result<StockMovementView>> RecordMovementAsync(
            RecordStockMovementCommand command,
            DateOnly currentDate,
            CancellationToken cancellationToken = default)
        {
            MovementCalls++;
            LastMovement = command;
            LastDate = currentDate;
            return Task.FromResult(Result<StockMovementView>.Success(new StockMovementView(
                1, command.OperationId, command.ProductId, "TEST-001", "Test",
                command.LocationId, "TEST", command.BatchId, null, command.MovementType,
                command.Quantity, 0, command.Quantity, command.Reason, command.SourceType,
                command.SourceReference, DateTime.UtcNow)));
        }

        public Task<Result<StockLocationView>> CreateLocationAsync(CreateStockLocationCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<StockLocationView>> SetLocationActivationAsync(SetStockLocationActivationCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<StockBatchView>> CreateBatchAsync(CreateStockBatchCommand command, DateOnly currentDate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<InventorySessionView>> CreateSessionAsync(CreateInventorySessionCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<InventorySessionView>> StartSessionAsync(StartInventorySessionCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<InventorySessionView>> SaveCountLineAsync(SaveInventoryCountLineCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<InventorySessionView>> FinalizeSessionAsync(FinalizeInventorySessionCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<InventorySessionView>> CancelSessionAsync(CancelInventorySessionCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
