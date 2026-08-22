using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Dashboard.Contracts;
using Pharmacy.Application.Dashboard.Errors;
using Pharmacy.Application.Dashboard.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Reporting;

public sealed class SqlDashboardService : IDashboardService
{
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;
    private readonly IPermissionAuthorizer authorizer;
    private readonly ISecurityClock clock;

    public SqlDashboardService(
        IDbContextFactory<PharmacyDbContext> contextFactory,
        IPermissionAuthorizer authorizer,
        ISecurityClock clock)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<DashboardSalesView>> GetGlobalSalesAsync(
        DashboardSalesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query is null || !query.IsValid)
        {
            return Result<DashboardSalesView>.Failure(DashboardErrors.InvalidSalesQuery);
        }

        var authorization = authorizer.Authorize(SecurityPermissions.ReportsView);
        if (authorization.IsFailure)
        {
            return Result<DashboardSalesView>.Failure(authorization.Error);
        }

        try
        {
            var nowUtc = clock.UtcNow;
            var todayUtc = DateTime.SpecifyKind(nowUtc.Date, DateTimeKind.Utc);
            var tomorrowUtc = todayUtc.AddDays(1);
            var trendFromUtc = todayUtc.AddDays(-(query.Days - 1));
            var trendPeriod = new DashboardPeriod(trendFromUtc, tomorrowUtc);

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var today = await context.Sales.AsNoTracking()
                .Where(sale => sale.CompletedAtUtc >= todayUtc && sale.CompletedAtUtc < tomorrowUtc)
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Count = group.Count(),
                    Amount = group.Sum(sale => sale.GrandTotal)
                })
                .SingleOrDefaultAsync(cancellationToken);

            var dailyValues = await context.Sales.AsNoTracking()
                .Where(sale => sale.CompletedAtUtc >= trendFromUtc && sale.CompletedAtUtc < tomorrowUtc)
                .GroupBy(sale => sale.CompletedAtUtc.Date)
                .Select(group => new
                {
                    Day = group.Key,
                    Count = group.Count(),
                    Amount = group.Sum(sale => sale.GrandTotal)
                })
                .ToDictionaryAsync(item => item.Day, cancellationToken);

            var trend = Enumerable.Range(0, query.Days)
                .Select(offset => trendFromUtc.AddDays(offset))
                .Select(day => dailyValues.TryGetValue(day, out var value)
                    ? new DashboardDailySalesPoint(
                        DateOnly.FromDateTime(day),
                        value.Count,
                        value.Amount)
                    : new DashboardDailySalesPoint(DateOnly.FromDateTime(day), 0, 0m))
                .ToArray();

            var topRows = await context.Database.SqlQuery<DashboardTopProductSqlRow>($"""
                SELECT TOP ({query.TopProductLimit})
                       ProductId,
                       MAX(ProductNameSnapshot) AS ProductName,
                       SUM(SoldQuantity) AS SoldQuantity,
                       SUM(GrossAmount) AS GrossSales
                FROM reporting.vw_ProductSales
                WHERE CompletedAtUtc >= {trendFromUtc} AND CompletedAtUtc < {tomorrowUtc}
                GROUP BY ProductId
                ORDER BY SUM(SoldQuantity) DESC, SUM(GrossAmount) DESC, ProductId
                """)
                .ToArrayAsync(cancellationToken);

            var topProducts = topRows.Select(item => new DashboardTopProductView(
                item.ProductId,
                item.ProductName,
                item.SoldQuantity,
                item.GrossSales)).ToArray();

            return Result<DashboardSalesView>.Success(new DashboardSalesView(
                trendPeriod,
                nowUtc,
                today?.Count ?? 0,
                today?.Amount ?? 0m,
                Array.AsReadOnly(trend),
                Array.AsReadOnly(topProducts)));
        }
        catch (Exception exception) when (ReportingSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<DashboardSalesView>.Failure(DashboardErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<DashboardCreditView>> GetOpenCreditAsync(
        CancellationToken cancellationToken = default)
    {
        var authorization = authorizer.Authorize(SecurityPermissions.CustomersManageCredit);
        if (authorization.IsFailure)
        {
            return Result<DashboardCreditView>.Failure(authorization.Error);
        }

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var openCredit = await context.Database.SqlQuery<decimal>($"""
                SELECT COALESCE(SUM(CASE WHEN OpenBalance > 0 THEN OpenBalance ELSE 0 END), 0) AS Value
                FROM reporting.vw_CustomerCredit
                """).SingleAsync(cancellationToken);
            return Result<DashboardCreditView>.Success(new DashboardCreditView(openCredit));
        }
        catch (Exception exception) when (ReportingSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<DashboardCreditView>.Failure(DashboardErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<DashboardInventoryView>> GetInventoryAlertsAsync(
        int expiryLimit = 6,
        CancellationToken cancellationToken = default)
    {
        if (expiryLimit is < 1 or > 20)
        {
            return Result<DashboardInventoryView>.Failure(DashboardErrors.InvalidExpiryLimit);
        }

        var authorization = authorizer.Authorize(SecurityPermissions.InventoryView);
        if (authorization.IsFailure)
        {
            return Result<DashboardInventoryView>.Failure(authorization.Error);
        }

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var settings = await context.ReportSettings.AsNoTracking().SingleAsync(cancellationToken);
            var lowStockProducts = settings.LowStockAlertsEnabled
                ? await context.Database.SqlQuery<int>($"""
                    SELECT COUNT(*) AS Value
                    FROM reporting.vw_StockSummary
                    WHERE MinimumStock IS NOT NULL AND SellableQuantity < MinimumStock
                    """).SingleAsync(cancellationToken)
                : 0;

            var horizon = settings.ExpiryAlertsEnabled ? settings.ExpiryHorizonDays : null;
            var expiryRows = await context.Database.SqlQuery<DashboardExpirySqlRow>($"""
                SELECT TOP ({expiryLimit})
                       BatchId,
                       ProductId,
                       ProductName,
                       BatchNumber,
                       ExpiryDate,
                       Quantity,
                       DaysRemaining
                FROM reporting.vw_ExpiryAlerts
                WHERE DaysRemaining < 0
                   OR ({horizon} IS NOT NULL AND DaysRemaining BETWEEN 0 AND {horizon})
                ORDER BY DaysRemaining, BatchId
                """)
                .ToArrayAsync(cancellationToken);

            var expiry = expiryRows.Select(item => new DashboardExpiryAlertView(
                item.BatchId,
                item.ProductId,
                item.ProductName,
                item.BatchNumber,
                DateOnly.FromDateTime(item.ExpiryDate),
                item.Quantity,
                item.DaysRemaining,
                item.DaysRemaining < 0
                    ? "Périmé"
                    : item.DaysRemaining <= 7 ? "Très proche" : "Dans l’horizon"))
                .ToArray();

            return Result<DashboardInventoryView>.Success(new DashboardInventoryView(
                settings.LowStockAlertsEnabled,
                lowStockProducts,
                settings.ExpiryAlertsEnabled,
                settings.ExpiryHorizonDays,
                Array.AsReadOnly(expiry)));
        }
        catch (Exception exception) when (ReportingSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<DashboardInventoryView>.Failure(DashboardErrors.DatabaseUnavailable);
        }
    }

    private sealed class DashboardTopProductSqlRow
    {
        public long ProductId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public int SoldQuantity { get; init; }
        public decimal GrossSales { get; init; }
    }

    private sealed class DashboardExpirySqlRow
    {
        public long BatchId { get; init; }
        public long ProductId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public string? BatchNumber { get; init; }
        public DateTime ExpiryDate { get; init; }
        public long Quantity { get; init; }
        public int DaysRemaining { get; init; }
    }
}
