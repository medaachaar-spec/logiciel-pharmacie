using System.Data;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Customers.Contracts;
using Pharmacy.Application.Customers.Errors;
using Pharmacy.Application.Customers.Models;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Cash;
using Pharmacy.Infrastructure.Persistence.Entities.Customers;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;
using Pharmacy.Infrastructure.Persistence.Entities.Sales;

namespace Pharmacy.Infrastructure.Sales;

public sealed class SqlCustomerCreditCashStore :
    ICustomerStore,
    ICreditPaymentStore,
    ISuspendedSaleStore,
    ISaleReturnStore
{
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;
    private readonly ISalesClock clock;
    private readonly Func<CancellationToken, Task>? afterCreditBalanceRead;

    public SqlCustomerCreditCashStore(
        IDbContextFactory<PharmacyDbContext> contextFactory,
        ISalesClock clock)
        : this(contextFactory, clock, null)
    {
    }

    internal SqlCustomerCreditCashStore(
        IDbContextFactory<PharmacyDbContext> contextFactory,
        ISalesClock clock,
        Func<CancellationToken, Task>? afterCreditBalanceRead)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.afterCreditBalanceRead = afterCreditBalanceRead;
    }

    public async Task<Result<CustomerDetailView>> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.InvalidCustomerName);
        }

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var now = clock.UtcNow;
            var customer = new CustomerEntity
            {
                Reference = $"CLI-{now:yyyyMMdd}-{request.OperationId:N}"[..24].ToUpperInvariant(),
                FullName = request.FullName.Trim(),
                Phone = NormalizeOptional(request.Phone),
                Note = NormalizeOptional(request.Note),
                IsActive = true,
                CreditAllowed = request.CreditAllowed,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            context.Customers.Add(customer);
            await context.SaveChangesAsync(cancellationToken);
            return await LoadCustomerAsync(context, customer.Id, cancellationToken);
        }
        catch (Exception exception) when (SalesSqlFailure.IsUniqueViolation(exception))
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var operationFragment = request.OperationId.ToString("N")[..12];
            var existing = await context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(customer => customer.Reference.Contains(operationFragment), cancellationToken);
            return existing is null
                ? Result<CustomerDetailView>.Failure(CustomerApplicationErrors.OperationConflict)
                : await LoadCustomerAsync(context, existing.Id, cancellationToken);
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<CustomerDetailView>> UpdateAsync(
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.InvalidCustomerName);
        }

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var customer = await context.Customers.SingleOrDefaultAsync(
                candidate => candidate.Id == request.CustomerId,
                cancellationToken);

            if (customer is null)
            {
                return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.CustomerNotFound);
            }

            context.Entry(customer).Property(entity => entity.RowVersion).OriginalValue = request.RowVersion;
            customer.FullName = request.FullName.Trim();
            customer.Phone = NormalizeOptional(request.Phone);
            customer.Note = NormalizeOptional(request.Note);
            customer.IsActive = request.IsActive;
            customer.CreditAllowed = request.CreditAllowed;
            customer.UpdatedAtUtc = clock.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return await LoadCustomerAsync(context, customer.Id, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.ConcurrencyConflict);
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<IReadOnlyList<CustomerSummaryView>>> SearchAsync(
        CustomerSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var limit = Math.Clamp(query.Limit, 1, 100);
            var term = query.SearchTerm?.Trim();
            var rowsQuery = context.Customers.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(term))
            {
                rowsQuery = rowsQuery.Where(customer =>
                    customer.Reference.Contains(term)
                    || customer.FullName.Contains(term)
                    || customer.Phone != null && customer.Phone.Contains(term));
            }

            var customers = await rowsQuery
                .OrderBy(customer => customer.FullName)
                .ThenBy(customer => customer.Id)
                .Take(limit)
                .ToArrayAsync(cancellationToken);
            var ids = customers.Select(customer => customer.Id).ToArray();
            var balances = await LoadBalancesAsync(context, ids, cancellationToken);
            var views = customers.Select(customer => MapSummary(customer, balances.GetValueOrDefault(customer.Id))).ToArray();
            return Result<IReadOnlyList<CustomerSummaryView>>.Success(Array.AsReadOnly(views));
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<IReadOnlyList<CustomerSummaryView>>.Failure(CustomerApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<CustomerDetailView>> GetByIdAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await LoadCustomerAsync(context, customerId, cancellationToken);
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<CustomerDetailView>> RegisterPaymentAsync(
        RegisterCreditPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidCreditPayment(request))
        {
            return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.InvalidCreditPayment);
        }

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
                var replay = await context.CustomerLedgerEntries.AsNoTracking()
                    .SingleOrDefaultAsync(entry => entry.OperationId == request.OperationId, cancellationToken);

                if (replay is not null)
                {
                    var isSame = await IsSameCreditPaymentAsync(context, replay, request, cancellationToken);
                    var replayResult = isSame
                        ? await LoadCustomerAsync(context, replay.CustomerId, cancellationToken)
                        : Result<CustomerDetailView>.Failure(CustomerApplicationErrors.OperationConflict);
                    await transaction.CommitAsync(cancellationToken);
                    return replayResult;
                }

                var customer = await context.Customers.SingleOrDefaultAsync(
                    candidate => candidate.Id == request.CustomerId,
                    cancellationToken);

                if (customer is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.CustomerNotFound);
                }

                var balance = await ComputeBalanceAsync(context, request.CustomerId, cancellationToken);

                if (request.Amount > balance)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.InvalidCreditPayment);
                }

                if (afterCreditBalanceRead is not null)
                {
                    await afterCreditBalanceRead(cancellationToken);
                }

                var now = clock.UtcNow;
                var sourceReference = CreditPaymentReference(now, request.OperationId);
                context.CustomerLedgerEntries.Add(new CustomerLedgerEntryEntity
                {
                    OperationId = request.OperationId,
                    CustomerId = request.CustomerId,
                    EntryType = "CreditPayment",
                    Amount = request.Amount,
                    SourceType = "CreditPayment",
                    SourceReference = sourceReference,
                    OccurredAtUtc = now,
                    CreatedAtUtc = now
                });

                if (request.PaymentMethod == SalePaymentMethod.Cash)
                {
                    context.CashLedgerEntries.Add(new CashLedgerEntryEntity
                    {
                        OperationId = DeterministicGuid(request.OperationId, "cash-credit-payment"),
                        EntryType = "CreditPaymentCashIn",
                        Amount = request.Amount,
                        SourceType = "CreditPayment",
                        SourceReference = sourceReference,
                        OccurredAtUtc = now,
                        CreatedAtUtc = now
                    });
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return await LoadCustomerAsync(context, request.CustomerId, cancellationToken);
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.ConcurrencyConflict);
        }
        catch (Exception exception) when (SalesSqlFailure.IsConcurrencyConflict(exception))
        {
            return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.ConcurrencyConflict);
        }
        catch (Exception exception) when (SalesSqlFailure.IsUniqueViolation(exception))
        {
            return await ResolveCreditPaymentRaceAsync(request, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.ConcurrencyConflict);
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<SuspendedSaleSummaryView>> SuspendAsync(
        SuspendSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OperationId == Guid.Empty || request.Lines.Count == 0 || request.Lines.Any(line => line.Quantity <= 0 || line.UnitPrice < 0m))
        {
            return Result<SuspendedSaleSummaryView>.Failure(SalesApplicationErrors.PaymentInvalid);
        }

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var replay = await context.SuspendedSales.AsNoTracking()
                .Include(sale => sale.Lines)
                .SingleOrDefaultAsync(sale => sale.OperationId == request.OperationId, cancellationToken);

            if (replay is not null)
            {
                return IsSameSuspension(replay, request)
                    ? Result<SuspendedSaleSummaryView>.Success(MapSuspension(replay))
                    : Result<SuspendedSaleSummaryView>.Failure(SalesApplicationErrors.OperationConflict);
            }

            var now = clock.UtcNow;
            var customerName = request.CustomerId is null
                ? null
                : await context.Customers
                    .Where(customer => customer.Id == request.CustomerId.Value)
                    .Select(customer => customer.FullName)
                    .SingleOrDefaultAsync(cancellationToken);
            var suspension = new SuspendedSaleEntity
            {
                OperationId = request.OperationId,
                Reference = $"HOLD-{now:yyyyMMdd}-{request.OperationId:N}"[..30].ToUpperInvariant(),
                CustomerId = request.CustomerId,
                CustomerNameSnapshot = customerName,
                GrandTotal = request.Lines.Sum(line => line.UnitPrice * line.Quantity),
                LineCount = request.Lines.Count,
                Status = "Open",
                SuspendedAtUtc = now,
                UpdatedAtUtc = now
            };
            foreach (var line in request.Lines)
            {
                suspension.Lines.Add(new SuspendedSaleLineEntity
                {
                    ProductId = line.ProductId,
                    ProductReferenceSnapshot = line.ProductReference,
                    ProductNameSnapshot = line.ProductName,
                    PresentationSnapshot = line.Presentation,
                    UnitPrice = line.UnitPrice,
                    Quantity = line.Quantity,
                    LineTotal = line.UnitPrice * line.Quantity
                });
            }

            context.SuspendedSales.Add(suspension);
            await context.SaveChangesAsync(cancellationToken);
            return Result<SuspendedSaleSummaryView>.Success(MapSuspension(suspension));
        }
        catch (Exception exception) when (SalesSqlFailure.IsUniqueViolation(exception))
        {
            return await ResolveSuspensionReplayAsync(request, cancellationToken);
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<SuspendedSaleSummaryView>.Failure(SalesApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<IReadOnlyList<SuspendedSaleSummaryView>>> GetOpenAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var rows = await context.SuspendedSales.AsNoTracking()
                .Include(sale => sale.Lines)
                .Where(sale => sale.Status == "Open")
                .OrderByDescending(sale => sale.SuspendedAtUtc)
                .Take(50)
                .ToArrayAsync(cancellationToken);
            return Result<IReadOnlyList<SuspendedSaleSummaryView>>.Success(
                Array.AsReadOnly(rows.Select(MapSuspension).ToArray()));
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<IReadOnlyList<SuspendedSaleSummaryView>>.Failure(SalesApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result> AbandonAsync(
        Guid operationId,
        long suspendedSaleId,
        byte[] rowVersion,
        CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty || suspendedSaleId <= 0 || rowVersion.Length == 0)
        {
            return Result.Failure(SalesApplicationErrors.OperationConflict);
        }

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var suspension = await context.SuspendedSales.SingleOrDefaultAsync(
                sale => sale.Id == suspendedSaleId,
                cancellationToken);

            if (suspension is null)
            {
                return Result.Failure(SalesApplicationErrors.SaleNotFound);
            }

            if (suspension.Status != "Open")
            {
                return suspension.RowVersion.AsSpan().SequenceEqual(rowVersion)
                    ? Result.Failure(SalesApplicationErrors.SaleNotFound)
                    : Result.Failure(SalesApplicationErrors.OperationConflict);
            }

            context.Entry(suspension).Property(entity => entity.RowVersion).OriginalValue = rowVersion;
            suspension.Status = "Abandoned";
            suspension.UpdatedAtUtc = clock.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(SalesApplicationErrors.OperationConflict);
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result.Failure(SalesApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<SaleReturnView>> ReturnAsync(
        ReturnSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OperationId == Guid.Empty
            || request.SaleId <= 0
            || request.Lines.Count == 0
            || request.Lines.Any(line => line.SaleLineId <= 0
                || line.Quantity <= 0
                || !Enum.IsDefined(line.StockDestination))
            || request.Lines.Select(line => line.SaleLineId).Distinct().Count() != request.Lines.Count
            || !Enum.IsDefined(request.RefundMethod))
        {
            return Result<SaleReturnView>.Failure(SalesApplicationErrors.ReturnInvalid);
        }

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
            var replay = await context.SaleReturns.AsNoTracking()
                .Include(ret => ret.Lines)
                .SingleOrDefaultAsync(ret => ret.OperationId == request.OperationId, cancellationToken);

            if (replay is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return IsSameReturn(replay, request)
                    ? Result<SaleReturnView>.Success(MapReturn(replay))
                    : Result<SaleReturnView>.Failure(SalesApplicationErrors.OperationConflict);
            }

            var sale = await context.Sales
                .Include(candidate => candidate.Lines)
                    .ThenInclude(line => line.Allocations)
                .SingleOrDefaultAsync(candidate => candidate.Id == request.SaleId, cancellationToken);

            if (sale is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<SaleReturnView>.Failure(SalesApplicationErrors.SaleNotFound);
            }

            var refundMethodIsCompatible = request.RefundMethod switch
            {
                ReturnRefundMethod.Cash => sale.PaymentMethod == SalePaymentMethod.Cash,
                ReturnRefundMethod.ExternalCardPending =>
                    sale.PaymentMethod == SalePaymentMethod.ExternalCard,
                ReturnRefundMethod.CreditReduction =>
                    sale.CustomerId is not null && sale.CreditTotal > 0m,
                _ => false
            };
            if (!refundMethodIsCompatible)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<SaleReturnView>.Failure(SalesApplicationErrors.ReturnInvalid);
            }

            var now = clock.UtcNow;
            var saleReturn = new SaleReturnEntity
            {
                OperationId = request.OperationId,
                Reference = $"RET-{now:yyyyMMdd}-{request.OperationId:N}"[..30].ToUpperInvariant(),
                OriginalSaleId = sale.Id,
                Status = "Completed",
                RefundMethod = request.RefundMethod.ToString(),
                CompletedAtUtc = now
            };

            foreach (var lineRequest in request.Lines)
            {
                var saleLine = sale.Lines.SingleOrDefault(line => line.Id == lineRequest.SaleLineId);
                if (saleLine is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<SaleReturnView>.Failure(SalesApplicationErrors.SaleNotFound);
                }

                var alreadyReturned = await context.SaleReturnLines
                    .Where(line => line.SaleLineId == saleLine.Id)
                    .SumAsync(line => (int?)line.Quantity, cancellationToken) ?? 0;
                if (alreadyReturned + lineRequest.Quantity > saleLine.Quantity)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<SaleReturnView>.Failure(SalesApplicationErrors.ReturnQuantityExceeded);
                }

                var amount = saleLine.UnitPrice * lineRequest.Quantity;
                saleReturn.RefundAmount += amount;
                saleReturn.Lines.Add(new SaleReturnLineEntity
                {
                    SaleLineId = saleLine.Id,
                    ProductId = saleLine.ProductId,
                    Quantity = lineRequest.Quantity,
                    UnitPrice = saleLine.UnitPrice,
                    LineTotal = amount,
                    StockDestination = lineRequest.StockDestination.ToString()
                });

                if (lineRequest.StockDestination == ReturnStockDestination.RestockApproved)
                {
                    var alreadyRestocked = await context.SaleReturnLines
                        .Where(line => line.SaleLineId == saleLine.Id
                            && line.StockDestination == nameof(ReturnStockDestination.RestockApproved))
                        .SumAsync(line => (int?)line.Quantity, cancellationToken) ?? 0;
                    await RestockReturnAsync(
                        context,
                        sale,
                        saleLine,
                        lineRequest.Quantity,
                        alreadyRestocked,
                        saleReturn.Reference,
                        now,
                        cancellationToken);
                }
            }

            if (request.RefundMethod == ReturnRefundMethod.CreditReduction
                && sale.CustomerId is not null)
            {
                var previousCreditReductions = await context.SaleReturns.AsNoTracking()
                    .Where(ret => ret.OriginalSaleId == sale.Id
                        && ret.RefundMethod == nameof(ReturnRefundMethod.CreditReduction))
                    .SumAsync(ret => (decimal?)ret.RefundAmount, cancellationToken) ?? 0m;
                var remainingSaleCredit = sale.CreditTotal - previousCreditReductions;
                var customerBalance = await ComputeBalanceAsync(
                    context,
                    sale.CustomerId.Value,
                    cancellationToken);
                if (saleReturn.RefundAmount > remainingSaleCredit
                    || saleReturn.RefundAmount > customerBalance)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<SaleReturnView>.Failure(SalesApplicationErrors.ReturnInvalid);
                }
            }

            context.SaleReturns.Add(saleReturn);

            if (request.RefundMethod == ReturnRefundMethod.Cash && saleReturn.RefundAmount > 0m)
            {
                context.CashLedgerEntries.Add(new CashLedgerEntryEntity
                {
                    OperationId = DeterministicGuid(request.OperationId, "cash-refund"),
                    EntryType = "RefundCashOut",
                    Amount = -saleReturn.RefundAmount,
                    SourceType = "SaleReturn",
                    SourceReference = saleReturn.Reference,
                    OccurredAtUtc = now,
                    CreatedAtUtc = now
                });
            }
            else if (request.RefundMethod == ReturnRefundMethod.CreditReduction
                && sale.CustomerId is not null
                && saleReturn.RefundAmount > 0m)
            {
                context.CustomerLedgerEntries.Add(new CustomerLedgerEntryEntity
                {
                    OperationId = DeterministicGuid(request.OperationId, "credit-return"),
                    CustomerId = sale.CustomerId.Value,
                    EntryType = "RefundImpact",
                    Amount = saleReturn.RefundAmount,
                    SourceType = "SaleReturn",
                    SourceReference = saleReturn.Reference,
                    OccurredAtUtc = now,
                    CreatedAtUtc = now
                });
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<SaleReturnView>.Success(MapReturn(saleReturn));
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<SaleReturnView>.Failure(SalesApplicationErrors.ReturnConflict);
        }
        catch (Exception exception) when (SalesSqlFailure.IsConcurrencyConflict(exception))
        {
            return Result<SaleReturnView>.Failure(SalesApplicationErrors.ReturnConflict);
        }
        catch (Exception exception) when (SalesSqlFailure.IsUniqueViolation(exception))
        {
            return await ResolveReturnRaceAsync(request, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<SaleReturnView>.Failure(SalesApplicationErrors.ReturnConflict);
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<SaleReturnView>.Failure(SalesApplicationErrors.DatabaseUnavailable);
        }
    }

    private static async Task RestockReturnAsync(
        PharmacyDbContext context,
        SaleEntity sale,
        SaleLineEntity saleLine,
        int quantity,
        int alreadyRestocked,
        string returnReference,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var remaining = quantity;
        var skip = alreadyRestocked;
        var allocations = saleLine.Allocations.OrderBy(allocation => allocation.Id).ToArray();

        for (var index = 0; index < allocations.Length && remaining > 0; index++)
        {
            var allocation = allocations[index];
            if (skip >= allocation.Quantity)
            {
                skip -= allocation.Quantity;
                continue;
            }

            var balance = await context.StockBalances
                .Include(candidate => candidate.StockLocation)
                .SingleAsync(candidate => candidate.Id == allocation.StockBalanceId, cancellationToken);
            var returned = Math.Min(remaining, allocation.Quantity - skip);
            skip = 0;
            var before = balance.AvailableQuantity;
            balance.AvailableQuantity += returned;
            balance.UpdatedAtUtc = now;
            balance.StockLocation.Movements.Add(new StockMovementEntity
            {
                OperationId = DeterministicGuid(sale.OperationId, $"return-{returnReference}-{allocation.Id}-{index}"),
                OperationFingerprint = $"RETURN|{returnReference}|{allocation.Id}",
                ProductId = saleLine.ProductId,
                StockLocation = balance.StockLocation,
                StockBatchId = allocation.StockBatchId,
                MovementType = StockMovementType.ManualIncrease,
                Quantity = returned,
                QuantityBefore = before,
                QuantityAfter = balance.AvailableQuantity,
                Reason = "Retour accepté avec réintégration explicite",
                SourceType = "SaleReturn",
                SourceReference = returnReference,
                OccurredAtUtc = now,
                CreatedAtUtc = now
            });
            remaining -= returned;
        }
    }

    private async Task<Result<SaleReturnView>> ResolveReturnRaceAsync(
        ReturnSaleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var replay = await context.SaleReturns.AsNoTracking()
                .Include(ret => ret.Lines)
                .SingleOrDefaultAsync(ret => ret.OperationId == request.OperationId, cancellationToken);
            return replay is not null && IsSameReturn(replay, request)
                ? Result<SaleReturnView>.Success(MapReturn(replay))
                : Result<SaleReturnView>.Failure(SalesApplicationErrors.OperationConflict);
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<SaleReturnView>.Failure(SalesApplicationErrors.DatabaseUnavailable);
        }
    }

    private static async Task<Result<CustomerDetailView>> LoadCustomerAsync(
        PharmacyDbContext context,
        long customerId,
        CancellationToken cancellationToken)
    {
        var customer = await context.Customers.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.CustomerNotFound);
        }

        var entries = await context.CustomerLedgerEntries.AsNoTracking()
            .Where(entry => entry.CustomerId == customerId)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ThenByDescending(entry => entry.Id)
            .ToArrayAsync(cancellationToken);
        var balance = ComputeBalance(entries);
        return Result<CustomerDetailView>.Success(new CustomerDetailView(
            customer.Id,
            customer.Reference,
            customer.FullName,
            customer.Phone,
            customer.Note,
            customer.IsActive,
            customer.CreditAllowed,
            Money.Create(balance).Value,
            customer.CreatedAtUtc,
            customer.UpdatedAtUtc,
            customer.RowVersion,
            Array.AsReadOnly(entries.Select(MapLedger).ToArray())));
    }

    private static async Task<decimal> ComputeBalanceAsync(
        PharmacyDbContext context,
        long customerId,
        CancellationToken cancellationToken)
    {
        var entries = await context.CustomerLedgerEntries.AsNoTracking()
            .Where(entry => entry.CustomerId == customerId)
            .ToArrayAsync(cancellationToken);
        return ComputeBalance(entries);
    }

    private async Task<Result<CustomerDetailView>> ResolveCreditPaymentRaceAsync(
        RegisterCreditPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var replay = await context.CustomerLedgerEntries.AsNoTracking()
                .SingleOrDefaultAsync(entry => entry.OperationId == request.OperationId, cancellationToken);
            if (replay is null
                || !await IsSameCreditPaymentAsync(context, replay, request, cancellationToken))
            {
                return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.OperationConflict);
            }

            return await LoadCustomerAsync(context, replay.CustomerId, cancellationToken);
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<CustomerDetailView>.Failure(CustomerApplicationErrors.DatabaseUnavailable);
        }
    }

    private static async Task<bool> IsSameCreditPaymentAsync(
        PharmacyDbContext context,
        CustomerLedgerEntryEntity replay,
        RegisterCreditPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (replay.CustomerId != request.CustomerId
            || replay.EntryType != "CreditPayment"
            || replay.Amount != request.Amount
            || replay.SourceType != "CreditPayment")
        {
            return false;
        }

        var cashOperationId = DeterministicGuid(request.OperationId, "cash-credit-payment");
        var cashEntry = await context.CashLedgerEntries.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.OperationId == cashOperationId, cancellationToken);
        return request.PaymentMethod switch
        {
            SalePaymentMethod.Cash => cashEntry is not null
                && cashEntry.EntryType == "CreditPaymentCashIn"
                && cashEntry.Amount == request.Amount
                && cashEntry.SourceType == "CreditPayment"
                && cashEntry.SourceReference == replay.SourceReference,
            SalePaymentMethod.ExternalCard => cashEntry is null,
            _ => false
        };
    }

    private static bool IsValidCreditPayment(RegisterCreditPaymentRequest request)
    {
        const decimal maximumSqlMoney = 999_999_999_999_999.9999m;
        return request.OperationId != Guid.Empty
            && request.CustomerId > 0
            && request.Amount > 0m
            && request.Amount <= maximumSqlMoney
            && decimal.Round(request.Amount, 4) == request.Amount
            && Enum.IsDefined(request.PaymentMethod)
            && (request.PaymentMethod == SalePaymentMethod.Cash && !request.ExternalCardConfirmed
                || request.PaymentMethod == SalePaymentMethod.ExternalCard && request.ExternalCardConfirmed);
    }

    private static string CreditPaymentReference(DateTime occurredAtUtc, Guid operationId)
    {
        return $"REG-{occurredAtUtc:yyyyMMdd}-{operationId:N}".ToUpperInvariant();
    }

    private static async Task<Dictionary<long, decimal>> LoadBalancesAsync(
        PharmacyDbContext context,
        IReadOnlyCollection<long> customerIds,
        CancellationToken cancellationToken)
    {
        var entries = await context.CustomerLedgerEntries.AsNoTracking()
            .Where(entry => customerIds.Contains(entry.CustomerId))
            .ToArrayAsync(cancellationToken);
        return entries
            .GroupBy(entry => entry.CustomerId)
            .ToDictionary(group => group.Key, group => ComputeBalance(group));
    }

    private static decimal ComputeBalance(IEnumerable<CustomerLedgerEntryEntity> entries)
    {
        return entries.Sum(entry => entry.EntryType is "CreditSale" or "CreditAdjustmentIncrease"
            ? entry.Amount
            : -entry.Amount);
    }

    private static CustomerSummaryView MapSummary(CustomerEntity customer, decimal balance)
    {
        return new CustomerSummaryView(
            customer.Id,
            customer.Reference,
            customer.FullName,
            customer.Phone,
            customer.IsActive,
            customer.CreditAllowed,
            Money.Create(Math.Max(0m, balance)).Value,
            customer.CreatedAtUtc,
            customer.UpdatedAtUtc);
    }

    private static CustomerLedgerEntryView MapLedger(CustomerLedgerEntryEntity entry)
    {
        return new CustomerLedgerEntryView(
            entry.Id,
            entry.OperationId,
            entry.EntryType,
            Money.Create(entry.Amount).Value,
            entry.SourceType,
            entry.SourceReference,
            entry.OccurredAtUtc);
    }

    private static SuspendedSaleSummaryView MapSuspension(SuspendedSaleEntity sale)
    {
        var lines = sale.Lines
            .OrderBy(line => line.Id)
            .Select(line => new SuspendedSaleLineView(
                ProductId.Create(line.ProductId).Value,
                ProductReference.Create(line.ProductReferenceSnapshot).Value,
                line.ProductNameSnapshot,
                line.PresentationSnapshot,
                Money.Create(line.UnitPrice).Value,
                SaleQuantity.Create(line.Quantity).Value,
                Money.Create(line.LineTotal).Value))
            .ToArray();
        return new SuspendedSaleSummaryView(
            sale.Id,
            sale.OperationId,
            sale.Reference,
            sale.CustomerId,
            sale.CustomerNameSnapshot,
            Money.Create(sale.GrandTotal).Value,
            sale.LineCount,
            sale.SuspendedAtUtc,
            sale.Status,
            sale.RowVersion.ToArray(),
            Array.AsReadOnly(lines));
    }

    private async Task<Result<SuspendedSaleSummaryView>> ResolveSuspensionReplayAsync(
        SuspendSaleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var replay = await context.SuspendedSales.AsNoTracking()
                .Include(sale => sale.Lines)
                .SingleOrDefaultAsync(sale => sale.OperationId == request.OperationId, cancellationToken);
            return replay is not null && IsSameSuspension(replay, request)
                ? Result<SuspendedSaleSummaryView>.Success(MapSuspension(replay))
                : Result<SuspendedSaleSummaryView>.Failure(SalesApplicationErrors.OperationConflict);
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<SuspendedSaleSummaryView>.Failure(SalesApplicationErrors.DatabaseUnavailable);
        }
    }

    private static bool IsSameSuspension(
        SuspendedSaleEntity sale,
        SuspendSaleRequest request)
    {
        if (sale.CustomerId != request.CustomerId || sale.Lines.Count != request.Lines.Count)
        {
            return false;
        }

        return sale.Lines.OrderBy(line => line.Id).Zip(request.Lines).All(pair =>
            pair.First.ProductId == pair.Second.ProductId
            && string.Equals(pair.First.ProductReferenceSnapshot, pair.Second.ProductReference, StringComparison.Ordinal)
            && string.Equals(pair.First.ProductNameSnapshot, pair.Second.ProductName, StringComparison.Ordinal)
            && string.Equals(pair.First.PresentationSnapshot, NormalizeOptional(pair.Second.Presentation), StringComparison.Ordinal)
            && pair.First.UnitPrice == pair.Second.UnitPrice
            && pair.First.Quantity == pair.Second.Quantity);
    }

    private static SaleReturnView MapReturn(SaleReturnEntity saleReturn)
    {
        return new SaleReturnView(
            saleReturn.Id,
            saleReturn.Reference,
            saleReturn.OriginalSaleId,
            Money.Create(saleReturn.RefundAmount).Value,
            Enum.Parse<ReturnRefundMethod>(saleReturn.RefundMethod),
            saleReturn.CompletedAtUtc,
            Array.AsReadOnly(saleReturn.Lines
                .OrderBy(line => line.Id)
                .Select(line => new SaleReturnLineView(
                    line.SaleLineId,
                    ProductId.Create(line.ProductId).Value,
                    line.Quantity,
                    Money.Create(line.UnitPrice).Value,
                    Money.Create(line.LineTotal).Value,
                    Enum.Parse<ReturnStockDestination>(line.StockDestination)))
                .ToArray()));
    }

    private static bool IsSameReturn(SaleReturnEntity saleReturn, ReturnSaleRequest request)
    {
        if (saleReturn.OriginalSaleId != request.SaleId
            || !string.Equals(saleReturn.RefundMethod, request.RefundMethod.ToString(), StringComparison.Ordinal)
            || saleReturn.Lines.Count != request.Lines.Count)
        {
            return false;
        }

        var persisted = saleReturn.Lines.OrderBy(line => line.SaleLineId).ToArray();
        var requested = request.Lines.OrderBy(line => line.SaleLineId).ToArray();
        return persisted.Zip(requested).All(pair =>
            pair.First.SaleLineId == pair.Second.SaleLineId
            && pair.First.Quantity == pair.Second.Quantity
            && string.Equals(
                pair.First.StockDestination,
                pair.Second.StockDestination.ToString(),
                StringComparison.Ordinal));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Guid DeterministicGuid(Guid operationId, string purpose)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes($"{operationId:N}|{purpose}");
        return new Guid(System.Security.Cryptography.SHA256.HashData(bytes).AsSpan(0, 16));
    }
}
