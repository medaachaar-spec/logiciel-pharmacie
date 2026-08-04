using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Inventory.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;

namespace Pharmacy.Application.Inventory.Commands;

public sealed class InventoryCommandHandlers
{
    private readonly IInventoryStore store;
    private readonly IInventoryDateProvider dateProvider;

    public InventoryCommandHandlers(IInventoryStore store, IInventoryDateProvider dateProvider)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.dateProvider = dateProvider ?? throw new ArgumentNullException(nameof(dateProvider));
    }

    public Task<Result<StockLocationView>> CreateLocationAsync(
        CreateStockLocationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return !ValidOperation(command.OperationId)
            || !ValidText(command.Code, 64)
            || !ValidText(command.Name, 160)
            ? Task.FromResult(Result<StockLocationView>.Failure(InventoryErrors.InvalidOperation))
            : store.CreateLocationAsync(
                command with { Code = command.Code.Trim(), Name = command.Name.Trim() },
                cancellationToken);
    }

    public Task<Result<StockLocationView>> SetLocationActivationAsync(
        SetStockLocationActivationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return !ValidOperation(command.OperationId) || command.LocationId <= 0 || !ValidToken(command.RowVersion)
            ? Task.FromResult(Result<StockLocationView>.Failure(InventoryErrors.InvalidOperation))
            : store.SetLocationActivationAsync(command, cancellationToken);
    }

    public Task<Result<StockBatchView>> CreateBatchAsync(
        CreateStockBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return !ValidOperation(command.OperationId) || command.ProductId <= 0
            || (command.LotNumber is not null && !ValidText(command.LotNumber, LotNumber.MaxLength))
            ? Task.FromResult(Result<StockBatchView>.Failure(InventoryErrors.InvalidOperation))
            : store.CreateBatchAsync(
                command with { LotNumber = command.LotNumber?.Trim() },
                dateProvider.CurrentDate,
                cancellationToken);
    }

    public Task<Result<StockMovementView>> RecordOpeningAsync(
        RecordStockMovementCommand command,
        CancellationToken cancellationToken = default) =>
        RecordMovementAsync(command with { MovementType = StockMovementType.Opening }, cancellationToken);

    public Task<Result<StockMovementView>> IncreaseAsync(
        RecordStockMovementCommand command,
        CancellationToken cancellationToken = default) =>
        RecordMovementAsync(command with { MovementType = StockMovementType.ManualIncrease }, cancellationToken);

    public Task<Result<StockMovementView>> DecreaseAsync(
        RecordStockMovementCommand command,
        CancellationToken cancellationToken = default) =>
        RecordMovementAsync(command with { MovementType = StockMovementType.ManualDecrease }, cancellationToken);

    public Task<Result<StockMovementView>> RecordMovementAsync(
        RecordStockMovementCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var quantity = StockMovementQuantity.Create(command.Quantity);
        var supported = command.MovementType is StockMovementType.Opening
            or StockMovementType.ManualIncrease
            or StockMovementType.ManualDecrease;
        return !ValidOperation(command.OperationId) || !supported || command.ProductId <= 0
            || command.LocationId <= 0 || command.BatchId <= 0 || quantity.IsFailure
            || !ValidText(command.Reason, 256) || !ValidText(command.SourceType, 32)
            || (command.SourceReference is not null && command.SourceReference.Trim().Length > 128)
            || (command.ExpectedBalanceRowVersion is not null && !ValidToken(command.ExpectedBalanceRowVersion))
            ? Task.FromResult(Result<StockMovementView>.Failure(
                quantity.IsFailure ? quantity.Error : InventoryErrors.InvalidOperation))
            : store.RecordMovementAsync(
                command with
                {
                    Reason = command.Reason.Trim(),
                    SourceType = command.SourceType.Trim(),
                    SourceReference = string.IsNullOrWhiteSpace(command.SourceReference) ? null : command.SourceReference.Trim()
                },
                dateProvider.CurrentDate,
                cancellationToken);
    }

    public Task<Result<InventorySessionView>> CreateSessionAsync(
        CreateInventorySessionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return !ValidOperation(command.OperationId) || !ValidText(command.Reference, 64)
            || command.LocationId <= 0 || command.Notes?.Trim().Length > 512
            ? Task.FromResult(Result<InventorySessionView>.Failure(InventoryErrors.InvalidOperation))
            : store.CreateSessionAsync(
                command with { Reference = command.Reference.Trim(), Notes = Normalize(command.Notes) },
                cancellationToken);
    }

    public Task<Result<InventorySessionView>> StartSessionAsync(
        StartInventorySessionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return !ValidSessionMutation(command.OperationId, command.SessionId, command.SessionRowVersion)
            ? Task.FromResult(Result<InventorySessionView>.Failure(InventoryErrors.InvalidOperation))
            : store.StartSessionAsync(command, cancellationToken);
    }

    public Task<Result<InventorySessionView>> SaveCountLineAsync(
        SaveInventoryCountLineCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return !ValidSessionMutation(command.OperationId, command.SessionId, command.SessionRowVersion)
            || command.CountLineId <= 0 || command.CountedQuantity < 0 || !ValidToken(command.LineRowVersion)
            ? Task.FromResult(Result<InventorySessionView>.Failure(InventoryErrors.InvalidQuantity))
            : store.SaveCountLineAsync(command, cancellationToken);
    }

    public Task<Result<InventorySessionView>> FinalizeSessionAsync(
        FinalizeInventorySessionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return !ValidSessionMutation(command.OperationId, command.SessionId, command.SessionRowVersion)
            ? Task.FromResult(Result<InventorySessionView>.Failure(InventoryErrors.InvalidOperation))
            : store.FinalizeSessionAsync(command, cancellationToken);
    }

    public Task<Result<InventorySessionView>> CancelSessionAsync(
        CancelInventorySessionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return !ValidSessionMutation(command.OperationId, command.SessionId, command.SessionRowVersion)
            ? Task.FromResult(Result<InventorySessionView>.Failure(InventoryErrors.InvalidOperation))
            : store.CancelSessionAsync(command, cancellationToken);
    }

    private static bool ValidSessionMutation(Guid operationId, long sessionId, string rowVersion) =>
        ValidOperation(operationId) && sessionId > 0 && ValidToken(rowVersion);

    private static bool ValidOperation(Guid operationId) => operationId != Guid.Empty;

    private static bool ValidText(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool ValidToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(token).Length == 8;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
