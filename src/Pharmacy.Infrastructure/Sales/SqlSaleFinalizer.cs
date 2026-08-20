using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Cash;
using Pharmacy.Infrastructure.Persistence.Entities.Customers;
using Pharmacy.Infrastructure.Persistence.Entities.Catalog;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;
using Pharmacy.Infrastructure.Persistence.Entities.Sales;

namespace Pharmacy.Infrastructure.Sales;

public sealed class SqlSaleFinalizer : ISaleFinalizer
{
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;
    private readonly ISalesClock clock;
    private readonly Func<CancellationToken, Task>? afterPlan;

    public SqlSaleFinalizer(
        IDbContextFactory<PharmacyDbContext> contextFactory,
        ISalesClock clock)
        : this(contextFactory, clock, null)
    {
    }

    internal SqlSaleFinalizer(
        IDbContextFactory<PharmacyDbContext> contextFactory,
        ISalesClock clock,
        Func<CancellationToken, Task>? afterPlan)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.afterPlan = afterPlan;
    }

    public async Task<Result<FinalizedSaleView>> FinalizeAsync(
        FinalizeSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Preparation);
        ArgumentNullException.ThrowIfNull(request.Payment);

        var validation = ValidateRequest(request);

        if (validation.IsFailure)
        {
            return Result<FinalizedSaleView>.Failure(validation.Error);
        }

        var fingerprint = ComputeFingerprint(request);

        try
        {
            await using var strategyContext = await contextFactory.CreateDbContextAsync(cancellationToken);
            var strategy = strategyContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
                await using var transaction = await context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

                try
                {
                    var replay = await context.SaleOperations
                        .AsNoTracking()
                        .SingleOrDefaultAsync(
                            operation => operation.OperationId == request.Preparation.OperationId,
                            cancellationToken);

                    if (replay is not null)
                    {
                        var replayResult = await ResolveReplayAsync(
                            context,
                            replay,
                            fingerprint,
                            cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        return replayResult;
                    }

                    var planResult = await BuildPlanAsync(context, request, cancellationToken);

                    if (planResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<FinalizedSaleView>.Failure(planResult.Error);
                    }

                    if (afterPlan is not null)
                    {
                        await afterPlan(cancellationToken);
                    }

                    var now = clock.UtcNow;
                    var customerResult = await ValidateCreditCustomerAsync(context, request, cancellationToken);

                    if (customerResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<FinalizedSaleView>.Failure(customerResult.Error);
                    }

                    var suspensionResult = await PrepareSuspensionCompletionAsync(
                        context,
                        request,
                        now,
                        cancellationToken);
                    if (suspensionResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<FinalizedSaleView>.Failure(suspensionResult.Error);
                    }

                    var operation = new SaleOperationEntity
                    {
                        OperationId = request.Preparation.OperationId,
                        OperationFingerprint = fingerprint,
                        Status = "Processing",
                        CreatedAtUtc = now
                    };
                    context.SaleOperations.Add(operation);
                    await context.SaveChangesAsync(cancellationToken);

                    var sale = CreateSale(request, planResult.Value, now, fingerprint);
                    context.Sales.Add(sale);
                    await context.SaveChangesAsync(cancellationToken);
                    AddSprint14LedgerEntries(context, request, sale, now);

                    operation.Status = "Completed";
                    operation.ResultSaleId = sale.Id;
                    operation.CompletedAtUtc = now;
                    await context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    return Result<FinalizedSaleView>.Success(SqlSalesReader.Map(sale));
                }
                catch (DbUpdateConcurrencyException)
                {
                    await RollbackQuietlyAsync(transaction, cancellationToken);
                    return Result<FinalizedSaleView>.Failure(
                        SalesApplicationErrors.StockConcurrencyConflict);
                }
                catch (Exception exception) when (SalesSqlFailure.IsConcurrencyConflict(exception))
                {
                    await RollbackQuietlyAsync(transaction, cancellationToken);
                    return Result<FinalizedSaleView>.Failure(
                        SalesApplicationErrors.StockConcurrencyConflict);
                }
                catch (DbUpdateException exception) when (
                    !SalesSqlFailure.IsUniqueViolation(exception)
                    && !SalesSqlFailure.IsDatabaseUnavailable(exception))
                {
                    await RollbackQuietlyAsync(transaction, cancellationToken);
                    return Result<FinalizedSaleView>.Failure(
                        SalesApplicationErrors.PersistenceFailed);
                }
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<FinalizedSaleView>.Failure(SalesApplicationErrors.StockConcurrencyConflict);
        }
        catch (Exception exception) when (SalesSqlFailure.IsConcurrencyConflict(exception))
        {
            return Result<FinalizedSaleView>.Failure(SalesApplicationErrors.StockConcurrencyConflict);
        }
        catch (Exception exception) when (SalesSqlFailure.IsUniqueViolation(exception))
        {
            return await ResolveUniqueRaceAsync(request, fingerprint, cancellationToken);
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<FinalizedSaleView>.Failure(SalesApplicationErrors.DatabaseUnavailable);
        }
        catch (DbUpdateException)
        {
            return Result<FinalizedSaleView>.Failure(SalesApplicationErrors.PersistenceFailed);
        }
    }

    private static async Task RollbackQuietlyAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (Exception)
        {
            // SQL Server peut avoir déjà annulé la transaction victime d'un deadlock.
        }
    }

    private static Result ValidateRequest(FinalizeSaleRequest request)
    {
        const decimal maximumSqlMoney = 999_999_999_999_999.9999m;
        if (request.Preparation.OperationId == Guid.Empty)
        {
            return Result.Failure(SalesApplicationErrors.InvalidOperationId);
        }

        if (request.SuspendedSale is not null
            && (request.SuspendedSale.SuspendedSaleId <= 0
                || request.SuspendedSale.RowVersion.Length == 0))
        {
            return Result.Failure(SalesApplicationErrors.OperationConflict);
        }

        if (request.Preparation.Lines.Count == 0)
        {
            return Result.Failure(SalesApplicationErrors.DraftEmpty);
        }

        if (request.Preparation.Lines.Select(line => line.ProductId.Value).Distinct().Count()
            != request.Preparation.Lines.Count
            || request.Preparation.Lines.Any(line => line.Quantity.Value <= 0
                || line.UnitPrice.Amount < 0m
                || line.UnitPrice.Amount > maximumSqlMoney
                || decimal.Round(line.UnitPrice.Amount, 4) != line.UnitPrice.Amount
                || line.Subtotal.Amount != line.UnitPrice.Amount * line.Quantity.Value)
            || request.Preparation.Total.Amount != request.Preparation.Lines.Sum(line => line.Subtotal.Amount)
            || request.Preparation.Total.Amount > maximumSqlMoney
            || decimal.Round(request.Preparation.Total.Amount, 4) != request.Preparation.Total.Amount
            || request.Payment.Amount < 0m
            || request.Payment.Amount > maximumSqlMoney
            || decimal.Round(request.Payment.Amount, 4) != request.Payment.Amount
            || request.Payment.TenderedAmount is not null
                && (request.Payment.TenderedAmount.Value < 0m
                    || request.Payment.TenderedAmount.Value > maximumSqlMoney
                    || decimal.Round(request.Payment.TenderedAmount.Value, 4)
                        != request.Payment.TenderedAmount.Value)
            || request.Payment.ChangeGiven < 0m
            || request.Payment.ChangeGiven > maximumSqlMoney
            || decimal.Round(request.Payment.ChangeGiven, 4) != request.Payment.ChangeGiven
            || !Enum.IsDefined(request.Payment.Method)
            || request.Payment.Amount + (request.Credit?.Amount ?? 0m) != request.Preparation.Total.Amount)
        {
            return Result.Failure(SalesApplicationErrors.PaymentInvalid);
        }

        if (request.Credit is not null
            && (request.Credit.CustomerId <= 0
                || request.Credit.Amount <= 0m
                || request.Credit.Amount > maximumSqlMoney
                || decimal.Round(request.Credit.Amount, 4) != request.Credit.Amount))
        {
            return Result.Failure(SalesApplicationErrors.CreditCustomerRequired);
        }

        if (request.Payment.Method == SalePaymentMethod.ExternalCard)
        {
            return request.Payment.ExternalCardConfirmed
                && request.Payment.TenderedAmount is null
                && request.Payment.ChangeGiven == 0m
                    ? Result.Success()
                    : Result.Failure(SalesApplicationErrors.CardUnconfirmed);
        }

        return request.Payment.Method == SalePaymentMethod.Cash
            && request.Payment.TenderedAmount is not null
            && request.Payment.TenderedAmount.Value >= request.Payment.Amount
            && request.Payment.ChangeGiven == request.Payment.TenderedAmount.Value - request.Payment.Amount
            && !request.Payment.ExternalCardConfirmed
                ? Result.Success()
                : Result.Failure(SalesApplicationErrors.PaymentInvalid);
    }

    private static async Task<Result> ValidateCreditCustomerAsync(
        PharmacyDbContext context,
        FinalizeSaleRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Credit is null)
        {
            return Result.Success();
        }

        var customer = await context.Customers.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == request.Credit.CustomerId,
            cancellationToken);

        if (customer is null)
        {
            return Result.Failure(SalesApplicationErrors.CreditCustomerRequired);
        }

        return customer.IsActive && customer.CreditAllowed
            ? Result.Success()
            : Result.Failure(SalesApplicationErrors.CreditNotAllowed);
    }

    private static async Task<Result> PrepareSuspensionCompletionAsync(
        PharmacyDbContext context,
        FinalizeSaleRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (request.SuspendedSale is null)
        {
            return Result.Success();
        }

        var completion = request.SuspendedSale;
        var suspension = await context.SuspendedSales.SingleOrDefaultAsync(
                sale => sale.Id == completion.SuspendedSaleId,
                cancellationToken);
        if (suspension is null
            || suspension.Status != "Open")
        {
            return Result.Failure(SalesApplicationErrors.OperationConflict);
        }

        context.Entry(suspension)
            .Property(sale => sale.RowVersion)
            .OriginalValue = completion.RowVersion;
        suspension.Status = "Resumed";
        suspension.UpdatedAtUtc = now;
        return Result.Success();
    }

    private async Task<Result<IReadOnlyList<AllocationPlan>>> BuildPlanAsync(
        PharmacyDbContext context,
        FinalizeSaleRequest request,
        CancellationToken cancellationToken)
    {
        var productIds = request.Preparation.Lines.Select(line => line.ProductId.Value).ToArray();
        var products = await context.ProductLocalSettings
            .Include(settings => settings.Product)
            .Where(settings => productIds.Contains(settings.ProductId))
            .ToDictionaryAsync(settings => settings.ProductId, cancellationToken);

        foreach (var line in request.Preparation.Lines)
        {
            if (!products.TryGetValue(line.ProductId.Value, out var settings)
                || !settings.Product.IsActive
                || !settings.IsActive
                || !settings.IsSellable)
            {
                return Result<IReadOnlyList<AllocationPlan>>.Failure(
                    SalesApplicationErrors.ProductNotSellable);
            }

            if (settings.CurrentSalePrice != line.UnitPrice.Amount)
            {
                return Result<IReadOnlyList<AllocationPlan>>.Failure(SalesApplicationErrors.PriceChanged);
            }
        }

        var balances = await context.StockBalances
            .Include(balance => balance.StockBatch)
            .Include(balance => balance.StockLocation)
            .Where(balance => productIds.Contains(balance.ProductId)
                && balance.AvailableQuantity > 0
                && balance.StockLocation.IsActive)
            .ToArrayAsync(cancellationToken);
        var plans = new List<AllocationPlan>();

        foreach (var line in request.Preparation.Lines)
        {
            var settings = products[line.ProductId.Value];
            var candidates = balances
                .Where(balance => balance.ProductId == line.ProductId.Value
                    && IsSellableBalance(balance, settings, clock.CurrentDate))
                .OrderBy(balance => balance.StockBatch?.ExpiryDate is null ? 1 : 0)
                .ThenBy(balance => balance.StockBatch?.ExpiryDate)
                .ThenBy(balance => balance.StockBatchId ?? long.MaxValue)
                .ThenBy(balance => balance.StockLocationId)
                .ThenBy(balance => balance.Id)
                .ToArray();
            var remaining = line.Quantity.Value;

            foreach (var balance in candidates)
            {
                if (remaining == 0)
                {
                    break;
                }

                var quantity = Math.Min(remaining, balance.AvailableQuantity);
                plans.Add(new AllocationPlan(line, balance, quantity));
                remaining -= quantity;
            }

            if (remaining > 0)
            {
                return Result<IReadOnlyList<AllocationPlan>>.Failure(
                    SalesApplicationErrors.StockInsufficient);
            }
        }

        return Result<IReadOnlyList<AllocationPlan>>.Success(plans.AsReadOnly());
    }

    private static bool IsSellableBalance(
        StockBalanceEntity balance,
        ProductLocalSettingsEntity settings,
        DateOnly currentDate)
    {
        if (settings.TracksLots && balance.StockBatch is null
            || !settings.TracksLots && balance.StockBatch is not null)
        {
            return false;
        }

        if (balance.StockBatch is null)
        {
            return !settings.TracksExpiry;
        }

        return balance.StockBatch.IsActive
            && (!settings.TracksExpiry || balance.StockBatch.ExpiryDate is not null)
            && (balance.StockBatch.ExpiryDate is null || balance.StockBatch.ExpiryDate >= currentDate);
    }

    private static SaleEntity CreateSale(
        FinalizeSaleRequest request,
        IReadOnlyList<AllocationPlan> plans,
        DateTime now,
        string fingerprint)
    {
        var operationId = request.Preparation.OperationId;
        var sale = new SaleEntity
        {
            Reference = $"VTE-{now:yyyyMMdd}-{operationId:N}".ToUpperInvariant(),
            OperationId = operationId,
            Status = "Completed",
            CompletedAtUtc = now,
            GrandTotal = request.Preparation.Total.Amount,
            PaidTotal = request.Payment.Amount,
            CreditTotal = request.Credit?.Amount ?? 0m,
            ChangeGiven = request.Payment.ChangeGiven,
            TotalQuantity = request.Preparation.TotalQuantity,
            LineCount = request.Preparation.Lines.Count,
            PaymentMethod = request.Payment.Method,
            CustomerId = request.Credit?.CustomerId
        };

        var lineEntities = new Dictionary<long, SaleLineEntity>();

        foreach (var line in request.Preparation.Lines)
        {
            var entity = new SaleLineEntity
            {
                ProductId = line.ProductId.Value,
                ProductReferenceSnapshot = line.Reference.Value,
                ProductNameSnapshot = line.DisplayName,
                PresentationSnapshot = line.Presentation,
                UnitPrice = line.UnitPrice.Amount,
                Quantity = line.Quantity.Value,
                LineTotal = line.Subtotal.Amount
            };
            sale.Lines.Add(entity);
            lineEntities.Add(line.ProductId.Value, entity);
        }

        for (var index = 0; index < plans.Count; index++)
        {
            var plan = plans[index];
            var balance = plan.Balance;
            var before = balance.AvailableQuantity;
            balance.AvailableQuantity -= plan.Quantity;
            balance.UpdatedAtUtc = now;
            lineEntities[plan.Line.ProductId.Value].Allocations.Add(new SaleLineAllocationEntity
            {
                StockBalance = balance,
                StockBatch = balance.StockBatch,
                Quantity = plan.Quantity,
                BatchNumberSnapshot = balance.StockBatch?.BatchNumber,
                ExpiryDateSnapshot = balance.StockBatch?.ExpiryDate
            });
            balance.StockLocation.Movements.Add(new StockMovementEntity
            {
                OperationId = DeterministicGuid(operationId, "movement", index),
                OperationFingerprint = $"SALE|{fingerprint}|{index}",
                ProductId = plan.Line.ProductId.Value,
                StockLocation = balance.StockLocation,
                StockBatch = balance.StockBatch,
                MovementType = StockMovementType.Sale,
                Quantity = plan.Quantity,
                QuantityBefore = before,
                QuantityAfter = balance.AvailableQuantity,
                Reason = "Vente finalisée",
                SourceType = "Sale",
                SourceReference = sale.Reference,
                OccurredAtUtc = now,
                CreatedAtUtc = now
            });
        }

        sale.Payments.Add(new PaymentEntity
        {
            OperationId = DeterministicGuid(operationId, "payment", 0),
            PaymentMethod = request.Payment.Method,
            Status = "Completed",
            Amount = request.Payment.Amount,
            TenderedAmount = request.Payment.TenderedAmount,
            ChangeGiven = request.Payment.ChangeGiven,
            ExternalCardConfirmed = request.Payment.ExternalCardConfirmed,
            ConfirmedAtUtc = now
        });
        return sale;
    }

    private static void AddSprint14LedgerEntries(
        PharmacyDbContext context,
        FinalizeSaleRequest request,
        SaleEntity sale,
        DateTime now)
    {
        if (request.Payment.Method == SalePaymentMethod.Cash && request.Payment.Amount > 0m)
        {
            context.CashLedgerEntries.Add(new CashLedgerEntryEntity
            {
                OperationId = DeterministicGuid(request.Preparation.OperationId, "cash-sale", 0),
                EntryType = "SaleCashIn",
                Amount = request.Payment.Amount,
                SourceType = "Sale",
                SourceReference = sale.Reference,
                OccurredAtUtc = now,
                CreatedAtUtc = now
            });
        }

        if (request.Credit is not null)
        {
            context.CustomerLedgerEntries.Add(new CustomerLedgerEntryEntity
            {
                OperationId = DeterministicGuid(request.Preparation.OperationId, "credit-sale", 0),
                CustomerId = request.Credit.CustomerId,
                EntryType = "CreditSale",
                Amount = request.Credit.Amount,
                SourceType = "Sale",
                SourceReference = sale.Reference,
                OccurredAtUtc = now,
                CreatedAtUtc = now
            });
        }
    }

    private async Task<Result<FinalizedSaleView>> ResolveUniqueRaceAsync(
        FinalizeSaleRequest request,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var operation = await context.SaleOperations.AsNoTracking().SingleOrDefaultAsync(
                candidate => candidate.OperationId == request.Preparation.OperationId,
                cancellationToken);
            return operation is null
                ? Result<FinalizedSaleView>.Failure(SalesApplicationErrors.OperationConflict)
                : await ResolveReplayAsync(context, operation, fingerprint, cancellationToken);
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<FinalizedSaleView>.Failure(SalesApplicationErrors.DatabaseUnavailable);
        }
    }

    private static async Task<Result<FinalizedSaleView>> ResolveReplayAsync(
        PharmacyDbContext context,
        SaleOperationEntity operation,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(operation.OperationFingerprint, fingerprint, StringComparison.Ordinal)
            || operation.Status != "Completed"
            || operation.ResultSaleId is null)
        {
            return Result<FinalizedSaleView>.Failure(SalesApplicationErrors.OperationConflict);
        }

        return await SqlSalesReader.LoadByIdAsync(
            context,
            operation.ResultSaleId.Value,
            cancellationToken);
    }

    private static string ComputeFingerprint(FinalizeSaleRequest request)
    {
        var lines = request.Preparation.Lines
            .OrderBy(line => line.ProductId.Value)
            .Select(line => string.Join(
                "~",
                line.ProductId.Value.ToString(CultureInfo.InvariantCulture),
                line.Reference.Value,
                line.DisplayName,
                line.Presentation ?? string.Empty,
                line.Quantity.Value.ToString(CultureInfo.InvariantCulture),
                line.UnitPrice.Amount.ToString("G29", CultureInfo.InvariantCulture),
                line.Subtotal.Amount.ToString("G29", CultureInfo.InvariantCulture)));
        var content = string.Join(
            "|",
            request.Preparation.DraftId.Value.ToString("N"),
            request.Preparation.PreparedVersion.ToString(CultureInfo.InvariantCulture),
            string.Join(";", lines),
            request.Preparation.Total.Amount.ToString("G29", CultureInfo.InvariantCulture),
            request.Payment.Method.ToString(),
            request.Payment.Amount.ToString("G29", CultureInfo.InvariantCulture),
            request.Payment.TenderedAmount?.ToString("G29", CultureInfo.InvariantCulture) ?? string.Empty,
            request.Payment.ChangeGiven.ToString("G29", CultureInfo.InvariantCulture),
            request.Payment.ExternalCardConfirmed ? "1" : "0");
        content = string.Join(
            "|",
            content,
            request.Credit?.CustomerId.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            request.Credit?.Amount.ToString("G29", CultureInfo.InvariantCulture) ?? string.Empty);
        content = string.Join(
            "|",
            content,
            request.SuspendedSale?.SuspendedSaleId.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            request.SuspendedSale is null
                ? string.Empty
                : Convert.ToHexString(request.SuspendedSale.RowVersion));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }

    private static Guid DeterministicGuid(Guid operationId, string purpose, int index)
    {
        var bytes = Encoding.UTF8.GetBytes($"{operationId:N}|{purpose}|{index}");
        return new Guid(SHA256.HashData(bytes).AsSpan(0, 16));
    }

    private sealed record AllocationPlan(
        SaleDraftLineView Line,
        StockBalanceEntity Balance,
        int Quantity);
}
