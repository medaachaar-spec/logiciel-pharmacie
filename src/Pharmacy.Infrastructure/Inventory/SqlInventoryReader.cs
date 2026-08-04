using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Inventory.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;

namespace Pharmacy.Infrastructure.Inventory;

public sealed class SqlInventoryReader : IInventoryReader
{
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;

    public SqlInventoryReader(IDbContextFactory<PharmacyDbContext> contextFactory)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<Result<IReadOnlyList<StockLocationView>>> ListLocationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var rows = await context.StockLocations.AsNoTracking()
                .OrderByDescending(location => location.IsPrimary)
                .ThenBy(location => location.Code)
                .ToArrayAsync(cancellationToken);
            return Result<IReadOnlyList<StockLocationView>>.Success(
                Array.AsReadOnly(rows.Select(MapLocation).ToArray()));
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<IReadOnlyList<StockLocationView>>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<IReadOnlyList<StockOverviewItem>>> SearchStockAsync(
        StockSearchCriteria criteria,
        DateOnly currentDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.ProductLocalSettings.AsNoTracking().Where(settings => settings.InternalCode != null);
            var text = criteria.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(text))
            {
                query = query.Where(settings => settings.InternalCode!.Contains(text)
                    || settings.Product.Name.Contains(text)
                    || settings.Product.Barcodes.Any(barcode => barcode.IsActive && barcode.NormalizedValue.Contains(text)));
            }

            if (criteria.LocationId is not null)
            {
                query = query.Where(settings => settings.Product.StockBalances.Any(balance =>
                    balance.StockLocationId == criteria.LocationId.Value));
            }

            query = criteria.Availability switch
            {
                StockAvailabilityFilter.Available => query.Where(settings => settings.Product.StockBalances.Any(balance =>
                    balance.AvailableQuantity > 0
                    && balance.StockLocation.IsActive
                    && (balance.StockBatch == null || (balance.StockBatch.IsActive
                        && (balance.StockBatch.ExpiryDate == null || balance.StockBatch.ExpiryDate >= currentDate))))),
                StockAvailabilityFilter.OutOfStock => query.Where(settings => !settings.Product.StockBalances.Any(balance =>
                    balance.AvailableQuantity > 0
                    && balance.StockLocation.IsActive
                    && (balance.StockBatch == null || (balance.StockBatch.IsActive
                        && (balance.StockBatch.ExpiryDate == null || balance.StockBatch.ExpiryDate >= currentDate))))),
                StockAvailabilityFilter.Expired => query.Where(settings => settings.Product.StockBalances.Any(balance =>
                    balance.AvailableQuantity > 0 && balance.StockBatch != null
                    && balance.StockBatch.ExpiryDate != null && balance.StockBatch.ExpiryDate < currentDate)),
                _ => query
            };

            var rows = await query
                .OrderBy(settings => settings.Product.Name)
                .ThenBy(settings => settings.ProductId)
                .Take(criteria.Limit)
                .Select(settings => new StockOverviewItem(
                    settings.ProductId,
                    settings.InternalCode!,
                    settings.Product.Name,
                    settings.Product.StockBalances.Sum(balance => balance.AvailableQuantity),
                    settings.Product.IsActive && settings.IsActive && settings.IsSellable
                        ? settings.Product.StockBalances
                            .Where(balance => balance.AvailableQuantity > 0
                                && balance.StockLocation.IsActive
                                && (balance.StockBatch == null || (balance.StockBatch.IsActive
                                    && (balance.StockBatch.ExpiryDate == null || balance.StockBatch.ExpiryDate >= currentDate))))
                            .Sum(balance => balance.AvailableQuantity)
                        : 0,
                    settings.Product.StockBalances
                        .Where(balance => balance.AvailableQuantity > 0 && balance.StockBatch != null
                            && balance.StockBatch.ExpiryDate != null && balance.StockBatch.ExpiryDate < currentDate)
                        .Sum(balance => balance.AvailableQuantity),
                    settings.Product.StockBatches.Count,
                    settings.TracksLots,
                    settings.TracksExpiry))
                .ToArrayAsync(cancellationToken);

            return Result<IReadOnlyList<StockOverviewItem>>.Success(Array.AsReadOnly(rows));
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<IReadOnlyList<StockOverviewItem>>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<ProductStockDetails>> GetProductDetailsAsync(
        long productId,
        long? locationId,
        DateOnly currentDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var settings = await context.ProductLocalSettings.AsNoTracking()
                .Include(candidate => candidate.Product)
                .SingleOrDefaultAsync(candidate => candidate.ProductId == productId, cancellationToken);

            if (settings?.InternalCode is null)
            {
                return Result<ProductStockDetails>.Failure(InventoryErrors.ProductNotFound);
            }

            var balancesQuery = context.StockBalances.AsNoTracking()
                .Include(balance => balance.StockLocation)
                .Include(balance => balance.StockBatch)
                .Where(balance => balance.ProductId == productId);

            if (locationId is not null)
            {
                balancesQuery = balancesQuery.Where(balance => balance.StockLocationId == locationId.Value);
            }

            var balances = await balancesQuery
                .OrderBy(balance => balance.StockLocation.Code)
                .ThenBy(balance => balance.StockBatch == null || balance.StockBatch.ExpiryDate == null)
                .ThenBy(balance => balance.StockBatch!.ExpiryDate)
                .ThenBy(balance => balance.StockBatchId)
                .ToArrayAsync(cancellationToken);
            return Result<ProductStockDetails>.Success(new ProductStockDetails(
                productId,
                settings.InternalCode,
                settings.Product.Name,
                settings.TracksLots,
                settings.TracksExpiry,
                balances.Select(balance => MapBalance(balance, currentDate))));
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<ProductStockDetails>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<IReadOnlyList<StockMovementView>>> GetMovementsAsync(
        long? productId,
        long? locationId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.StockMovements.AsNoTracking().AsQueryable();

            if (productId is not null)
            {
                query = query.Where(movement => movement.ProductId == productId.Value);
            }

            if (locationId is not null)
            {
                query = query.Where(movement => movement.StockLocationId == locationId.Value);
            }

            var rows = await query
                .OrderByDescending(movement => movement.OccurredAtUtc)
                .ThenByDescending(movement => movement.Id)
                .Take(limit)
                .Select(movement => new StockMovementView(
                    movement.Id,
                    movement.OperationId,
                    movement.ProductId,
                    movement.Product.LocalSettings!.InternalCode ?? string.Empty,
                    movement.Product.Name,
                    movement.StockLocationId,
                    movement.StockLocation.Code,
                    movement.StockBatchId,
                    movement.StockBatch == null ? null : movement.StockBatch.BatchNumber,
                    movement.MovementType,
                    movement.Quantity,
                    movement.QuantityBefore,
                    movement.QuantityAfter,
                    movement.Reason,
                    movement.SourceType,
                    movement.SourceReference,
                    movement.OccurredAtUtc))
                .ToArrayAsync(cancellationToken);
            return Result<IReadOnlyList<StockMovementView>>.Success(Array.AsReadOnly(rows));
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<IReadOnlyList<StockMovementView>>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    public Task<Result<IReadOnlyList<StockBatchView>>> GetExpiringBatchesAsync(
        DateOnly currentDate,
        int horizonDays,
        CancellationToken cancellationToken = default) =>
        GetBatchesByExpiryAsync(currentDate, currentDate.AddDays(horizonDays), false, cancellationToken);

    public Task<Result<IReadOnlyList<StockBatchView>>> GetExpiredBatchesAsync(
        DateOnly currentDate,
        CancellationToken cancellationToken = default) =>
        GetBatchesByExpiryAsync(currentDate, currentDate, true, cancellationToken);

    public async Task<Result<FefoSuggestionView>> SuggestFefoAsync(
        ProductId productId,
        int requestedQuantity,
        DateOnly currentDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productId);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var candidates = await context.StockBalances.AsNoTracking()
                .Where(balance => balance.ProductId == productId.Value
                    && balance.AvailableQuantity > 0
                    && balance.StockLocation.IsActive
                    && balance.Product.IsActive
                    && balance.Product.LocalSettings != null
                    && balance.Product.LocalSettings.IsActive
                    && balance.Product.LocalSettings.IsSellable
                    && (balance.StockBatch == null || (balance.StockBatch.IsActive
                        && (balance.StockBatch.ExpiryDate == null || balance.StockBatch.ExpiryDate >= currentDate))))
                .OrderBy(balance => balance.StockBatch == null || balance.StockBatch.ExpiryDate == null)
                .ThenBy(balance => balance.StockBatch!.ExpiryDate)
                .ThenBy(balance => balance.StockBatch!.CreatedAtUtc)
                .ThenBy(balance => balance.StockBatchId)
                .ThenBy(balance => balance.Id)
                .Select(balance => new
                {
                    balance.Id,
                    balance.StockBatchId,
                    LotNumber = balance.StockBatch == null ? null : balance.StockBatch.BatchNumber,
                    ExpiryDate = balance.StockBatch == null ? null : balance.StockBatch.ExpiryDate,
                    balance.AvailableQuantity
                })
                .ToArrayAsync(cancellationToken);

            if (candidates.Sum(candidate => candidate.AvailableQuantity) < requestedQuantity)
            {
                return Result<FefoSuggestionView>.Failure(InventoryErrors.InsufficientStock);
            }

            var remaining = requestedQuantity;
            var allocations = new List<FefoAllocationLineView>();

            foreach (var candidate in candidates)
            {
                if (remaining == 0)
                {
                    break;
                }

                var allocated = Math.Min(remaining, candidate.AvailableQuantity);
                allocations.Add(new FefoAllocationLineView(
                    candidate.Id,
                    candidate.StockBatchId,
                    candidate.LotNumber,
                    candidate.ExpiryDate,
                    allocated));
                remaining -= allocated;
            }

            return Result<FefoSuggestionView>.Success(
                new FefoSuggestionView(productId.Value, requestedQuantity, allocations));
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<FefoSuggestionView>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<IReadOnlyList<InventorySessionView>>> ListSessionsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var sessions = await SessionQuery(context)
                .OrderByDescending(session => session.StartedAtUtc)
                .ThenByDescending(session => session.Id)
                .Take(limit)
                .ToArrayAsync(cancellationToken);
            return Result<IReadOnlyList<InventorySessionView>>.Success(
                Array.AsReadOnly(sessions.Select(MapSession).ToArray()));
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<IReadOnlyList<InventorySessionView>>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<InventorySessionView>> GetSessionAsync(
        long sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var session = await SessionQuery(context)
                .SingleOrDefaultAsync(candidate => candidate.Id == sessionId, cancellationToken);
            return session is null
                ? Result<InventorySessionView>.Failure(InventoryErrors.SessionNotFound)
                : Result<InventorySessionView>.Success(MapSession(session));
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<InventorySessionView>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    public async Task<InventoryDiagnosticsView> GetDiagnosticsAsync(
        DateOnly currentDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var databaseName = context.Database.GetDbConnection().Database;
            var reachable = await context.Database.CanConnectAsync(cancellationToken);

            if (!reachable)
            {
                return Unavailable(databaseName, true);
            }

            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
            return new InventoryDiagnosticsView(
                true,
                true,
                databaseName,
                pending.Length == 0 ? $"À jour — {applied.LastOrDefault() ?? "aucune migration"}" : $"{pending.Length} migration(s) en attente",
                await context.StockLocations.AsNoTracking().CountAsync(cancellationToken),
                await context.StockBatches.AsNoTracking().CountAsync(cancellationToken),
                await context.StockBalances.AsNoTracking().CountAsync(balance => balance.AvailableQuantity > 0, cancellationToken),
                await context.StockBatches.AsNoTracking().CountAsync(batch => batch.BatchNumber != null
                    && batch.BatchNumber.StartsWith("DEMO-") && batch.ExpiryDate != null && batch.ExpiryDate < currentDate,
                    cancellationToken),
                null);
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Unavailable("Non configurée", false);
        }
    }

    internal static StockLocationView MapLocation(StockLocationEntity location) => new(
        location.Id,
        location.Code,
        location.Name,
        location.IsActive,
        location.IsPrimary,
        location.CreatedAtUtc,
        Convert.ToBase64String(location.RowVersion));

    internal static StockBatchView MapBalance(StockBalanceEntity balance, DateOnly currentDate) => new(
        balance.Id,
        balance.ProductId,
        balance.StockLocationId,
        balance.StockLocation.Code,
        balance.StockLocation.Name,
        balance.StockBatchId,
        balance.StockBatch?.BatchNumber,
        balance.StockBatch?.ExpiryDate,
        balance.StockBatch?.IsActive ?? true,
        balance.StockBatch?.ExpiryDate < currentDate,
        balance.AvailableQuantity,
        Convert.ToBase64String(balance.RowVersion),
        balance.StockBatch is null ? null : Convert.ToBase64String(balance.StockBatch.RowVersion));

    internal static InventorySessionView MapSession(InventorySessionEntity session) => new(
        session.Id,
        session.Reference,
        session.StockLocationId,
        session.StockLocation.Code,
        session.Status,
        session.StartedAtUtc,
        session.CompletedAtUtc,
        session.Notes,
        Convert.ToBase64String(session.RowVersion),
        session.Lines
            .OrderBy(line => line.Product.LocalSettings!.InternalCode)
            .ThenBy(line => line.StockBatchId)
            .Select(line => new InventoryCountLineView(
                line.Id,
                line.ProductId,
                line.Product.LocalSettings?.InternalCode ?? string.Empty,
                line.Product.Name,
                line.StockBatchId,
                line.StockBatch?.BatchNumber,
                line.ExpectedQuantity,
                line.CountedQuantity,
                line.CountedQuantity is null ? null : line.CountedQuantity.Value - line.ExpectedQuantity,
                Convert.ToBase64String(line.RowVersion))));

    private async Task<Result<IReadOnlyList<StockBatchView>>> GetBatchesByExpiryAsync(
        DateOnly currentDate,
        DateOnly endDate,
        bool expired,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.StockBalances.AsNoTracking()
                .Include(balance => balance.StockLocation)
                .Include(balance => balance.StockBatch)
                .Where(balance => balance.StockBatch != null && balance.StockBatch.ExpiryDate != null);
            query = expired
                ? query.Where(balance => balance.StockBatch!.ExpiryDate < currentDate)
                : query.Where(balance => balance.StockBatch!.ExpiryDate >= currentDate
                    && balance.StockBatch.ExpiryDate <= endDate);
            var balances = await query
                .OrderBy(balance => balance.StockBatch!.ExpiryDate)
                .ThenBy(balance => balance.StockBatchId)
                .ThenBy(balance => balance.StockLocationId)
                .ToArrayAsync(cancellationToken);
            return Result<IReadOnlyList<StockBatchView>>.Success(
                Array.AsReadOnly(balances.Select(balance => MapBalance(balance, currentDate)).ToArray()));
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<IReadOnlyList<StockBatchView>>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }

    private static IQueryable<InventorySessionEntity> SessionQuery(PharmacyDbContext context) =>
        context.InventorySessions.AsNoTracking().AsSplitQuery()
            .Include(session => session.StockLocation)
            .Include(session => session.Lines).ThenInclude(line => line.Product).ThenInclude(product => product.LocalSettings)
            .Include(session => session.Lines).ThenInclude(line => line.StockBatch);

    private static InventoryDiagnosticsView Unavailable(string databaseName, bool configured) =>
        new(configured, false, databaseName, "Indisponible", 0, 0, 0, 0, InventoryErrors.DatabaseUnavailable.Code);
}
