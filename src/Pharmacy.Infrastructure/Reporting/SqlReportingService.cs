using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Reporting.Contracts;
using Pharmacy.Application.Reporting.Errors;
using Pharmacy.Application.Reporting.Exports;
using Pharmacy.Application.Reporting.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Reporting;

public sealed class SqlReportingService : IReportingService
{
    private const string CurrencyCode = "MAD";
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;
    private readonly IPermissionAuthorizer authorizer;
    private readonly IBusinessAuditRecorder audit;
    private readonly ISecurityClock clock;

    public SqlReportingService(
        IDbContextFactory<PharmacyDbContext> contextFactory,
        IPermissionAuthorizer authorizer,
        IBusinessAuditRecorder audit,
        ISecurityClock clock)
    {
        this.contextFactory = contextFactory;
        this.authorizer = authorizer;
        this.audit = audit;
        this.clock = clock;
    }

    public async Task<Result<ReportingSnapshotView>> GetSnapshotAsync(
        ReportingQuery query,
        CancellationToken cancellationToken = default)
    {
        return await GetSnapshotAsync(query, query.Limit, cancellationToken);
    }

    private async Task<Result<ReportingSnapshotView>> GetSnapshotAsync(
        ReportingQuery query,
        int? limit,
        CancellationToken cancellationToken)
    {
        var validation = ValidateQuery(query);
        if (validation is not null) return Result<ReportingSnapshotView>.Failure(validation);
        var authorization = authorizer.Authorize(SecurityPermissions.ReportsView);
        if (authorization.IsFailure) return Result<ReportingSnapshotView>.Failure(authorization.Error);

        try
        {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var period = query.Period;
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var settingsEntity = await context.ReportSettings.AsNoTracking().SingleAsync(cancellationToken);
        var settings = MapSettings(settingsEntity);

        var saleData = await context.Sales.AsNoTracking()
            .Where(sale => sale.CompletedAtUtc >= period.FromUtc && sale.CompletedAtUtc < period.ToUtc)
            .OrderByDescending(sale => sale.CompletedAtUtc).ThenByDescending(sale => sale.Id)
            .Select(sale => new
            {
                sale.Id, sale.Reference, sale.CompletedAtUtc, sale.PaymentMethod, sale.LineCount,
                sale.TotalQuantity, sale.GrandTotal, sale.PaidTotal, sale.CreditTotal,
                RefundAmount = context.SaleReturns
                    .Where(item => item.OriginalSaleId == sale.Id && item.Status == "Completed")
                    .Sum(item => (decimal?)item.RefundAmount) ?? 0m
            })
            .LimitForInteractiveView(limit)
            .ToArrayAsync(cancellationToken);
        var sales = saleData.Select(item => new SaleReportRow(
            item.Id, item.Reference, item.CompletedAtUtc, item.PaymentMethod.ToString(), item.LineCount,
            item.TotalQuantity, item.GrandTotal, item.PaidTotal, item.CreditTotal,
            item.RefundAmount, item.GrandTotal - item.RefundAmount)).ToArray();

        var lineData = await context.SaleLines.AsNoTracking()
            .Where(line => line.Sale.CompletedAtUtc >= period.FromUtc && line.Sale.CompletedAtUtc < period.ToUtc)
            .OrderByDescending(line => line.Sale.CompletedAtUtc).ThenByDescending(line => line.Id)
            .Select(line => new
            {
                line.Id, line.Sale.CompletedAtUtc, line.Sale.Reference, line.ProductId,
                line.ProductReferenceSnapshot, line.ProductNameSnapshot, line.Quantity, line.LineTotal,
                ReturnedQuantity = context.SaleReturnLines
                    .Where(item => item.SaleLineId == line.Id && item.SaleReturn.Status == "Completed")
                    .Sum(item => (int?)item.Quantity) ?? 0,
                RefundAmount = context.SaleReturnLines
                    .Where(item => item.SaleLineId == line.Id && item.SaleReturn.Status == "Completed")
                    .Sum(item => (decimal?)item.LineTotal) ?? 0m
            })
            .LimitForInteractiveView(limit)
            .ToArrayAsync(cancellationToken);
        var productSales = lineData.Select(item => new ProductSalesReportRow(
            item.Id, item.CompletedAtUtc, item.Reference, item.ProductId,
            item.ProductReferenceSnapshot, item.ProductNameSnapshot, item.Quantity,
            item.ReturnedQuantity, item.LineTotal, item.RefundAmount, item.LineTotal - item.RefundAmount)).ToArray();

        var stockData = await context.ProductLocalSettings.AsNoTracking()
            .Where(local => local.Product.IsActive && local.IsActive && local.IsSellable)
            .OrderBy(local => local.Product.Name).ThenBy(local => local.ProductId)
            .Select(local => new
            {
                local.ProductId,
                Reference = local.InternalCode ?? string.Empty,
                Name = local.Product.Name,
                local.MinimumStock,
                Quantity = context.StockBalances
                    .Where(balance => balance.ProductId == local.ProductId
                        && balance.AvailableQuantity > 0
                        && (balance.StockBatchId == null
                            || (balance.StockBatch!.IsActive
                                && (balance.StockBatch.ExpiryDate == null || balance.StockBatch.ExpiryDate >= today))))
                    .Sum(balance => (long?)balance.AvailableQuantity) ?? 0L
            })
            .LimitForInteractiveView(limit)
            .ToArrayAsync(cancellationToken);
        var stock = stockData.Select(item => new StockReportRow(
            item.ProductId, item.Reference, item.Name, item.Quantity, item.MinimumStock,
            item.Quantity <= 0 ? "Rupture" : item.MinimumStock.HasValue && item.Quantity < item.MinimumStock.Value
                ? "Sous seuil configuré" : "Disponible")).ToArray();

        var expiryData = await context.StockBatches.AsNoTracking()
            .Where(batch => batch.IsActive && batch.ExpiryDate != null)
            .OrderBy(batch => batch.ExpiryDate).ThenBy(batch => batch.Id)
            .Select(batch => new
            {
                batch.Id, batch.ProductId,
                Reference = batch.Product.LocalSettings != null ? batch.Product.LocalSettings.InternalCode ?? string.Empty : string.Empty,
                Name = batch.Product.Name, batch.BatchNumber, batch.ExpiryDate,
                Quantity = batch.Balances.Sum(balance => (long?)balance.AvailableQuantity) ?? 0L
            })
            .Where(item => item.Quantity > 0)
            .LimitForInteractiveView(limit)
            .ToArrayAsync(cancellationToken);
        var expiry = expiryData.Select(item =>
        {
            var days = item.ExpiryDate.HasValue ? item.ExpiryDate.Value.DayNumber - today.DayNumber : (int?)null;
            var status = days < 0 ? "Périmé" : settings.ExpiryAlertsEnabled && settings.ExpiryHorizonDays.HasValue
                && days <= settings.ExpiryHorizonDays ? "Dans l’horizon configuré" : "Valide";
            return new ExpiryReportRow(item.Id, item.ProductId, item.Reference, item.Name,
                item.BatchNumber, item.ExpiryDate, item.Quantity, days, status);
        }).ToArray();

        var receiptData = await context.GoodsReceipts.AsNoTracking()
            .Where(receipt => receipt.ReceivedAtUtc >= period.FromUtc && receipt.ReceivedAtUtc < period.ToUtc)
            .OrderByDescending(receipt => receipt.ReceivedAtUtc).ThenByDescending(receipt => receipt.Id)
            .Select(receipt => new
            {
                receipt.Id, receipt.ReceiptNumber, receipt.ReceivedAtUtc,
                SupplierCode = receipt.Supplier.Code, SupplierName = receipt.Supplier.Name,
                receipt.ReceiptType,
                Quantity = receipt.Lines.Sum(line => (long?)line.Quantity) ?? 0L,
                Amount = receipt.Lines.Sum(line => (decimal?)(line.Quantity * line.UnitPurchasePrice)) ?? 0m
            })
            .LimitForInteractiveView(limit)
            .ToArrayAsync(cancellationToken);
        var purchasing = receiptData.Select(item => new PurchasingReportRow(
            item.Id, item.ReceiptNumber, item.ReceivedAtUtc, item.SupplierCode, item.SupplierName,
            item.ReceiptType.ToString(), item.Quantity, item.Amount)).ToArray();

        var movementTypes = new[]
        {
            StockMovementType.ManualIncrease, StockMovementType.ManualDecrease,
            StockMovementType.InventoryIncrease, StockMovementType.InventoryDecrease
        };
        var movementData = await context.StockMovements.AsNoTracking()
            .Where(item => item.OccurredAtUtc >= period.FromUtc && item.OccurredAtUtc < period.ToUtc
                && movementTypes.Contains(item.MovementType))
            .OrderByDescending(item => item.OccurredAtUtc).ThenByDescending(item => item.Id)
            .Select(item => new
            {
                item.Id, item.OccurredAtUtc, item.ProductId,
                Reference = item.Product.LocalSettings != null ? item.Product.LocalSettings.InternalCode ?? string.Empty : string.Empty,
                Name = item.Product.Name, item.MovementType, item.Quantity, item.QuantityBefore,
                item.QuantityAfter, item.Reason, item.SourceReference
            })
            .LimitForInteractiveView(limit)
            .ToArrayAsync(cancellationToken);
        var adjustments = movementData.Select(item => new InventoryAdjustmentReportRow(
            item.Id, item.OccurredAtUtc, item.ProductId, item.Reference, item.Name,
            item.MovementType.ToString(), item.Quantity, item.QuantityBefore, item.QuantityAfter,
            item.Reason, item.SourceReference)).ToArray();

        var customerData = await context.Customers.AsNoTracking()
            .OrderBy(customer => customer.FullName).ThenBy(customer => customer.Id)
            .Select(customer => new
            {
                customer.Id, customer.Reference, customer.FullName, customer.CreditAllowed,
                Generated = customer.LedgerEntries
                    .Where(entry => entry.EntryType == "CreditSale" || entry.EntryType == "CreditAdjustmentIncrease")
                    .Sum(entry => (decimal?)entry.Amount) ?? 0m,
                Paid = customer.LedgerEntries
                    .Where(entry => entry.EntryType == "CreditPayment" || entry.EntryType == "CreditAdjustmentDecrease" || entry.EntryType == "RefundImpact")
                    .Sum(entry => (decimal?)entry.Amount) ?? 0m
            })
            .LimitForInteractiveView(limit)
            .ToArrayAsync(cancellationToken);
        var customerCredit = customerData.Select(item => new CustomerCreditReportRow(
            item.Id, item.Reference, item.FullName, item.CreditAllowed,
            item.Generated, item.Paid, Math.Max(0m, item.Generated - item.Paid))).ToArray();

        var returnData = await context.SaleReturns.AsNoTracking()
            .Where(item => item.CompletedAtUtc >= period.FromUtc && item.CompletedAtUtc < period.ToUtc)
            .OrderByDescending(item => item.CompletedAtUtc).ThenByDescending(item => item.Id)
            .Select(item => new
            {
                item.Id, item.Reference, item.CompletedAtUtc,
                SaleReference = context.Sales.Where(sale => sale.Id == item.OriginalSaleId).Select(sale => sale.Reference).Single(),
                item.RefundMethod, item.RefundAmount,
                Quantity = item.Lines.Sum(line => (int?)line.Quantity) ?? 0,
                Restocked = item.Lines.Where(line => line.StockDestination == "RestockApproved").Sum(line => (int?)line.Quantity) ?? 0
            })
            .LimitForInteractiveView(limit)
            .ToArrayAsync(cancellationToken);
        var returns = returnData.Select(item => new ReturnReportRow(
            item.Id, item.Reference, item.CompletedAtUtc, item.SaleReference, item.RefundMethod,
            item.RefundAmount, item.Quantity, item.Restocked)).ToArray();

        var cash = await context.CashLedgerEntries.AsNoTracking()
            .Where(item => item.OccurredAtUtc >= period.FromUtc && item.OccurredAtUtc < period.ToUtc)
            .OrderByDescending(item => item.OccurredAtUtc).ThenByDescending(item => item.Id)
            .Select(item => new CashReportRow(item.Id, item.OccurredAtUtc, item.EntryType,
                item.Amount, item.SourceType, item.SourceReference))
            .LimitForInteractiveView(limit)
            .ToArrayAsync(cancellationToken);

        var payments = await context.Payments.AsNoTracking()
            .Where(item => item.ConfirmedAtUtc >= period.FromUtc && item.ConfirmedAtUtc < period.ToUtc && item.Status == "Completed")
            .GroupBy(item => item.PaymentMethod)
            .Select(group => new { Method = group.Key, Amount = group.Sum(item => item.Amount), Count = group.Count() })
            .ToArrayAsync(cancellationToken);
        var grossSales = await context.Sales.AsNoTracking()
            .Where(item => item.CompletedAtUtc >= period.FromUtc && item.CompletedAtUtc < period.ToUtc)
            .SumAsync(item => (decimal?)item.GrandTotal, cancellationToken) ?? 0m;
        var saleCount = await context.Sales.AsNoTracking()
            .CountAsync(item => item.CompletedAtUtc >= period.FromUtc && item.CompletedAtUtc < period.ToUtc, cancellationToken);
        var refundAmount = await context.SaleReturns.AsNoTracking()
            .Where(item => item.CompletedAtUtc >= period.FromUtc && item.CompletedAtUtc < period.ToUtc && item.Status == "Completed")
            .SumAsync(item => (decimal?)item.RefundAmount, cancellationToken) ?? 0m;
        var returnCount = await context.SaleReturns.AsNoTracking()
            .CountAsync(item => item.CompletedAtUtc >= period.FromUtc && item.CompletedAtUtc < period.ToUtc && item.Status == "Completed", cancellationToken);
        var openCredit = await context.Database.SqlQuery<decimal>($"""
            SELECT COALESCE(SUM(CASE WHEN OpenBalance > 0 THEN OpenBalance ELSE 0 END), 0) AS Value
            FROM reporting.vw_CustomerCredit
            """).SingleAsync(cancellationToken);
        var creditPayments = await context.CustomerLedgerEntries.AsNoTracking()
            .Where(item => item.OccurredAtUtc >= period.FromUtc && item.OccurredAtUtc < period.ToUtc && item.EntryType == "CreditPayment")
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var purchaseOrders = await context.PurchaseOrders.AsNoTracking()
            .CountAsync(item => item.CreatedAtUtc >= period.FromUtc && item.CreatedAtUtc < period.ToUtc, cancellationToken);
        var receivedQuantity = await context.GoodsReceiptLines.AsNoTracking()
            .Where(item => item.GoodsReceipt.ReceivedAtUtc >= period.FromUtc && item.GoodsReceipt.ReceivedAtUtc < period.ToUtc)
            .SumAsync(item => (long?)item.Quantity, cancellationToken) ?? 0L;
        var currentSellableStock = await context.Database.SqlQuery<long>($"""
            SELECT COALESCE(SUM(SellableQuantity), 0) AS Value FROM reporting.vw_StockSummary
            """).SingleAsync(cancellationToken);
        var outOfStockProducts = await context.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM reporting.vw_StockSummary WHERE SellableQuantity <= 0
            """).SingleAsync(cancellationToken);
        var lowStockProducts = settings.LowStockAlertsEnabled
            ? await context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM reporting.vw_StockSummary
                WHERE MinimumStock IS NOT NULL AND SellableQuantity < MinimumStock
                """).SingleAsync(cancellationToken)
            : 0;
        var expiredBatches = await context.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM reporting.vw_ExpiryAlerts WHERE DaysRemaining < 0
            """).SingleAsync(cancellationToken);
        var nearExpiryBatches = settings.ExpiryAlertsEnabled && settings.ExpiryHorizonDays.HasValue
            ? await context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM reporting.vw_ExpiryAlerts
                WHERE DaysRemaining >= 0 AND DaysRemaining <= {settings.ExpiryHorizonDays.Value}
                """).SingleAsync(cancellationToken)
            : 0;

        var summary = new ExecutiveSummaryView(period, clock.UtcNow, CurrencyCode,
            grossSales, refundAmount, grossSales - refundAmount, saleCount, returnCount,
            payments.Select(item => new PaymentBreakdownView(item.Method.ToString(), item.Amount, item.Count)).ToArray(),
            openCredit, creditPayments, currentSellableStock,
            outOfStockProducts, lowStockProducts, expiredBatches, nearExpiryBatches,
            purchaseOrders, receivedQuantity);

        return Result<ReportingSnapshotView>.Success(new ReportingSnapshotView(
            summary, settings, sales, productSales, stock, expiry, purchasing,
            adjustments, customerCredit, returns, cash));
        }
        catch (Exception exception) when (ReportingSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<ReportingSnapshotView>.Failure(ReportingErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<AlertSettingsView>> UpdateSettingsAsync(
        UpdateAlertSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OperationId == Guid.Empty) return Result<AlertSettingsView>.Failure(ReportingErrors.InvalidOperationId);
        if (request.ExpiryHorizonDays < 0 || (request.ExpiryAlertsEnabled && request.ExpiryHorizonDays is null))
            return Result<AlertSettingsView>.Failure(ReportingErrors.InvalidSettings);
        var authorization = authorizer.Authorize(SecurityPermissions.ReportsManageSettings, true);
        if (authorization.IsFailure) return Result<AlertSettingsView>.Failure(authorization.Error);

        try
        {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.ReportSettings.SingleAsync(cancellationToken);
        context.Entry(entity).Property(item => item.RowVersion).OriginalValue = request.RowVersion;
        entity.LowStockAlertsEnabled = request.LowStockAlertsEnabled;
        entity.ExpiryAlertsEnabled = request.ExpiryAlertsEnabled;
        entity.ExpiryHorizonDays = request.ExpiryHorizonDays;
        entity.UpdatedAtUtc = clock.UtcNow;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<AlertSettingsView>.Failure(new Error(
                "reporting.settings.concurrency", "Les paramètres ont changé ; rechargez-les avant de réessayer."));
        }

        await audit.RecordSuccessAsync("reporting", "ReportSettings", "1", "alerts.settings.update",
            request.OperationId, cancellationToken);
        return Result<AlertSettingsView>.Success(MapSettings(entity));
        }
        catch (Exception exception) when (ReportingSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<AlertSettingsView>.Failure(ReportingErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<CsvExportView>> ExportCsvAsync(
        ReportingExportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OperationId == Guid.Empty) return Result<CsvExportView>.Failure(ReportingErrors.InvalidOperationId);
        var authorization = authorizer.Authorize(SecurityPermissions.ReportsExport);
        if (authorization.IsFailure) return Result<CsvExportView>.Failure(authorization.Error);
        // The reporting screen is intentionally paged, but a CSV advertised as a
        // complete export must query the whole requested dataset.
        var snapshot = await GetSnapshotAsync(request.Query, limit: null, cancellationToken);
        if (snapshot.IsFailure) return Result<CsvExportView>.Failure(snapshot.Error);

        var (headers, rows) = CsvRows(request.Kind, snapshot.Value);
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(';', headers.Select(CsvCell)));
        foreach (var row in rows) builder.AppendLine(string.Join(';', row.Select(CsvCell)));
        var preamble = Encoding.UTF8.GetPreamble();
        var payload = Encoding.UTF8.GetBytes(builder.ToString());
        var content = new byte[preamble.Length + payload.Length];
        Buffer.BlockCopy(preamble, 0, content, 0, preamble.Length);
        Buffer.BlockCopy(payload, 0, content, preamble.Length, payload.Length);
        var fileName = $"pharmacy-{request.Kind.ToString().ToLowerInvariant()}-{clock.UtcNow:yyyyMMdd-HHmmss}.csv";
        await audit.RecordSuccessAsync("reporting", "CsvExport", request.Kind.ToString(), "report.export.csv",
            request.OperationId, cancellationToken);
        return Result<CsvExportView>.Success(new CsvExportView(fileName, "text/csv; charset=utf-8", content));
    }

    private static Error? ValidateQuery(ReportingQuery query)
    {
        if (query is null || query.Period is null || !query.Period.IsValid) return ReportingErrors.InvalidPeriod;
        return query.Limit is < 1 or > 1000 ? ReportingErrors.InvalidLimit : null;
    }

    private static AlertSettingsView MapSettings(Persistence.Entities.Reporting.ReportSettingsEntity entity) =>
        new(entity.LowStockAlertsEnabled, entity.ExpiryAlertsEnabled, entity.ExpiryHorizonDays,
            entity.UpdatedAtUtc, entity.RowVersion.ToArray());

    private static string CsvCell(string? value)
    {
        var safe = CsvExportSecurity.NeutralizeFormula(value);
        return '"' + safe.Replace("\"", "\"\"") + '"';
    }

    private static string Number(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    private static string Date(DateTime value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static (string[] Headers, IEnumerable<string[]> Rows) CsvRows(
        ReportingExportKind kind,
        ReportingSnapshotView snapshot) => kind switch
    {
        ReportingExportKind.Sales =>
            (["SaleId", "Reference", "CompletedAtUtc", "PaymentMethod", "Quantity", "GrossAmountMAD", "RefundAmountMAD", "NetAmountMAD"],
             snapshot.Sales.Select(item => new[] { item.SaleId.ToString(CultureInfo.InvariantCulture), item.Reference, Date(item.CompletedAtUtc), item.PaymentMethod, item.Quantity.ToString(CultureInfo.InvariantCulture), Number(item.GrossAmount), Number(item.RefundAmount), Number(item.NetAmount) })),
        ReportingExportKind.ProductSales =>
            (["SaleLineId", "CompletedAtUtc", "SaleReference", "ProductReference", "ProductName", "SoldQuantity", "ReturnedQuantity", "GrossAmountMAD", "RefundAmountMAD", "NetAmountMAD"],
             snapshot.ProductSales.Select(item => new[] { item.SaleLineId.ToString(CultureInfo.InvariantCulture), Date(item.CompletedAtUtc), item.SaleReference, item.ProductReference, item.ProductName, item.SoldQuantity.ToString(CultureInfo.InvariantCulture), item.ReturnedQuantity.ToString(CultureInfo.InvariantCulture), Number(item.GrossAmount), Number(item.RefundAmount), Number(item.NetAmount) })),
        ReportingExportKind.Stock =>
            (["ProductId", "ProductReference", "ProductName", "SellableQuantity", "MinimumStockConfigured", "Status"],
             snapshot.Stock.Select(item => new[] { item.ProductId.ToString(CultureInfo.InvariantCulture), item.ProductReference, item.ProductName, item.SellableQuantity.ToString(CultureInfo.InvariantCulture), item.MinimumStock?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, item.Status })),
        ReportingExportKind.Expiry =>
            (["BatchId", "ProductReference", "ProductName", "BatchNumber", "ExpiryDate", "Quantity", "DaysRemaining", "Status"],
             snapshot.Expiry.Select(item => new[] { item.BatchId.ToString(CultureInfo.InvariantCulture), item.ProductReference, item.ProductName, item.BatchNumber ?? string.Empty, item.ExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty, item.Quantity.ToString(CultureInfo.InvariantCulture), item.DaysRemaining?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, item.Status })),
        ReportingExportKind.Purchasing =>
            (["ReceiptId", "ReceiptNumber", "ReceivedAtUtc", "SupplierCode", "SupplierName", "ReceiptType", "Quantity", "HistoricalAmountMAD"],
             snapshot.Purchasing.Select(item => new[] { item.ReceiptId.ToString(CultureInfo.InvariantCulture), item.ReceiptNumber, Date(item.ReceivedAtUtc), item.SupplierCode, item.SupplierName, item.ReceiptType, item.Quantity.ToString(CultureInfo.InvariantCulture), Number(item.HistoricalAmount) })),
        ReportingExportKind.InventoryAdjustments =>
            (["MovementId", "OccurredAtUtc", "ProductReference", "ProductName", "MovementType", "Quantity", "QuantityBefore", "QuantityAfter", "Reason", "SourceReference"],
             snapshot.InventoryAdjustments.Select(item => new[] { item.MovementId.ToString(CultureInfo.InvariantCulture), Date(item.OccurredAtUtc), item.ProductReference, item.ProductName, item.MovementType, item.Quantity.ToString(CultureInfo.InvariantCulture), item.QuantityBefore.ToString(CultureInfo.InvariantCulture), item.QuantityAfter.ToString(CultureInfo.InvariantCulture), item.Reason, item.SourceReference ?? string.Empty })),
        ReportingExportKind.CustomerCredit =>
            (["CustomerId", "CustomerReference", "CustomerName", "CreditAllowed", "CreditGeneratedMAD", "CreditPaidMAD", "OpenBalanceMAD"],
             snapshot.CustomerCredit.Select(item => new[] { item.CustomerId.ToString(CultureInfo.InvariantCulture), item.CustomerReference, item.CustomerName, item.CreditAllowed.ToString(CultureInfo.InvariantCulture), Number(item.CreditGenerated), Number(item.CreditPaid), Number(item.OpenBalance) })),
        ReportingExportKind.Returns =>
            (["ReturnId", "ReturnReference", "CompletedAtUtc", "SaleReference", "RefundMethod", "RefundAmountMAD", "ReturnedQuantity", "RestockedQuantity"],
             snapshot.Returns.Select(item => new[] { item.ReturnId.ToString(CultureInfo.InvariantCulture), item.ReturnReference, Date(item.CompletedAtUtc), item.SaleReference, item.RefundMethod, Number(item.RefundAmount), item.ReturnedQuantity.ToString(CultureInfo.InvariantCulture), item.RestockedQuantity.ToString(CultureInfo.InvariantCulture) })),
        ReportingExportKind.Cash =>
            (["EntryId", "OccurredAtUtc", "EntryType", "AmountMAD", "SourceType", "SourceReference"],
             snapshot.Cash.Select(item => new[] { item.EntryId.ToString(CultureInfo.InvariantCulture), Date(item.OccurredAtUtc), item.EntryType, Number(item.Amount), item.SourceType, item.SourceReference })),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}

internal static class ReportingQueryLimitExtensions
{
    public static IQueryable<T> LimitForInteractiveView<T>(this IQueryable<T> source, int? limit) =>
        limit.HasValue ? source.Take(limit.Value) : source;
}
