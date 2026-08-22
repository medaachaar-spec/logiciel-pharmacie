using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Sales;

namespace Pharmacy.Infrastructure.Sales;

public sealed class SqlSalesReader : ISalesReader, ISaleReturnReader
{
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;

    public SqlSalesReader(IDbContextFactory<PharmacyDbContext> contextFactory)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<Result<FinalizedSaleView>> GetByIdAsync(
        long saleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await LoadByIdAsync(context, saleId, cancellationToken);
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<FinalizedSaleView>.Failure(SalesApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<FinalizedSaleView>> GetByIdForUserAsync(
        long saleId,
        long userAccountId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await LoadByIdAsync(context, saleId, cancellationToken, userAccountId);
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<FinalizedSaleView>.Failure(SalesApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<IReadOnlyList<FinalizedSaleSummaryView>>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await GetRecentCoreAsync(limit, null, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<FinalizedSaleSummaryView>>> GetRecentForUserAsync(
        int limit,
        long userAccountId,
        CancellationToken cancellationToken = default)
    {
        return await GetRecentCoreAsync(limit, userAccountId, cancellationToken);
    }

    private async Task<Result<IReadOnlyList<FinalizedSaleSummaryView>>> GetRecentCoreAsync(
        int limit,
        long? userAccountId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.Sales
                .AsNoTracking()
                .Where(sale => userAccountId == null || sale.FinalizedByUserAccountId == userAccountId);
            var rows = await query
                .OrderByDescending(sale => sale.CompletedAtUtc)
                .ThenByDescending(sale => sale.Id)
                .Take(limit)
                .Select(sale => new
                {
                    sale.Id,
                    sale.Reference,
                    sale.CompletedAtUtc,
                    sale.LineCount,
                    sale.TotalQuantity,
                sale.GrandTotal,
                sale.CreditTotal,
                sale.CustomerId,
                sale.PaymentMethod,
                    sale.Status
                })
                .ToArrayAsync(cancellationToken);

            var summaries = rows.Select(row => new FinalizedSaleSummaryView(
                row.Id,
                row.Reference,
                row.CompletedAtUtc,
                row.LineCount,
                row.TotalQuantity,
                Money.Create(row.GrandTotal).Value,
                row.PaymentMethod,
                row.Status)).ToArray();
            return Result<IReadOnlyList<FinalizedSaleSummaryView>>.Success(Array.AsReadOnly(summaries));
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<IReadOnlyList<FinalizedSaleSummaryView>>.Failure(
                SalesApplicationErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<SaleReturnPreparationView>> GetPreparationAsync(
        long saleId,
        CancellationToken cancellationToken = default)
    {
        return await GetPreparationCoreAsync(saleId, null, cancellationToken);
    }

    public async Task<Result<SaleReturnPreparationView>> GetPreparationForUserAsync(
        long saleId,
        long userAccountId,
        CancellationToken cancellationToken = default)
    {
        return await GetPreparationCoreAsync(saleId, userAccountId, cancellationToken);
    }

    private async Task<Result<SaleReturnPreparationView>> GetPreparationCoreAsync(
        long saleId,
        long? userAccountId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var sale = await context.Sales
                .AsNoTracking()
                .Include(candidate => candidate.Lines)
                .SingleOrDefaultAsync(
                    candidate => candidate.Id == saleId
                        && (userAccountId == null || candidate.FinalizedByUserAccountId == userAccountId),
                    cancellationToken);

            if (sale is null)
            {
                return Result<SaleReturnPreparationView>.Failure(SalesApplicationErrors.SaleNotFound);
            }

            var previousReturns = await context.SaleReturns
                .AsNoTracking()
                .AsSplitQuery()
                .Include(candidate => candidate.Lines)
                .Where(candidate => candidate.OriginalSaleId == saleId && candidate.Status == "Completed")
                .OrderByDescending(candidate => candidate.CompletedAtUtc)
                .ThenByDescending(candidate => candidate.Id)
                .ToArrayAsync(cancellationToken);

            var returnedByLine = previousReturns
                .SelectMany(candidate => candidate.Lines)
                .GroupBy(line => line.SaleLineId)
                .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));
            var lines = sale.Lines
                .OrderBy(line => line.Id)
                .Select(line =>
                {
                    var returned = returnedByLine.GetValueOrDefault(line.Id);
                    return new SaleReturnableLineView(
                        line.Id,
                        ProductId.Create(line.ProductId).Value,
                        ProductReference.Create(line.ProductReferenceSnapshot).Value,
                        line.ProductNameSnapshot,
                        line.PresentationSnapshot,
                        Money.Create(line.UnitPrice).Value,
                        line.Quantity,
                        returned,
                        Math.Max(0, line.Quantity - returned));
                })
                .ToArray();
            var returns = previousReturns.Select(MapReturn).ToArray();

            return Result<SaleReturnPreparationView>.Success(new SaleReturnPreparationView(
                sale.Id,
                sale.Reference,
                sale.CompletedAtUtc,
                sale.PaymentMethod,
                Money.Create(sale.GrandTotal).Value,
                sale.CustomerId,
                Money.Create(sale.CreditTotal).Value,
                Array.AsReadOnly(lines),
                Array.AsReadOnly(returns)));
        }
        catch (Exception exception) when (SalesSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<SaleReturnPreparationView>.Failure(SalesApplicationErrors.DatabaseUnavailable);
        }
    }

    internal static async Task<Result<FinalizedSaleView>> LoadByIdAsync(
        PharmacyDbContext context,
        long saleId,
        CancellationToken cancellationToken,
        long? userAccountId = null)
    {
        var sale = await context.Sales
            .AsNoTracking()
            .AsSplitQuery()
            .Include(candidate => candidate.Lines)
                .ThenInclude(line => line.Allocations)
            .Include(candidate => candidate.Payments)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == saleId
                    && (userAccountId == null || candidate.FinalizedByUserAccountId == userAccountId),
                cancellationToken);

        return sale is null
            ? Result<FinalizedSaleView>.Failure(SalesApplicationErrors.SaleNotFound)
            : Result<FinalizedSaleView>.Success(Map(sale));
    }

    internal static FinalizedSaleView Map(SaleEntity sale)
    {
        var payment = sale.Payments.Single();
        var lines = sale.Lines
            .OrderBy(line => line.Id)
            .Select(line => new FinalizedSaleLineView(
                line.Id,
                ProductId.Create(line.ProductId).Value,
                ProductReference.Create(line.ProductReferenceSnapshot).Value,
                line.ProductNameSnapshot,
                line.PresentationSnapshot,
                Money.Create(line.UnitPrice).Value,
                SaleQuantity.Create(line.Quantity).Value,
                Money.Create(line.LineTotal).Value,
                line.Allocations
                    .OrderBy(allocation => allocation.Id)
                    .Select(allocation => new SaleAllocationView(
                        allocation.Id,
                        allocation.StockBalanceId,
                        allocation.StockBatchId,
                        allocation.BatchNumberSnapshot,
                        allocation.ExpiryDateSnapshot,
                        allocation.Quantity))));

        return new FinalizedSaleView(
            sale.Id,
            sale.Reference,
            sale.OperationId,
            sale.CompletedAtUtc,
            Money.Create(sale.GrandTotal).Value,
            sale.TotalQuantity,
            lines,
            new SalePaymentView(
                payment.Id,
                payment.PaymentMethod,
                Money.Create(payment.Amount).Value,
                payment.TenderedAmount,
                payment.ChangeGiven,
                payment.ExternalCardConfirmed,
                payment.ConfirmedAtUtc),
            sale.CustomerId,
            Money.Create(sale.CreditTotal).Value);
    }

    private static SaleReturnView MapReturn(SaleReturnEntity saleReturn) => new(
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
