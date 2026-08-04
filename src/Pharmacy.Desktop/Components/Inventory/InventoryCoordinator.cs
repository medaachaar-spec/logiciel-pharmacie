using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Models;
using Pharmacy.Application.Inventory.Queries;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;

namespace Pharmacy.Desktop.Components.Inventory;

public sealed class InventoryCoordinator
{
    private readonly InventoryQueryHandlers queries;
    private readonly InventoryCommandHandlers commands;
    private int operationInProgress;

    public InventoryCoordinator(InventoryQueryHandlers queries, InventoryCommandHandlers commands)
    {
        this.queries = queries ?? throw new ArgumentNullException(nameof(queries));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public IReadOnlyList<StockLocationView> Locations { get; private set; } = Array.Empty<StockLocationView>();
    public IReadOnlyList<StockOverviewItem> Products { get; private set; } = Array.Empty<StockOverviewItem>();
    public IReadOnlyList<StockBatchView> ExpiredBatches { get; private set; } = Array.Empty<StockBatchView>();
    public IReadOnlyList<StockMovementView> Movements { get; private set; } = Array.Empty<StockMovementView>();
    public IReadOnlyList<InventorySessionView> Sessions { get; private set; } = Array.Empty<InventorySessionView>();
    public ProductStockDetails? SelectedProduct { get; private set; }
    public InventorySessionView? SelectedSession { get; private set; }
    public InventoryDiagnosticsView? Diagnostics { get; private set; }
    public StockSearchCriteria Criteria { get; private set; } = new();
    public bool IsBusy => Volatile.Read(ref operationInProgress) != 0;
    public bool IsInitialized { get; private set; }
    public string? Message { get; private set; }
    public string? ErrorCode { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            Diagnostics = await queries.GetDiagnosticsAsync(cancellationToken);
            var locations = await queries.ListLocationsAsync(cancellationToken);
            var expired = await queries.GetExpiredBatchesAsync(cancellationToken);
            var sessions = await queries.ListSessionsAsync(cancellationToken: cancellationToken);

            if (!Apply(locations) || !Apply(expired) || !Apply(sessions))
            {
                return;
            }

            Locations = locations.Value;
            ExpiredBatches = expired.Value;
            Sessions = sessions.Value;
            await SearchCoreAsync(Criteria, cancellationToken);
            IsInitialized = ErrorCode is null;
        }
        finally
        {
            End();
        }
    }

    public async Task SearchAsync(StockSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            await SearchCoreAsync(criteria, cancellationToken);
        }
        finally
        {
            End();
        }
    }

    public async Task SelectProductAsync(long productId, long? locationId, CancellationToken cancellationToken = default)
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            var details = await queries.GetProductDetailsAsync(productId, locationId, cancellationToken);
            var movements = await queries.GetMovementsAsync(productId, locationId, cancellationToken: cancellationToken);

            if (Apply(details) && Apply(movements))
            {
                SelectedProduct = details.Value;
                Movements = movements.Value;
            }
        }
        finally
        {
            End();
        }
    }

    public async Task CreateBatchAsync(
        long productId,
        string? lotNumber,
        DateOnly? expiryDate,
        CancellationToken cancellationToken = default)
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            var result = await commands.CreateBatchAsync(
                new CreateStockBatchCommand(Guid.NewGuid(), productId, lotNumber, expiryDate),
                cancellationToken);

            if (!Apply(result))
            {
                return;
            }

            Message = "Lot créé. Une ouverture de stock explicite reste nécessaire.";
            await ReloadSelectedProductCoreAsync(cancellationToken);
        }
        finally
        {
            End();
        }
    }

    public async Task RecordMovementAsync(
        StockMovementType movementType,
        long productId,
        long locationId,
        long? batchId,
        int quantity,
        string reason,
        string? expectedBalanceRowVersion,
        CancellationToken cancellationToken = default)
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            var command = new RecordStockMovementCommand(
                Guid.NewGuid(),
                movementType,
                productId,
                locationId,
                batchId,
                quantity,
                reason,
                "Desktop",
                "StockManagementUI",
                expectedBalanceRowVersion);
            var result = movementType switch
            {
                StockMovementType.Opening => await commands.RecordOpeningAsync(command, cancellationToken),
                StockMovementType.ManualIncrease => await commands.IncreaseAsync(command, cancellationToken),
                StockMovementType.ManualDecrease => await commands.DecreaseAsync(command, cancellationToken),
                _ => Result<StockMovementView>.Failure(InventoryErrors.InvalidOperation)
            };

            if (!Apply(result))
            {
                return;
            }

            Message = $"Mouvement {result.Value.MovementType} enregistré : {result.Value.QuantityBefore} → {result.Value.QuantityAfter}.";
            await ReloadSelectedProductCoreAsync(cancellationToken);
            await SearchCoreAsync(Criteria, cancellationToken);
        }
        finally
        {
            End();
        }
    }

    public async Task CreateAndStartSessionAsync(
        string reference,
        long locationId,
        CancellationToken cancellationToken = default)
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            var created = await commands.CreateSessionAsync(
                new CreateInventorySessionCommand(Guid.NewGuid(), reference, locationId, "Créée depuis Desktop"),
                cancellationToken);

            if (!Apply(created))
            {
                return;
            }

            var started = await commands.StartSessionAsync(
                new StartInventorySessionCommand(Guid.NewGuid(), created.Value.SessionId, created.Value.RowVersion),
                cancellationToken);

            if (!Apply(started))
            {
                return;
            }

            SelectedSession = started.Value;
            Message = $"Inventaire {started.Value.Reference} démarré avec {started.Value.Lines.Count} ligne(s).";
            await ReloadSessionsCoreAsync(cancellationToken);
        }
        finally
        {
            End();
        }
    }

    public async Task SelectSessionAsync(long sessionId, CancellationToken cancellationToken = default)
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            var session = await queries.GetSessionAsync(sessionId, cancellationToken);

            if (Apply(session))
            {
                SelectedSession = session.Value;
            }
        }
        finally
        {
            End();
        }
    }

    public async Task SaveCountAsync(
        long countLineId,
        int countedQuantity,
        CancellationToken cancellationToken = default)
    {
        if (SelectedSession is null || !TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            var line = SelectedSession.Lines.SingleOrDefault(candidate => candidate.CountLineId == countLineId);

            if (line is null)
            {
                SetError(InventoryErrors.InvalidOperation);
                return;
            }

            var result = await commands.SaveCountLineAsync(
                new SaveInventoryCountLineCommand(
                    Guid.NewGuid(),
                    SelectedSession.SessionId,
                    countLineId,
                    countedQuantity,
                    SelectedSession.RowVersion,
                    line.RowVersion),
                cancellationToken);

            if (Apply(result))
            {
                SelectedSession = result.Value;
                Message = "Comptage enregistré.";
                await ReloadSessionsCoreAsync(cancellationToken);
            }
        }
        finally
        {
            End();
        }
    }

    public async Task FinalizeSessionAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSession is null || !TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            var result = await commands.FinalizeSessionAsync(
                new FinalizeInventorySessionCommand(Guid.NewGuid(), SelectedSession.SessionId, SelectedSession.RowVersion),
                cancellationToken);

            if (Apply(result))
            {
                SelectedSession = result.Value;
                Message = $"Inventaire {result.Value.Reference} finalisé atomiquement.";
                await ReloadSessionsCoreAsync(cancellationToken);
                await SearchCoreAsync(Criteria, cancellationToken);
            }
        }
        finally
        {
            End();
        }
    }

    public async Task CancelSessionAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSession is null || !TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            var result = await commands.CancelSessionAsync(
                new CancelInventorySessionCommand(Guid.NewGuid(), SelectedSession.SessionId, SelectedSession.RowVersion),
                cancellationToken);

            if (Apply(result))
            {
                SelectedSession = result.Value;
                Message = $"Inventaire {result.Value.Reference} annulé.";
                await ReloadSessionsCoreAsync(cancellationToken);
            }
        }
        finally
        {
            End();
        }
    }

    private async Task SearchCoreAsync(StockSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var result = await queries.SearchStockAsync(criteria, cancellationToken);

        if (Apply(result))
        {
            Criteria = criteria;
            Products = result.Value;
        }
    }

    private async Task ReloadSelectedProductCoreAsync(CancellationToken cancellationToken)
    {
        if (SelectedProduct is null)
        {
            return;
        }

        var details = await queries.GetProductDetailsAsync(SelectedProduct.ProductId, Criteria.LocationId, cancellationToken);
        var movements = await queries.GetMovementsAsync(
            SelectedProduct.ProductId,
            Criteria.LocationId,
            cancellationToken: cancellationToken);

        if (Apply(details) && Apply(movements))
        {
            SelectedProduct = details.Value;
            Movements = movements.Value;
        }
    }

    private async Task ReloadSessionsCoreAsync(CancellationToken cancellationToken)
    {
        var result = await queries.ListSessionsAsync(cancellationToken: cancellationToken);

        if (Apply(result))
        {
            Sessions = result.Value;
        }
    }

    private bool Apply<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return true;
        }

        SetError(result.Error);
        return false;
    }

    private void SetError(Error error)
    {
        ErrorCode = error.Code;
        Message = error.Code switch
        {
            "inventory.database.unavailable" => "Le Stock SQL est indisponible. Aucun stock synthétique de secours n’est utilisé.",
            "inventory.stock.insufficient" => "Stock insuffisant : aucune quantité et aucun mouvement n’ont été modifiés.",
            "inventory.concurrency_conflict" => "Les données ont changé. Rechargez la fiche avant de recommencer.",
            "inventory.session.invalid_status" => "L’état de cet inventaire ne permet pas cette action.",
            "inventory.session.already_completed" => "Cet inventaire est déjà finalisé ; aucune seconde finalisation n’a été effectuée.",
            _ => error.Message
        };
    }

    private void ClearFeedback()
    {
        Message = null;
        ErrorCode = null;
    }

    private bool TryBegin() => Interlocked.CompareExchange(ref operationInProgress, 1, 0) == 0;

    private void End() => Volatile.Write(ref operationInProgress, 0);
}
