using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Sales;

namespace Pharmacy.Infrastructure.Sales;

public sealed class SqlSalesReader : ISalesReader
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

    public async Task<Result<IReadOnlyList<FinalizedSaleSummaryView>>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var rows = await context.Sales
                .AsNoTracking()
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

    internal static async Task<Result<FinalizedSaleView>> LoadByIdAsync(
        PharmacyDbContext context,
        long saleId,
        CancellationToken cancellationToken)
    {
        var sale = await context.Sales
            .AsNoTracking()
            .AsSplitQuery()
            .Include(candidate => candidate.Lines)
                .ThenInclude(line => line.Allocations)
            .Include(candidate => candidate.Payments)
            .SingleOrDefaultAsync(candidate => candidate.Id == saleId, cancellationToken);

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
}
