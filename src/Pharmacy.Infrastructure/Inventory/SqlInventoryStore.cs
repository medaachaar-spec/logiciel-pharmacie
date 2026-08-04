using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Inventory.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;

namespace Pharmacy.Infrastructure.Inventory;

public sealed class SqlInventoryStore : IInventoryStore
{
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;

    public SqlInventoryStore(IDbContextFactory<PharmacyDbContext> contextFactory)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<Result<StockLocationView>> CreateLocationAsync(
        CreateStockLocationCommand command,
        CancellationToken cancellationToken = default)
    {
        var fingerprint = Fingerprint("location.create", command.Code, command.Name, command.IsPrimary);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var previous = await context.StockLocations.SingleOrDefaultAsync(
                location => location.CreationOperationId == command.OperationId,
                cancellationToken);

            if (previous is not null)
            {
                return SameFingerprint(previous.CreationFingerprint, fingerprint)
                    ? Result<StockLocationView>.Success(SqlInventoryReader.MapLocation(previous))
                    : Result<StockLocationView>.Failure(InventoryErrors.OperationConflict);
            }

            if (await context.StockLocations.AnyAsync(location => location.Code == command.Code, cancellationToken))
            {
                return Result<StockLocationView>.Failure(InventoryErrors.OperationConflict);
            }

            var location = new StockLocationEntity
            {
                CreationOperationId = command.OperationId,
                CreationFingerprint = fingerprint,
                Code = command.Code,
                Name = command.Name,
                IsActive = true,
                IsPrimary = command.IsPrimary,
                CreatedAtUtc = DateTime.UtcNow
            };
            context.StockLocations.Add(location);
            await context.SaveChangesAsync(cancellationToken);
            return Result<StockLocationView>.Success(SqlInventoryReader.MapLocation(location));
        }
        catch (DbUpdateException exception) when (InventorySqlFailure.IsUniqueViolation(exception))
        {
            return Result<StockLocationView>.Failure(InventoryErrors.OperationConflict);
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<StockLocationView>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<StockLocationView>> SetLocationActivationAsync(
        SetStockLocationActivationCommand command,
        CancellationToken cancellationToken = default)
    {
        var fingerprint = Fingerprint("location.activation", command.LocationId, command.IsActive);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var previous = await context.StockLocations.SingleOrDefaultAsync(
                location => location.LastOperationId == command.OperationId,
                cancellationToken);

            if (previous is not null)
            {
                return SameFingerprint(previous.LastOperationFingerprint, fingerprint)
                    ? Result<StockLocationView>.Success(SqlInventoryReader.MapLocation(previous))
                    : Result<StockLocationView>.Failure(InventoryErrors.OperationConflict);
            }

            var location = await context.StockLocations.SingleOrDefaultAsync(
                candidate => candidate.Id == command.LocationId,
                cancellationToken);

            if (location is null)
            {
                return Result<StockLocationView>.Failure(InventoryErrors.InvalidLocationId);
            }

            context.Entry(location).Property(entity => entity.RowVersion).OriginalValue = Decode(command.RowVersion);
            location.IsActive = command.IsActive;
            location.LastOperationId = command.OperationId;
            location.LastOperationFingerprint = fingerprint;
            await context.SaveChangesAsync(cancellationToken);
            return Result<StockLocationView>.Success(SqlInventoryReader.MapLocation(location));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<StockLocationView>.Failure(InventoryErrors.ConcurrencyConflict);
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<StockLocationView>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<StockBatchView>> CreateBatchAsync(
        CreateStockBatchCommand command,
        DateOnly currentDate,
        CancellationToken cancellationToken = default)
    {
        var fingerprint = Fingerprint("batch.create", command.ProductId, command.LotNumber, command.ExpiryDate, command.IsActive);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var previous = await context.StockBatches.SingleOrDefaultAsync(
                batch => batch.CreationOperationId == command.OperationId,
                cancellationToken);

            if (previous is not null)
            {
                return SameFingerprint(previous.CreationFingerprint, fingerprint)
                    ? Result<StockBatchView>.Success(MapBatchDefinition(previous, currentDate))
                    : Result<StockBatchView>.Failure(InventoryErrors.OperationConflict);
            }

            var settings = await context.ProductLocalSettings.AsNoTracking().SingleOrDefaultAsync(
                candidate => candidate.ProductId == command.ProductId,
                cancellationToken);

            if (settings is null)
            {
                return Result<StockBatchView>.Failure(InventoryErrors.ProductNotFound);
            }

            var lot = command.LotNumber is null ? null : LotNumber.Create(command.LotNumber).Value;
            var validation = InventoryTrackingPolicy.Validate(
                settings.TracksLots,
                settings.TracksExpiry,
                lot,
                command.ExpiryDate is null ? null : new ExpiryDate(command.ExpiryDate.Value));

            if (validation.IsFailure)
            {
                return Result<StockBatchView>.Failure(validation.Error);
            }

            if (command.LotNumber is not null && await context.StockBatches.AnyAsync(
                batch => batch.ProductId == command.ProductId && batch.BatchNumber == command.LotNumber,
                cancellationToken))
            {
                return Result<StockBatchView>.Failure(InventoryErrors.OperationConflict);
            }

            var entity = new StockBatchEntity
            {
                CreationOperationId = command.OperationId,
                CreationFingerprint = fingerprint,
                ProductId = command.ProductId,
                BatchNumber = command.LotNumber,
                ExpiryDate = command.ExpiryDate,
                IsActive = command.IsActive,
                CreatedAtUtc = DateTime.UtcNow
            };
            context.StockBatches.Add(entity);
            await context.SaveChangesAsync(cancellationToken);
            return Result<StockBatchView>.Success(MapBatchDefinition(entity, currentDate));
        }
        catch (DbUpdateException exception) when (InventorySqlFailure.IsUniqueViolation(exception))
        {
            return Result<StockBatchView>.Failure(InventoryErrors.OperationConflict);
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<StockBatchView>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<StockMovementView>> RecordMovementAsync(
        RecordStockMovementCommand command,
        DateOnly currentDate,
        CancellationToken cancellationToken = default)
    {
        var fingerprint = MovementFingerprint(command);

        try
        {
            await using var strategyContext = await contextFactory.CreateDbContextAsync(cancellationToken);
            var strategy = strategyContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                var previous = await MovementQuery(context).SingleOrDefaultAsync(
                    movement => movement.OperationId == command.OperationId,
                    cancellationToken);

                if (previous is not null)
                {
                    return SameFingerprint(previous.OperationFingerprint, fingerprint)
                        ? Result<StockMovementView>.Success(MapMovement(previous))
                        : Result<StockMovementView>.Failure(InventoryErrors.OperationConflict);
                }

                var settings = await context.ProductLocalSettings
                    .Include(candidate => candidate.Product)
                    .SingleOrDefaultAsync(candidate => candidate.ProductId == command.ProductId, cancellationToken);

                if (settings is null)
                {
                    return Result<StockMovementView>.Failure(InventoryErrors.ProductNotFound);
                }

                var location = await context.StockLocations.SingleOrDefaultAsync(
                    candidate => candidate.Id == command.LocationId,
                    cancellationToken);

                if (location is null)
                {
                    return Result<StockMovementView>.Failure(InventoryErrors.InvalidLocationId);
                }

                if (!location.IsActive)
                {
                    return Result<StockMovementView>.Failure(InventoryErrors.LocationInactive);
                }

                StockBatchEntity? batch = null;

                if (command.BatchId is not null)
                {
                    batch = await context.StockBatches.SingleOrDefaultAsync(
                        candidate => candidate.Id == command.BatchId.Value && candidate.ProductId == command.ProductId,
                        cancellationToken);

                    if (batch is null)
                    {
                        return Result<StockMovementView>.Failure(InventoryErrors.BatchNotFound);
                    }
                }

                var tracking = InventoryTrackingPolicy.Validate(
                    settings.TracksLots,
                    settings.TracksExpiry,
                    batch?.BatchNumber is null ? null : LotNumber.Create(batch.BatchNumber).Value,
                    batch?.ExpiryDate is null ? null : new ExpiryDate(batch.ExpiryDate.Value));

                if (tracking.IsFailure)
                {
                    return Result<StockMovementView>.Failure(tracking.Error);
                }

                var balance = await context.StockBalances.SingleOrDefaultAsync(
                    candidate => candidate.StockLocationId == command.LocationId
                        && candidate.ProductId == command.ProductId
                        && candidate.StockBatchId == command.BatchId,
                    cancellationToken);

                if (command.MovementType == StockMovementType.Opening && balance?.AvailableQuantity > 0)
                {
                    return Result<StockMovementView>.Failure(InventoryErrors.InvalidOperation);
                }

                if (balance is null && command.MovementType.IsDecrease())
                {
                    return Result<StockMovementView>.Failure(InventoryErrors.InsufficientStock);
                }

                balance ??= new StockBalanceEntity
                {
                    StockLocationId = command.LocationId,
                    ProductId = command.ProductId,
                    StockBatchId = command.BatchId,
                    AvailableQuantity = 0,
                    UpdatedAtUtc = DateTime.UtcNow,
                    StockLocation = location,
                    Product = settings.Product,
                    StockBatch = batch
                };

                if (balance.Id == 0)
                {
                    context.StockBalances.Add(balance);
                }
                else if (command.ExpectedBalanceRowVersion is not null)
                {
                    context.Entry(balance).Property(entity => entity.RowVersion).OriginalValue = Decode(command.ExpectedBalanceRowVersion);
                }

                var state = StockBalanceState.Create(balance.AvailableQuantity).Value;
                var quantity = StockMovementQuantity.Create(command.Quantity).Value;
                var changed = state.Apply(command.MovementType, quantity);

                if (changed.IsFailure)
                {
                    return Result<StockMovementView>.Failure(changed.Error);
                }

                var now = DateTime.UtcNow;
                var movement = new StockMovementEntity
                {
                    OperationId = command.OperationId,
                    OperationFingerprint = fingerprint,
                    ProductId = command.ProductId,
                    StockLocationId = command.LocationId,
                    StockBatchId = command.BatchId,
                    MovementType = command.MovementType,
                    Quantity = command.Quantity,
                    QuantityBefore = balance.AvailableQuantity,
                    QuantityAfter = changed.Value.Quantity.Value,
                    Reason = command.Reason,
                    SourceType = command.SourceType,
                    SourceReference = command.SourceReference,
                    OccurredAtUtc = now,
                    CreatedAtUtc = now,
                    Product = settings.Product,
                    StockLocation = location,
                    StockBatch = batch
                };
                balance.AvailableQuantity = movement.QuantityAfter;
                balance.UpdatedAtUtc = now;
                context.StockMovements.Add(movement);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result<StockMovementView>.Success(MapMovement(movement));
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<StockMovementView>.Failure(InventoryErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (InventorySqlFailure.IsUniqueViolation(exception))
        {
            return await ResolveMovementRaceAsync(command.OperationId, fingerprint, cancellationToken);
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<StockMovementView>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<InventorySessionView>> CreateSessionAsync(
        CreateInventorySessionCommand command,
        CancellationToken cancellationToken = default)
    {
        var fingerprint = Fingerprint("session.create", command.Reference, command.LocationId, command.Notes);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var previous = await SessionQuery(context).SingleOrDefaultAsync(
                session => session.CreationOperationId == command.OperationId,
                cancellationToken);

            if (previous is not null)
            {
                return SameFingerprint(previous.CreationFingerprint, fingerprint)
                    ? Result<InventorySessionView>.Success(SqlInventoryReader.MapSession(previous))
                    : Result<InventorySessionView>.Failure(InventoryErrors.OperationConflict);
            }

            var location = await context.StockLocations.SingleOrDefaultAsync(
                candidate => candidate.Id == command.LocationId,
                cancellationToken);

            if (location is null)
            {
                return Result<InventorySessionView>.Failure(InventoryErrors.InvalidLocationId);
            }

            if (!location.IsActive)
            {
                return Result<InventorySessionView>.Failure(InventoryErrors.LocationInactive);
            }

            if (await context.InventorySessions.AnyAsync(session => session.Reference == command.Reference, cancellationToken))
            {
                return Result<InventorySessionView>.Failure(InventoryErrors.OperationConflict);
            }

            var session = new InventorySessionEntity
            {
                CreationOperationId = command.OperationId,
                CreationFingerprint = fingerprint,
                Reference = command.Reference,
                StockLocationId = command.LocationId,
                StockLocation = location,
                Status = InventorySessionStatus.Draft,
                StartedAtUtc = DateTime.UtcNow,
                Notes = command.Notes
            };
            context.InventorySessions.Add(session);
            await context.SaveChangesAsync(cancellationToken);
            return Result<InventorySessionView>.Success(SqlInventoryReader.MapSession(session));
        }
        catch (DbUpdateException exception) when (InventorySqlFailure.IsUniqueViolation(exception))
        {
            return Result<InventorySessionView>.Failure(InventoryErrors.OperationConflict);
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<InventorySessionView>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<InventorySessionView>> StartSessionAsync(
        StartInventorySessionCommand command,
        CancellationToken cancellationToken = default)
    {
        var fingerprint = Fingerprint("session.start", command.SessionId);

        try
        {
            await using var strategyContext = await contextFactory.CreateDbContextAsync(cancellationToken);
            var strategy = strategyContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                var session = await SessionQuery(context).SingleOrDefaultAsync(
                    candidate => candidate.Id == command.SessionId,
                    cancellationToken);

                if (session is null)
                {
                    return Result<InventorySessionView>.Failure(InventoryErrors.SessionNotFound);
                }

                if (session.StartOperationId == command.OperationId)
                {
                    return SameFingerprint(session.StartOperationFingerprint, fingerprint)
                        ? Result<InventorySessionView>.Success(SqlInventoryReader.MapSession(session))
                        : Result<InventorySessionView>.Failure(InventoryErrors.OperationConflict);
                }

                if (session.Status != InventorySessionStatus.Draft)
                {
                    return Result<InventorySessionView>.Failure(InventoryErrors.SessionInvalidStatus);
                }

                context.Entry(session).Property(entity => entity.RowVersion).OriginalValue = Decode(command.SessionRowVersion);
                var balances = await context.StockBalances
                    .Include(balance => balance.Product).ThenInclude(product => product.LocalSettings)
                    .Include(balance => balance.StockBatch)
                    .Where(balance => balance.StockLocationId == session.StockLocationId)
                    .ToArrayAsync(cancellationToken);

                foreach (var balance in balances)
                {
                    session.Lines.Add(new InventoryCountLineEntity
                    {
                        ProductId = balance.ProductId,
                        Product = balance.Product,
                        StockBatchId = balance.StockBatchId,
                        StockBatch = balance.StockBatch,
                        ExpectedQuantity = balance.AvailableQuantity
                    });
                }

                session.Status = InventorySessionStatus.InProgress;
                session.StartedAtUtc = DateTime.UtcNow;
                session.StartOperationId = command.OperationId;
                session.StartOperationFingerprint = fingerprint;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result<InventorySessionView>.Success(SqlInventoryReader.MapSession(session));
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<InventorySessionView>.Failure(InventoryErrors.ConcurrencyConflict);
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<InventorySessionView>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<InventorySessionView>> SaveCountLineAsync(
        SaveInventoryCountLineCommand command,
        CancellationToken cancellationToken = default)
    {
        var fingerprint = Fingerprint("session.count", command.SessionId, command.CountLineId, command.CountedQuantity);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var session = await SessionQuery(context).SingleOrDefaultAsync(
                candidate => candidate.Id == command.SessionId,
                cancellationToken);

            if (session is null)
            {
                return Result<InventorySessionView>.Failure(InventoryErrors.SessionNotFound);
            }

            var line = session.Lines.SingleOrDefault(candidate => candidate.Id == command.CountLineId);

            if (line is null)
            {
                return Result<InventorySessionView>.Failure(InventoryErrors.InvalidOperation);
            }

            if (line.LastOperationId == command.OperationId)
            {
                return SameFingerprint(line.LastOperationFingerprint, fingerprint)
                    ? Result<InventorySessionView>.Success(SqlInventoryReader.MapSession(session))
                    : Result<InventorySessionView>.Failure(InventoryErrors.OperationConflict);
            }

            if (session.Status != InventorySessionStatus.InProgress)
            {
                return Result<InventorySessionView>.Failure(
                    session.Status == InventorySessionStatus.Completed
                        ? InventoryErrors.SessionAlreadyCompleted
                        : InventoryErrors.SessionInvalidStatus);
            }

            context.Entry(session).Property(entity => entity.RowVersion).OriginalValue = Decode(command.SessionRowVersion);
            context.Entry(line).Property(entity => entity.RowVersion).OriginalValue = Decode(command.LineRowVersion);
            line.CountedQuantity = command.CountedQuantity;
            line.LastOperationId = command.OperationId;
            line.LastOperationFingerprint = fingerprint;
            context.Entry(session).Property(entity => entity.Status).IsModified = true;
            await context.SaveChangesAsync(cancellationToken);
            return Result<InventorySessionView>.Success(SqlInventoryReader.MapSession(session));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<InventorySessionView>.Failure(InventoryErrors.ConcurrencyConflict);
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<InventorySessionView>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<InventorySessionView>> FinalizeSessionAsync(
        FinalizeInventorySessionCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var strategyContext = await contextFactory.CreateDbContextAsync(cancellationToken);
            var strategy = strategyContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                var session = await SessionQuery(context).SingleOrDefaultAsync(
                    candidate => candidate.Id == command.SessionId,
                    cancellationToken);

                if (session is null)
                {
                    return Result<InventorySessionView>.Failure(InventoryErrors.SessionNotFound);
                }

                var fingerprint = FinalizationFingerprint(session);

                if (session.Status == InventorySessionStatus.Completed)
                {
                    return session.FinalizationOperationId == command.OperationId
                        && SameFingerprint(session.FinalizationFingerprint, fingerprint)
                        ? Result<InventorySessionView>.Success(SqlInventoryReader.MapSession(session))
                        : Result<InventorySessionView>.Failure(InventoryErrors.SessionAlreadyCompleted);
                }

                if (session.Status != InventorySessionStatus.InProgress || session.Lines.Any(line => line.CountedQuantity is null))
                {
                    return Result<InventorySessionView>.Failure(InventoryErrors.SessionInvalidStatus);
                }

                context.Entry(session).Property(entity => entity.RowVersion).OriginalValue = Decode(command.SessionRowVersion);
                var balances = await context.StockBalances
                    .Include(balance => balance.Product).ThenInclude(product => product.LocalSettings)
                    .Include(balance => balance.StockLocation)
                    .Include(balance => balance.StockBatch)
                    .Where(balance => balance.StockLocationId == session.StockLocationId)
                    .ToArrayAsync(cancellationToken);
                var now = DateTime.UtcNow;

                foreach (var line in session.Lines)
                {
                    var balance = balances.SingleOrDefault(candidate => candidate.ProductId == line.ProductId
                        && candidate.StockBatchId == line.StockBatchId);

                    if (balance is null || balance.AvailableQuantity != line.ExpectedQuantity)
                    {
                        return Result<InventorySessionView>.Failure(InventoryErrors.ConcurrencyConflict);
                    }

                    var difference = line.CountedQuantity!.Value - line.ExpectedQuantity;

                    if (difference == 0)
                    {
                        continue;
                    }

                    var movementType = difference > 0
                        ? StockMovementType.InventoryIncrease
                        : StockMovementType.InventoryDecrease;
                    var quantity = Math.Abs(difference);
                    var operationId = DeterministicOperationId(command.OperationId, line.Id);
                    var movementCommand = new RecordStockMovementCommand(
                        operationId,
                        movementType,
                        line.ProductId,
                        session.StockLocationId,
                        line.StockBatchId,
                        quantity,
                        "Écart de comptage inventaire",
                        "InventorySession",
                        session.Reference);
                    context.StockMovements.Add(new StockMovementEntity
                    {
                        OperationId = operationId,
                        OperationFingerprint = MovementFingerprint(movementCommand),
                        ProductId = line.ProductId,
                        StockLocationId = session.StockLocationId,
                        StockBatchId = line.StockBatchId,
                        MovementType = movementType,
                        Quantity = quantity,
                        QuantityBefore = balance.AvailableQuantity,
                        QuantityAfter = line.CountedQuantity.Value,
                        Reason = movementCommand.Reason,
                        SourceType = movementCommand.SourceType,
                        SourceReference = movementCommand.SourceReference,
                        OccurredAtUtc = now,
                        CreatedAtUtc = now,
                        Product = balance.Product,
                        StockLocation = balance.StockLocation,
                        StockBatch = balance.StockBatch
                    });
                    balance.AvailableQuantity = line.CountedQuantity.Value;
                    balance.UpdatedAtUtc = now;
                }

                session.Status = InventorySessionStatus.Completed;
                session.CompletedAtUtc = now;
                session.FinalizationOperationId = command.OperationId;
                session.FinalizationFingerprint = fingerprint;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result<InventorySessionView>.Success(SqlInventoryReader.MapSession(session));
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<InventorySessionView>.Failure(InventoryErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (InventorySqlFailure.IsUniqueViolation(exception))
        {
            return Result<InventorySessionView>.Failure(InventoryErrors.OperationConflict);
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<InventorySessionView>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<InventorySessionView>> CancelSessionAsync(
        CancelInventorySessionCommand command,
        CancellationToken cancellationToken = default)
    {
        var fingerprint = Fingerprint("session.cancel", command.SessionId);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var session = await SessionQuery(context).SingleOrDefaultAsync(
                candidate => candidate.Id == command.SessionId,
                cancellationToken);

            if (session is null)
            {
                return Result<InventorySessionView>.Failure(InventoryErrors.SessionNotFound);
            }

            if (session.CancellationOperationId == command.OperationId)
            {
                return SameFingerprint(session.CancellationFingerprint, fingerprint)
                    ? Result<InventorySessionView>.Success(SqlInventoryReader.MapSession(session))
                    : Result<InventorySessionView>.Failure(InventoryErrors.OperationConflict);
            }

            if (session.Status is InventorySessionStatus.Completed or InventorySessionStatus.Cancelled)
            {
                return Result<InventorySessionView>.Failure(
                    session.Status == InventorySessionStatus.Completed
                        ? InventoryErrors.SessionAlreadyCompleted
                        : InventoryErrors.SessionInvalidStatus);
            }

            context.Entry(session).Property(entity => entity.RowVersion).OriginalValue = Decode(command.SessionRowVersion);
            session.Status = InventorySessionStatus.Cancelled;
            session.CompletedAtUtc = DateTime.UtcNow;
            session.CancellationOperationId = command.OperationId;
            session.CancellationFingerprint = fingerprint;
            await context.SaveChangesAsync(cancellationToken);
            return Result<InventorySessionView>.Success(SqlInventoryReader.MapSession(session));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<InventorySessionView>.Failure(InventoryErrors.ConcurrencyConflict);
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<InventorySessionView>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    private async Task<Result<StockMovementView>> ResolveMovementRaceAsync(
        Guid operationId,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var movement = await MovementQuery(context).SingleOrDefaultAsync(
            candidate => candidate.OperationId == operationId,
            cancellationToken);
        return movement is not null && SameFingerprint(movement.OperationFingerprint, fingerprint)
            ? Result<StockMovementView>.Success(MapMovement(movement))
            : Result<StockMovementView>.Failure(InventoryErrors.OperationConflict);
    }

    private static IQueryable<StockMovementEntity> MovementQuery(PharmacyDbContext context) =>
        context.StockMovements
            .Include(movement => movement.Product).ThenInclude(product => product.LocalSettings)
            .Include(movement => movement.StockLocation)
            .Include(movement => movement.StockBatch);

    private static IQueryable<InventorySessionEntity> SessionQuery(PharmacyDbContext context) =>
        context.InventorySessions.AsSplitQuery()
            .Include(session => session.StockLocation)
            .Include(session => session.Lines).ThenInclude(line => line.Product).ThenInclude(product => product.LocalSettings)
            .Include(session => session.Lines).ThenInclude(line => line.StockBatch);

    private static StockMovementView MapMovement(StockMovementEntity movement) => new(
        movement.Id,
        movement.OperationId,
        movement.ProductId,
        movement.Product.LocalSettings?.InternalCode ?? string.Empty,
        movement.Product.Name,
        movement.StockLocationId,
        movement.StockLocation.Code,
        movement.StockBatchId,
        movement.StockBatch?.BatchNumber,
        movement.MovementType,
        movement.Quantity,
        movement.QuantityBefore,
        movement.QuantityAfter,
        movement.Reason,
        movement.SourceType,
        movement.SourceReference,
        movement.OccurredAtUtc);

    private static StockBatchView MapBatchDefinition(StockBatchEntity batch, DateOnly currentDate) => new(
        0,
        batch.ProductId,
        0,
        string.Empty,
        string.Empty,
        batch.Id,
        batch.BatchNumber,
        batch.ExpiryDate,
        batch.IsActive,
        batch.ExpiryDate < currentDate,
        0,
        string.Empty,
        Convert.ToBase64String(batch.RowVersion));

    private static string FinalizationFingerprint(InventorySessionEntity session) =>
        Fingerprint(
            "session.finalize",
            session.Id,
            string.Join(
                ";",
                session.Lines.OrderBy(line => line.Id).Select(line =>
                    $"{line.Id}:{line.ProductId}:{line.StockBatchId?.ToString(CultureInfo.InvariantCulture) ?? "-"}:{line.ExpectedQuantity}:{line.CountedQuantity}")));

    private static string MovementFingerprint(RecordStockMovementCommand command) =>
        Fingerprint(
            "movement",
            command.MovementType,
            command.ProductId,
            command.LocationId,
            command.BatchId,
            command.Quantity,
            command.SourceType,
            command.SourceReference);

    private static string Fingerprint(params object?[] values) => string.Join(
        "|",
        values.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim().ToUpperInvariant() ?? "-"));

    private static Guid DeterministicOperationId(Guid operationId, long lineId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{operationId:N}:{lineId}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static byte[] Decode(string token) => Convert.FromBase64String(token);

    private static bool SameFingerprint(string? left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);
}
