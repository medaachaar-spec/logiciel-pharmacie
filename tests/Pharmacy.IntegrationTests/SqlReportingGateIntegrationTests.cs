using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Reporting.Errors;
using Pharmacy.Application.Reporting.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Reporting;
using Pharmacy.Infrastructure.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class SqlReportingGateIntegrationTests(SqlReportingGateFixture fixture)
    : IClassFixture<SqlReportingGateFixture>
{
    private CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RPT001_AllElevenReportingViewsExistAndRespondWithExpectedColumns()
    {
        var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["vw_CashMovements"] = ["CashEntryId", "OccurredAtUtc", "EntryType", "Amount", "SourceType", "SourceReference"],
            ["vw_CustomerCredit"] = ["CustomerId", "CustomerReference", "CustomerName", "CreditAllowed", "CreditGenerated", "CreditPaid", "OpenBalance"],
            ["vw_ExpiryAlerts"] = ["BatchId", "ProductId", "ProductReference", "ProductName", "BatchNumber", "ExpiryDate", "Quantity", "DaysRemaining"],
            ["vw_InventoryAdjustments"] = ["MovementId", "OccurredAtUtc", "ProductId", "ProductReference", "ProductName", "MovementType", "Quantity", "QuantityBefore", "QuantityAfter", "Reason", "SourceType", "SourceReference"],
            ["vw_Payments"] = ["PaymentId", "SaleId", "SaleReference", "ConfirmedAtUtc", "PaymentMethod", "Status", "Amount", "TenderedAmount", "ChangeGiven", "ExternalCardConfirmed"],
            ["vw_ProductSales"] = ["SaleLineId", "SaleId", "SaleReference", "CompletedAtUtc", "ProductId", "ProductReferenceSnapshot", "ProductNameSnapshot", "SoldQuantity", "ReturnedQuantity", "GrossAmount", "RefundAmount", "NetAmount"],
            ["vw_PurchaseOrders"] = ["PurchaseOrderId", "OrderNumber", "OrderDate", "ExpectedDate", "Status", "SupplierCode", "SupplierName", "OrderedQuantity", "ReceivedQuantity", "HistoricalOrderedAmount"],
            ["vw_Receipts"] = ["ReceiptId", "ReceiptNumber", "ReceivedAtUtc", "ReceiptType", "Status", "SupplierCode", "SupplierName", "ReceivedQuantity", "HistoricalReceivedAmount"],
            ["vw_ReturnsSummary"] = ["ReturnId", "ReturnReference", "CompletedAtUtc", "SaleReference", "Status", "RefundMethod", "RefundAmount", "ReturnedQuantity", "RestockedQuantity"],
            ["vw_SalesSummary"] = ["SaleId", "Reference", "CompletedAtUtc", "Status", "PaymentMethod", "LineCount", "TotalQuantity", "GrossAmount", "PaidAmount", "CreditAmount", "RefundAmount", "NetAmount"],
            ["vw_StockSummary"] = ["ProductId", "ProductReference", "ProductName", "MinimumStock", "SellableQuantity"]
        };

        var names = await fixture.ViewNamesAsync();
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), names);
        foreach (var (name, columns) in expected)
        {
            Assert.Equal(columns, await fixture.ColumnsAsync(name));
            _ = await fixture.ScalarAsync<int>($"SELECT COUNT(*) FROM reporting.[{name}];");
        }
    }

    [Fact]
    public async Task RPT002_DashboardSaleCountDoesNotMultiplyBySixSaleLines()
    {
        var snapshot = await fixture.SnapshotAsync(fixture.Day());
        Assert.Equal(3, snapshot.Summary.SaleCount);
        Assert.Equal(6, snapshot.Sales.Sum(item => item.LineCount));
        Assert.Equal(6, snapshot.ProductSales.Count);
    }

    [Fact]
    public async Task RPT003_DashboardAmountsAreExact()
    {
        var summary = (await fixture.SnapshotAsync(fixture.Day())).Summary;
        Assert.Equal(111.10m, summary.GrossSales);
        Assert.Equal(20.99m, summary.Refunds);
        Assert.Equal(90.11m, summary.NetSales);
    }

    [Fact]
    public async Task RPT004_DateBoundariesUseInclusiveFromAndExclusiveTo()
    {
        var snapshot = await fixture.SnapshotAsync(fixture.Day());
        Assert.DoesNotContain(snapshot.Sales, item => item.Reference == "RPT-BEFORE");
        Assert.Contains(snapshot.Sales, item => item.Reference == "RPT-SALE-A-FROM");
        Assert.Contains(snapshot.Sales, item => item.Reference == "RPT-SALE-B-INSIDE");
        Assert.Contains(snapshot.Sales, item => item.Reference == "RPT-SALE-C-INSIDE");
        Assert.DoesNotContain(snapshot.Sales, item => item.Reference == "RPT-DAY2-AT-TO");
        Assert.DoesNotContain(snapshot.Sales, item => item.Reference == "RPT-DAY2-AFTER-TO");
    }

    [Fact]
    public async Task RPT005_MultiDayPeriodIncludesDaysOneAndTwoButNotDayThree()
    {
        var snapshot = await fixture.SnapshotAsync(fixture.Day(days: 2));
        Assert.Equal(5, snapshot.Summary.SaleCount);
        Assert.Equal(123.10m, snapshot.Summary.GrossSales);
        Assert.Contains(snapshot.Sales, item => item.Reference == "RPT-DAY2-AT-TO");
        Assert.Contains(snapshot.Sales, item => item.Reference == "RPT-DAY2-AFTER-TO");
        Assert.DoesNotContain(snapshot.Sales, item => item.Reference == "RPT-DAY3");
    }

    [Fact]
    public async Task RPT006_EmptyPeriodReturnsZeroAndSafeCurrentStateCollections()
    {
        var snapshot = await fixture.SnapshotAsync(fixture.Day(startOffset: 10));
        Assert.Equal(0m, snapshot.Summary.GrossSales);
        Assert.Equal(0m, snapshot.Summary.Refunds);
        Assert.Equal(0m, snapshot.Summary.NetSales);
        Assert.Equal(0, snapshot.Summary.SaleCount);
        Assert.Equal(0, snapshot.Summary.ReturnCount);
        Assert.Empty(snapshot.Summary.Payments);
        Assert.Equal(0m, snapshot.Summary.CreditPayments);
        Assert.Equal(0, snapshot.Summary.PurchaseOrders);
        Assert.Equal(0, snapshot.Summary.ReceivedQuantity);
        Assert.Empty(snapshot.Sales);
        Assert.Empty(snapshot.ProductSales);
        Assert.Empty(snapshot.Purchasing);
        Assert.Empty(snapshot.InventoryAdjustments);
        Assert.Empty(snapshot.Returns);
        Assert.Empty(snapshot.Cash);
        Assert.NotEmpty(snapshot.Stock);
        Assert.NotEmpty(snapshot.Expiry);
        Assert.NotEmpty(snapshot.CustomerCredit);
        Assert.Equal(85m, snapshot.Summary.OpenCredit);
        Assert.Equal(9, snapshot.Summary.CurrentSellableStock);
    }

    [Fact]
    public async Task RPT008_DecimalAmountsRemainExact()
    {
        Assert.Equal(111.10m, await fixture.ScalarAsync<decimal>("SELECT SUM(GrandTotal) FROM sales.Sales WHERE Reference IN (N'RPT-SALE-A-FROM',N'RPT-SALE-B-INSIDE',N'RPT-SALE-C-INSIDE');"));
        Assert.Equal(51.10m, (await fixture.SnapshotAsync(fixture.Day())).Summary.Payments.Sum(item => item.Amount));
        Assert.Equal(59.01m, (await fixture.SnapshotAsync(fixture.Day())).Cash.Sum(item => item.Amount));
    }

    [Fact]
    public async Task RPT009_MultiplePaymentsDoNotMultiplySales()
    {
        Assert.Equal(2, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM reporting.vw_Payments WHERE SaleReference=N'RPT-SALE-B-INSIDE';"));
        Assert.Equal(10.99m, await fixture.ScalarAsync<decimal>("SELECT SUM(Amount) FROM reporting.vw_Payments WHERE SaleReference=N'RPT-SALE-B-INSIDE';"));
        Assert.Equal(10.99m, await fixture.ScalarAsync<decimal>("SELECT GrossAmount FROM reporting.vw_SalesSummary WHERE Reference=N'RPT-SALE-B-INSIDE';"));
        Assert.Equal(1, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM reporting.vw_SalesSummary WHERE Reference=N'RPT-SALE-B-INSIDE';"));
    }

    [Fact]
    public async Task RPT010_MultipleFefoAllocationsDoNotMultiplySalesOrLines()
    {
        Assert.Equal(2, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM sales.SaleLineAllocations a INNER JOIN sales.SaleLines l ON l.Id=a.SaleLineId INNER JOIN sales.Sales s ON s.Id=l.SaleId WHERE s.Reference=N'RPT-SALE-A-FROM' AND l.ProductReferenceSnapshot=N'RPT-A';"));
        Assert.Equal(1, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM reporting.vw_ProductSales WHERE SaleReference=N'RPT-SALE-A-FROM' AND ProductReferenceSnapshot=N'RPT-A';"));
        Assert.Equal(100m, await fixture.ScalarAsync<decimal>("SELECT GrossAmount FROM reporting.vw_SalesSummary WHERE Reference=N'RPT-SALE-A-FROM';"));
    }

    [Fact]
    public async Task RPT011_ReturnAggregationKeepsGrossRefundAndNetSeparate()
    {
        var summary = (await fixture.SnapshotAsync(fixture.Day())).Summary;
        Assert.Equal(111.10m, summary.GrossSales);
        Assert.Equal(20.99m, summary.Refunds);
        Assert.Equal(90.11m, summary.NetSales);
        Assert.Equal(2, summary.ReturnCount);
    }

    [Fact]
    public async Task RPT012_MultiLineReturnIsOneReturnWithExactQuantities()
    {
        Assert.Equal(1, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM reporting.vw_ReturnsSummary WHERE ReturnReference=N'RPT-RETURN-A';"));
        Assert.Equal(2, await fixture.ScalarAsync<int>("SELECT ReturnedQuantity FROM reporting.vw_ReturnsSummary WHERE ReturnReference=N'RPT-RETURN-A';"));
        Assert.Equal(1, await fixture.ScalarAsync<int>("SELECT RestockedQuantity FROM reporting.vw_ReturnsSummary WHERE ReturnReference=N'RPT-RETURN-A';"));
        Assert.Equal(20m, await fixture.ScalarAsync<decimal>("SELECT RefundAmount FROM reporting.vw_ReturnsSummary WHERE ReturnReference=N'RPT-RETURN-A';"));
    }

    [Fact]
    public async Task RPT013_CustomerCreditTotalsThreeCustomersExactly()
    {
        Assert.Equal(3, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM reporting.vw_CustomerCredit WHERE CustomerReference LIKE N'RPT-CUST-%';"));
        Assert.Equal(85m, await fixture.ScalarAsync<decimal>("SELECT SUM(CASE WHEN OpenBalance>0 THEN OpenBalance ELSE 0 END) FROM reporting.vw_CustomerCredit WHERE CustomerReference LIKE N'RPT-CUST-%';"));
        Assert.Equal(85m, (await fixture.SnapshotAsync(fixture.Day())).Summary.OpenCredit);
    }

    [Fact]
    public async Task RPT014_CreditPaymentAndReturnReductionAreReflected()
    {
        var reduced = await fixture.CreditAfterTemporaryReductionsAsync();
        Assert.Equal(55m, reduced);
        Assert.Equal(85m, await fixture.ScalarAsync<decimal>("SELECT SUM(CASE WHEN OpenBalance>0 THEN OpenBalance ELSE 0 END) FROM reporting.vw_CustomerCredit WHERE CustomerReference LIKE N'RPT-CUST-%';"));
    }

    [Fact]
    public async Task RPT015_CashMovementsAreSignedAndExact()
    {
        var snapshot = await fixture.SnapshotAsync(fixture.Day());
        Assert.Equal(3, snapshot.Cash.Count);
        Assert.Equal(59.01m, snapshot.Cash.Sum(item => item.Amount));
        Assert.Contains(snapshot.Cash, item => item.EntryType == "RefundCashOut" && item.Amount == -0.99m);
    }

    [Fact]
    public async Task RPT016_ExternalCardPaymentsCreateNoArtificialCash()
    {
        Assert.Equal(10.99m, await fixture.ScalarAsync<decimal>("SELECT SUM(Amount) FROM reporting.vw_Payments WHERE SaleReference=N'RPT-SALE-B-INSIDE' AND PaymentMethod=N'ExternalCard';"));
        Assert.Equal(0, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM reporting.vw_CashMovements WHERE SourceReference=N'RPT-SALE-B-INSIDE';"));
    }

    [Fact]
    public async Task RPT017_StockPhysicalAndSellableQuantitiesAreExact()
    {
        Assert.Equal(12L, await fixture.ScalarAsync<long>("SELECT SUM(CONVERT(bigint,b.AvailableQuantity)) FROM inventory.StockBalances b INNER JOIN catalog.ProductLocalSettings l ON l.ProductId=b.ProductId WHERE l.InternalCode IN (N'RPT-A',N'RPT-B');"));
        Assert.Equal(9L, await fixture.ScalarAsync<long>("SELECT SUM(SellableQuantity) FROM reporting.vw_StockSummary WHERE ProductReference IN (N'RPT-A',N'RPT-B');"));
        Assert.Equal(4L, await fixture.ScalarAsync<long>("SELECT SellableQuantity FROM reporting.vw_StockSummary WHERE ProductReference=N'RPT-A';"));
        Assert.Equal(5L, await fixture.ScalarAsync<long>("SELECT SellableQuantity FROM reporting.vw_StockSummary WHERE ProductReference=N'RPT-B';"));
    }

    [Fact]
    public async Task RPT018_ExpiredStockRemainsPhysicalButIsExcludedFromSellable()
    {
        Assert.Equal(7L, await fixture.ScalarAsync<long>("SELECT SUM(CONVERT(bigint,b.AvailableQuantity)) FROM inventory.StockBalances b INNER JOIN catalog.ProductLocalSettings l ON l.ProductId=b.ProductId WHERE l.InternalCode=N'RPT-A';"));
        Assert.Equal(4L, await fixture.ScalarAsync<long>("SELECT SellableQuantity FROM reporting.vw_StockSummary WHERE ProductReference=N'RPT-A';"));
        Assert.Equal(3L, await fixture.ScalarAsync<long>("SELECT Quantity FROM reporting.vw_ExpiryAlerts WHERE ProductReference=N'RPT-A' AND DaysRemaining=-1;"));
    }

    [Fact]
    public async Task RPT019_ExpiryHorizonBoundariesAreInclusiveAndExpiredIsSeparate()
    {
        await fixture.ResetSettingsAsync();
        try
        {
            var service = fixture.Service(SecurityPermissions.ReportsView, SecurityPermissions.ReportsManageSettings);
            var before = await service.GetSnapshotAsync(fixture.Day(), Token);
            var updated = await service.UpdateSettingsAsync(new UpdateAlertSettingsRequest(
                Guid.NewGuid(), true, true, 30, before.Value.Settings.RowVersion), Token);
            Assert.True(updated.IsSuccess, updated.IsFailure ? updated.Error.Code : null);
            var snapshot = await service.GetSnapshotAsync(fixture.Day(), Token);
            Assert.Equal(1, snapshot.Value.Summary.ExpiredBatches);
            Assert.Equal(2, snapshot.Value.Summary.NearExpiryBatches);
            Assert.Equal(1, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM reporting.vw_ExpiryAlerts WHERE DaysRemaining=30;"));
            Assert.Equal(1, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM reporting.vw_ExpiryAlerts WHERE DaysRemaining=31;"));
        }
        finally
        {
            await fixture.ResetSettingsAsync();
        }
    }

    [Fact]
    public async Task RPT020_ReportSettingsPersistAcrossContextsAndRejectStaleRowVersion()
    {
        await fixture.ResetSettingsAsync();
        try
        {
            var firstService = fixture.Service(SecurityPermissions.ReportsView, SecurityPermissions.ReportsManageSettings);
            var initial = await firstService.GetSnapshotAsync(fixture.Day(), Token);
            var changed = await firstService.UpdateSettingsAsync(new UpdateAlertSettingsRequest(
                Guid.NewGuid(), true, true, 30, initial.Value.Settings.RowVersion), Token);
            Assert.True(changed.IsSuccess, changed.IsFailure ? changed.Error.Code : null);

            var secondService = fixture.Service(SecurityPermissions.ReportsView, SecurityPermissions.ReportsManageSettings);
            var persisted = await secondService.GetSnapshotAsync(fixture.Day(), Token);
            Assert.True(persisted.Value.Settings.LowStockAlertsEnabled);
            Assert.True(persisted.Value.Settings.ExpiryAlertsEnabled);
            Assert.Equal(30, persisted.Value.Settings.ExpiryHorizonDays);

            var stale = await secondService.UpdateSettingsAsync(new UpdateAlertSettingsRequest(
                Guid.NewGuid(), false, false, null, initial.Value.Settings.RowVersion), Token);
            Assert.Equal("reporting.settings.concurrency", stale.Error.Code);
        }
        finally
        {
            await fixture.ResetSettingsAsync();
        }
    }

    [Fact]
    public async Task RPT021_InvalidReportSettingsAreRejectedBeforeMutation()
    {
        await fixture.ResetSettingsAsync();
        var service = fixture.Service(SecurityPermissions.ReportsView, SecurityPermissions.ReportsManageSettings);
        var initial = await service.GetSnapshotAsync(fixture.Day(), Token);
        var missing = await service.UpdateSettingsAsync(new UpdateAlertSettingsRequest(
            Guid.NewGuid(), false, true, null, initial.Value.Settings.RowVersion), Token);
        var negative = await service.UpdateSettingsAsync(new UpdateAlertSettingsRequest(
            Guid.NewGuid(), false, true, -1, initial.Value.Settings.RowVersion), Token);
        Assert.Equal(ReportingErrors.InvalidSettings.Code, missing.Error.Code);
        Assert.Equal(ReportingErrors.InvalidSettings.Code, negative.Error.Code);
        Assert.Equal(0, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM reporting.ReportSettings WHERE LowStockAlertsEnabled=1 OR ExpiryAlertsEnabled=1 OR ExpiryHorizonDays IS NOT NULL;"));
    }

    [Fact]
    public async Task RPT022_ReportsViewRbacIsEnforcedAtBackend()
    {
        var denied = await fixture.UnauthenticatedService().GetSnapshotAsync(fixture.Day(), Token);
        Assert.Equal("security.authorization.authentication_required", denied.Error.Code);
        var allowed = await fixture.Service(SecurityPermissions.ReportsView).GetSnapshotAsync(fixture.Day(), Token);
        Assert.True(allowed.IsSuccess, allowed.IsFailure ? allowed.Error.Code : null);
    }

    [Fact]
    public async Task RPT023_ReportsExportRbacIsEnforcedAtBackend()
    {
        var request = new ReportingExportRequest(Guid.NewGuid(), ReportingExportKind.Sales, fixture.Day());
        var denied = await fixture.Service(SecurityPermissions.ReportsView).ExportCsvAsync(request, Token);
        Assert.Equal("security.authorization.denied", denied.Error.Code);
        var allowed = await fixture.Service(SecurityPermissions.ReportsView, SecurityPermissions.ReportsExport)
            .ExportCsvAsync(request with { OperationId = Guid.NewGuid() }, Token);
        Assert.True(allowed.IsSuccess, allowed.IsFailure ? allowed.Error.Code : null);
    }

    [Fact]
    public async Task RPT024_ReportsManageSettingsRbacRefusesBeforeMutation()
    {
        await fixture.ResetSettingsAsync();
        var snapshot = await fixture.Service(SecurityPermissions.ReportsView).GetSnapshotAsync(fixture.Day(), Token);
        var denied = await fixture.Service(SecurityPermissions.ReportsView).UpdateSettingsAsync(
            new UpdateAlertSettingsRequest(Guid.NewGuid(), true, true, 30, snapshot.Value.Settings.RowVersion), Token);
        Assert.Equal("security.authorization.denied", denied.Error.Code);
        Assert.Equal(0, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM reporting.ReportSettings WHERE LowStockAlertsEnabled=1 OR ExpiryAlertsEnabled=1;"));
    }

    [Fact]
    public async Task RPT025_AllCsvExportsHaveBomHeadersAndExactDataRowCounts()
    {
        var service = fixture.Service(SecurityPermissions.ReportsView, SecurityPermissions.ReportsExport);
        var snapshot = (await service.GetSnapshotAsync(fixture.Day(), Token)).Value;
        var expectedCounts = new Dictionary<ReportingExportKind, int>
        {
            [ReportingExportKind.Sales] = snapshot.Sales.Count,
            [ReportingExportKind.ProductSales] = snapshot.ProductSales.Count,
            [ReportingExportKind.Stock] = snapshot.Stock.Count,
            [ReportingExportKind.Expiry] = snapshot.Expiry.Count,
            [ReportingExportKind.Purchasing] = snapshot.Purchasing.Count,
            [ReportingExportKind.InventoryAdjustments] = snapshot.InventoryAdjustments.Count,
            [ReportingExportKind.CustomerCredit] = snapshot.CustomerCredit.Count,
            [ReportingExportKind.Returns] = snapshot.Returns.Count,
            [ReportingExportKind.Cash] = snapshot.Cash.Count
        };

        foreach (var kind in Enum.GetValues<ReportingExportKind>())
        {
            var result = await service.ExportCsvAsync(new ReportingExportRequest(Guid.NewGuid(), kind, fixture.Day()), Token);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
            Assert.True(result.Value.Content.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
            Assert.Equal("text/csv; charset=utf-8", result.Value.MediaType);
            var lines = fixture.CsvLines(result.Value.Content);
            Assert.NotEmpty(lines[0]);
            Assert.Equal(expectedCounts[kind], lines.Length - 1);
        }
    }

    [Fact]
    public async Task RPT026_EmptyPeriodCsvContainsHeadersAndNoBusinessRows()
    {
        var result = await fixture.Service(SecurityPermissions.ReportsView, SecurityPermissions.ReportsExport)
            .ExportCsvAsync(new ReportingExportRequest(Guid.NewGuid(), ReportingExportKind.Sales, fixture.Day(10)), Token);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        var lines = fixture.CsvLines(result.Value.Content);
        Assert.Single(lines);
        Assert.Equal("\"SaleId\";\"Reference\";\"CompletedAtUtc\";\"PaymentMethod\";\"Quantity\";\"GrossAmountMAD\";\"RefundAmountMAD\";\"NetAmountMAD\"", lines[0]);
    }

    [Fact]
    public async Task RPT027_FormulaInjectionPrefixesAreNeutralizedInReportingCsv()
    {
        var export = await fixture.Service(SecurityPermissions.ReportsView, SecurityPermissions.ReportsExport)
            .ExportCsvAsync(new ReportingExportRequest(Guid.NewGuid(), ReportingExportKind.Stock, fixture.Day()), Token);
        var csv = Encoding.UTF8.GetString(export.Value.Content);
        foreach (var expected in new[] { "'=1+1", "'+TEST", "'-TEST", "'@TEST", "'  =1+1", "'\t=1+1" })
        {
            Assert.Contains(expected, csv, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task RPT028_UnicodeIsPreservedInReportingCsv()
    {
        var export = await fixture.Service(SecurityPermissions.ReportsView, SecurityPermissions.ReportsExport)
            .ExportCsvAsync(new ReportingExportRequest(Guid.NewGuid(), ReportingExportKind.Stock, fixture.Day()), Token);
        var csv = Encoding.UTF8.GetString(export.Value.Content);
        Assert.Contains("Paracétamol", csv, StringComparison.Ordinal);
        Assert.Contains("Crème", csv, StringComparison.Ordinal);
        Assert.Contains("دواء", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RPT029_InventoryAdjustmentsAreExactAndNotMultiplied()
    {
        Assert.Equal(2, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM reporting.vw_InventoryAdjustments WHERE SourceReference LIKE N'RPT-ADJ-%';"));
        Assert.Equal(1, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM reporting.vw_InventoryAdjustments WHERE SourceReference=N'RPT-ADJ-PLUS' AND MovementType=N'InventoryIncrease' AND Quantity=2 AND QuantityBefore=4 AND QuantityAfter=6;"));
        Assert.Equal(1, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM reporting.vw_InventoryAdjustments WHERE SourceReference=N'RPT-ADJ-MINUS' AND MovementType=N'InventoryDecrease' AND Quantity=1 AND QuantityBefore=5 AND QuantityAfter=4;"));
    }

    [Fact]
    public async Task RPT030_PurchaseOrderWithMultipleLinesRemainsOneOrder()
    {
        Assert.Equal(1, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM reporting.vw_PurchaseOrders WHERE OrderNumber=N'RPT-PO-001';"));
        Assert.Equal(30L, await fixture.ScalarAsync<long>("SELECT OrderedQuantity FROM reporting.vw_PurchaseOrders WHERE OrderNumber=N'RPT-PO-001';"));
        Assert.Equal(9L, await fixture.ScalarAsync<long>("SELECT ReceivedQuantity FROM reporting.vw_PurchaseOrders WHERE OrderNumber=N'RPT-PO-001';"));
        Assert.Equal(230m, await fixture.ScalarAsync<decimal>("SELECT HistoricalOrderedAmount FROM reporting.vw_PurchaseOrders WHERE OrderNumber=N'RPT-PO-001';"));
    }

    [Fact]
    public async Task RPT031_TwoReceiptsRemainTwoReceiptsWithExactTotals()
    {
        Assert.Equal(2, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM reporting.vw_Receipts WHERE ReceiptNumber LIKE N'RPT-GR-%';"));
        Assert.Equal(9L, await fixture.ScalarAsync<long>("SELECT SUM(ReceivedQuantity) FROM reporting.vw_Receipts WHERE ReceiptNumber LIKE N'RPT-GR-%';"));
        Assert.Equal(65m, await fixture.ScalarAsync<decimal>("SELECT SUM(HistoricalReceivedAmount) FROM reporting.vw_Receipts WHERE ReceiptNumber LIKE N'RPT-GR-%';"));
    }

    [Fact]
    public async Task RPT032_ProductSalesQuantitiesIgnoreFefoAllocationCount()
    {
        Assert.Equal(5, await fixture.ScalarAsync<int>("SELECT SUM(SoldQuantity) FROM reporting.vw_ProductSales WHERE ProductReferenceSnapshot=N'RPT-A' AND CompletedAtUtc>=@from AND CompletedAtUtc<@to;", fixture.P("@from", fixture.AnchorUtc), fixture.P("@to", fixture.AnchorUtc.AddDays(1))));
        Assert.Equal(3, await fixture.ScalarAsync<int>("SELECT SUM(SoldQuantity) FROM reporting.vw_ProductSales WHERE ProductReferenceSnapshot=N'RPT-B' AND CompletedAtUtc>=@from AND CompletedAtUtc<@to;", fixture.P("@from", fixture.AnchorUtc), fixture.P("@to", fixture.AnchorUtc.AddDays(1))));
    }

    [Fact]
    public async Task RPT033_SQLUnavailableReturnsControlledFailureWithoutFallback()
    {
        var unavailable = fixture.UnavailableService(SecurityPermissions.ReportsView, SecurityPermissions.ReportsExport);
        var read = await unavailable.GetSnapshotAsync(fixture.Day(), Token);
        Assert.Equal(ReportingErrors.DatabaseUnavailable.Code, read.Error.Code);
        var export = await unavailable.ExportCsvAsync(
            new ReportingExportRequest(Guid.NewGuid(), ReportingExportKind.Sales, fixture.Day()), Token);
        Assert.Equal(ReportingErrors.DatabaseUnavailable.Code, export.Error.Code);
    }

    [Fact]
    public async Task RPT034_ReportingReadsAndExportsDoNotMutateBusinessTables()
    {
        var before = await fixture.BusinessCountsAsync();
        var service = fixture.Service(SecurityPermissions.ReportsView, SecurityPermissions.ReportsExport);
        Assert.True((await service.GetSnapshotAsync(fixture.Day(), Token)).IsSuccess);
        foreach (var kind in Enum.GetValues<ReportingExportKind>())
        {
            Assert.True((await service.ExportCsvAsync(
                new ReportingExportRequest(Guid.NewGuid(), kind, fixture.Day()), Token)).IsSuccess);
        }
        Assert.Equal(before, await fixture.BusinessCountsAsync());
    }

    [Fact]
    public async Task RPT035_NewServiceAndContextReturnTheSameReport()
    {
        var first = (await fixture.Service(SecurityPermissions.ReportsView).GetSnapshotAsync(fixture.Day(), Token)).Value;
        var second = (await fixture.Service(SecurityPermissions.ReportsView).GetSnapshotAsync(fixture.Day(), Token)).Value;
        Assert.NotSame(first, second);
        Assert.Equal(first.Summary.GrossSales, second.Summary.GrossSales);
        Assert.Equal(first.Summary.SaleCount, second.Summary.SaleCount);
        Assert.Equal(first.Summary.OpenCredit, second.Summary.OpenCredit);
        Assert.Equal(first.Summary.CurrentSellableStock, second.Summary.CurrentSellableStock);
        Assert.Equal(first.Sales.Select(item => item.Reference), second.Sales.Select(item => item.Reference));
        Assert.Equal(first.Stock.Select(item => (item.ProductReference, item.SellableQuantity)), second.Stock.Select(item => (item.ProductReference, item.SellableQuantity)));
    }

    [Fact]
    public async Task RPT036_ReadDuringUncommittedWriteWaitsAndReturnsConsistentReadOnlyProjection()
    {
        var before = await fixture.BusinessCountsAsync();

        var result = await fixture.SnapshotDuringRolledBackSettingsWriteAsync();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        Assert.Equal(before, await fixture.BusinessCountsAsync());
        Assert.Equal(result.Value.Sales.Count, result.Value.Sales.Select(item => item.SaleId).Distinct().Count());
    }
}

public sealed class SqlReportingGateFixture : IAsyncLifetime
{
    private const string Prefix = "Pharmacy_RPT_Gate_Test_";
    private string serverConnection = string.Empty;
    private string databaseConnection = string.Empty;
    private string databaseName = string.Empty;

    public DateTime AnchorUtc { get; private set; }
    public IDbContextFactory<PharmacyDbContext> Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("PHARMACY_TEST_SQLSERVER_CONNECTION est requise.");
        }

        serverConnection = new SqlConnectionStringBuilder(raw)
        {
            InitialCatalog = "master",
            Pooling = false,
            ApplicationName = "Pharmacy.RPT.Gate.Tests"
        }.ConnectionString;
        AnchorUtc = DateTime.SpecifyKind(
            (await ServerScalarAsync<DateTime>("SELECT CONVERT(datetime2(7), CONVERT(date, SYSUTCDATETIME()));")),
            DateTimeKind.Utc);

        databaseName = Prefix + Guid.NewGuid().ToString("N")[..12];
        ValidateDatabaseName(databaseName);
        await ExecuteServerAsync($"CREATE DATABASE [{databaseName}];");
        databaseConnection = new SqlConnectionStringBuilder(serverConnection)
        {
            InitialCatalog = databaseName,
            Pooling = false
        }.ConnectionString;
        Factory = CreateFactory(databaseConnection);

        await using (var context = await Factory.CreateDbContextAsync())
        {
            await context.Database.MigrateAsync();
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
            Assert.Equal(11, (await context.Database.GetAppliedMigrationsAsync()).Count());
        }

        await SeedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            ValidateDatabaseName(databaseName);
            await ExecuteServerAsync(
                $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END;");
        }
        SqlConnection.ClearAllPools();
    }

    public ReportingQuery Day(int startOffset = 0, int days = 1) => new(
        new ReportingPeriod(AnchorUtc.AddDays(startOffset), AnchorUtc.AddDays(startOffset + days)),
        1000);

    public async Task<ReportingSnapshotView> SnapshotAsync(ReportingQuery query)
    {
        var result = await Service(SecurityPermissions.ReportsView)
            .GetSnapshotAsync(query, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    public SqlReportingService Service(params string[] permissions) =>
        CreateService(Factory, permissions, establishSession: true);

    public SqlReportingService UnauthenticatedService() =>
        CreateService(Factory, [], establishSession: false);

    public SqlReportingService UnavailableService(params string[] permissions)
    {
        var unavailableConnection = new SqlConnectionStringBuilder(serverConnection)
        {
            DataSource = "localhost,1",
            InitialCatalog = "master",
            ConnectTimeout = 1,
            Pooling = false,
            ApplicationName = "Pharmacy.RPT.Unavailable.Tests"
        }.ConnectionString;
        return CreateService(CreateFactory(unavailableConnection), permissions, establishSession: true);
    }

    public async Task<string[]> ViewNamesAsync()
    {
        const string sql = "SELECT v.name FROM sys.views v INNER JOIN sys.schemas s ON s.schema_id=v.schema_id WHERE s.name=N'reporting' ORDER BY v.name;";
        await using var connection = new SqlConnection(databaseConnection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var names = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken)) names.Add(reader.GetString(0));
        return names.ToArray();
    }

    public async Task<string[]> ColumnsAsync(string viewName)
    {
        Assert.Matches(new Regex("^vw_[A-Za-z]+$", RegexOptions.CultureInvariant), viewName);
        await using var connection = new SqlConnection(databaseConnection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT TOP (0) * FROM reporting.[{viewName}];";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        return Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
    }

    public async Task<T> ScalarAsync<T>(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(databaseConnection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(value);
        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    public SqlParameter P(string name, object value) => new(name, value);

    public string[] CsvLines(byte[] content) => Encoding.UTF8.GetString(content)
        .TrimStart('\uFEFF')
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Split('\n', StringSplitOptions.RemoveEmptyEntries);

    public async Task ResetSettingsAsync() => await ExecuteAsync("""
        UPDATE reporting.ReportSettings
        SET LowStockAlertsEnabled=0, ExpiryAlertsEnabled=0, ExpiryHorizonDays=NULL, UpdatedAtUtc=@now
        WHERE Id=1;
        """, P("@now", AnchorUtc));

    public async Task<decimal> CreditAfterTemporaryReductionsAsync()
    {
        await using var connection = new SqlConnection(databaseConnection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        try
        {
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    DECLARE @customerId bigint=(SELECT Id FROM customers.Customers WHERE Reference=N'RPT-CUST-A');
                    INSERT customers.CustomerLedgerEntries(OperationId,CustomerId,EntryType,Amount,SourceType,SourceReference,OccurredAtUtc,CreatedAtUtc)
                    VALUES(NEWID(),@customerId,N'CreditPayment',10,N'CreditPayment',N'RPT-TEMP-PAY',@now,@now),
                          (NEWID(),@customerId,N'RefundImpact',20,N'SaleReturn',N'RPT-TEMP-RETURN',@now,@now);
                    """;
                insert.Parameters.Add(P("@now", AnchorUtc.AddHours(22)));
                await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            await using var query = connection.CreateCommand();
            query.Transaction = transaction;
            query.CommandText = "SELECT SUM(CASE WHEN OpenBalance>0 THEN OpenBalance ELSE 0 END) FROM reporting.vw_CustomerCredit WHERE CustomerReference LIKE N'RPT-CUST-%';";
            var value = Convert.ToDecimal(await query.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
            return value;
        }
        catch
        {
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
            throw;
        }
    }

    public async Task<Result<ReportingSnapshotView>> SnapshotDuringRolledBackSettingsWriteAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(databaseConnection);
        await connection.OpenAsync(token);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(token);
        try
        {
            await using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = "UPDATE reporting.ReportSettings SET LowStockAlertsEnabled=1, UpdatedAtUtc=@now WHERE Id=1;";
                update.Parameters.Add(P("@now", AnchorUtc.AddHours(23)));
                Assert.Equal(1, await update.ExecuteNonQueryAsync(token));
            }

            var read = Service(SecurityPermissions.ReportsView).GetSnapshotAsync(Day(), token);
            await Task.Delay(100, token);
            await transaction.RollbackAsync(token);
            return await read.WaitAsync(TimeSpan.FromSeconds(10), token);
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync(token);
            throw;
        }
    }

    public async Task<string> BusinessCountsAsync()
    {
        var tables = new[]
        {
            "sales.Sales", "sales.Payments", "inventory.StockBalances", "inventory.StockMovements",
            "customers.Customers", "customers.CustomerLedgerEntries", "cash.CashLedgerEntries",
            "purchasing.PurchaseOrders", "purchasing.GoodsReceipts", "sales.SaleReturns"
        };
        var counts = new List<string>(tables.Length);
        foreach (var table in tables)
        {
            Assert.Matches(new Regex("^[a-z]+\\.[A-Za-z]+$", RegexOptions.CultureInvariant), table);
            counts.Add($"{table}:{await ScalarAsync<int>($"SELECT COUNT(*) FROM {table};")}");
        }
        return string.Join('|', counts);
    }

    private SqlReportingService CreateService(
        IDbContextFactory<PharmacyDbContext> factory,
        IReadOnlyCollection<string> permissions,
        bool establishSession)
    {
        var now = AnchorUtc.AddHours(12);
        var clock = new FixedClock(now);
        var session = new CurrentSecuritySession();
        if (establishSession)
        {
            session.Establish(new UserSessionView(
                9900,
                "rpt-gate",
                "RPT Gate",
                ["OWNER"],
                new HashSet<string>(permissions, StringComparer.Ordinal),
                now,
                now), now);
        }
        var authorizer = new PermissionAuthorizer(session, clock);
        var audit = new BusinessAuditRecorder(new SqlAuditStore(factory, authorizer), session, clock);
        return new SqlReportingService(factory, authorizer, audit, clock);
    }

    private static IDbContextFactory<PharmacyDbContext> CreateFactory(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<PharmacyDbContext>();
        builder.UsePharmacySqlServer(connectionString);
        return new TestDbContextFactory(builder.Options);
    }

    private async Task SeedAsync()
    {
        const string sql = """
            -- RPT deterministic fixture; all rows live only in the temporary gate database.
            DECLARE @categoryId bigint, @productA bigint, @productB bigint, @productC bigint, @locationId bigint;
            DECLARE @batchA5 bigint, @batchANull bigint, @batchAExpired bigint, @batchB30 bigint, @batchB31 bigint;
            DECLARE @balanceA5 bigint, @balanceANull bigint;
            DECLARE @customerA bigint, @customerB bigint, @customerC bigint;
            DECLARE @saleA bigint, @saleB bigint, @saleC bigint, @saleBefore bigint, @saleDay2A bigint, @saleDay2B bigint, @saleDay3 bigint;
            DECLARE @lineA1 bigint, @lineA2 bigint, @lineB1 bigint, @lineC1 bigint, @lineC2 bigint, @lineC3 bigint;
            DECLARE @returnA bigint, @returnB bigint;
            DECLARE @supplierId bigint, @orderId bigint, @orderLineA bigint, @orderLineB bigint;
            DECLARE @movementReceiptA bigint, @movementReceiptB bigint, @receiptA bigint, @receiptB bigint;

            INSERT catalog.ProductCategories(Code,Name,IsActive,DefaultTracksLots,DefaultTracksExpiry,DefaultDiscountAllowed,SortOrder)
            VALUES(N'RPT-GATE',N'Reporting gate',1,1,1,0,0);
            SET @categoryId=SCOPE_IDENTITY();

            INSERT catalog.Products(Name,IsActive,CreatedAtUtc,UpdatedAtUtc) VALUES(N'Paracétamol',1,@anchor,@anchor);
            SET @productA=SCOPE_IDENTITY();
            INSERT catalog.ProductLocalSettings(ProductId,InternalCode,CategoryId,IsActive,IsSellable,IsPurchasable,TracksLots,TracksExpiry,DiscountAllowed,QuantityScale,MinimumStock,CurrentSalePrice,CurrencyCode)
            VALUES(@productA,N'RPT-A',@categoryId,1,1,1,1,1,0,0,5,20,N'MAD');

            INSERT catalog.Products(Name,IsActive,CreatedAtUtc,UpdatedAtUtc) VALUES(N'Crème دواء',1,@anchor,@anchor);
            SET @productB=SCOPE_IDENTITY();
            INSERT catalog.ProductLocalSettings(ProductId,InternalCode,CategoryId,IsActive,IsSellable,IsPurchasable,TracksLots,TracksExpiry,DiscountAllowed,QuantityScale,MinimumStock,CurrentSalePrice,CurrencyCode)
            VALUES(@productB,N'RPT-B',@categoryId,1,1,1,1,1,0,0,5,10,N'MAD');

            INSERT catalog.Products(Name,IsActive,CreatedAtUtc,UpdatedAtUtc) VALUES(N'Produit C',1,@anchor,@anchor);
            SET @productC=SCOPE_IDENTITY();
            INSERT catalog.ProductLocalSettings(ProductId,InternalCode,CategoryId,IsActive,IsSellable,IsPurchasable,TracksLots,TracksExpiry,DiscountAllowed,QuantityScale,MinimumStock,CurrentSalePrice,CurrencyCode)
            VALUES(@productC,N'RPT-C',@categoryId,1,1,1,0,0,0,0,NULL,10,N'MAD');

            DECLARE @formula TABLE(Code nvarchar(64),Name nvarchar(200));
            INSERT @formula(Code,Name) VALUES
                (N'RPT-FML-1',N'=1+1'),(N'RPT-FML-2',N'+TEST'),(N'RPT-FML-3',N'-TEST'),
                (N'RPT-FML-4',N'@TEST'),(N'RPT-FML-5',N'  =1+1'),(N'RPT-FML-6',NCHAR(9)+N'=1+1');
            DECLARE @formulaMap TABLE(ProductId bigint,Code nvarchar(64));
            MERGE catalog.Products AS target
            USING @formula AS source ON 1=0
            WHEN NOT MATCHED THEN INSERT(Name,IsActive,CreatedAtUtc,UpdatedAtUtc) VALUES(source.Name,1,@anchor,@anchor)
            OUTPUT inserted.Id,source.Code INTO @formulaMap(ProductId,Code);
            INSERT catalog.ProductLocalSettings(ProductId,InternalCode,CategoryId,IsActive,IsSellable,IsPurchasable,TracksLots,TracksExpiry,DiscountAllowed,QuantityScale,MinimumStock,CurrentSalePrice,CurrencyCode)
            SELECT ProductId,Code,@categoryId,1,1,1,0,0,0,0,NULL,1,N'MAD' FROM @formulaMap;

            INSERT inventory.StockLocations(CreationOperationId,CreationFingerprint,Code,Name,IsActive,IsPrimary,CreatedAtUtc)
            VALUES(NEWID(),N'RPT-GATE-LOCATION',N'RPT-MAIN',N'Emplacement RPT',1,1,@anchor);
            SET @locationId=SCOPE_IDENTITY();

            INSERT inventory.StockBatches(CreationOperationId,CreationFingerprint,ProductId,BatchNumber,ExpiryDate,IsActive,CreatedAtUtc)
            VALUES(NEWID(),N'RPT-A-5',@productA,N'A-PLUS-5',DATEADD(day,5,CONVERT(date,@anchor)),1,@anchor);
            SET @batchA5=SCOPE_IDENTITY();
            INSERT inventory.StockBatches(CreationOperationId,CreationFingerprint,ProductId,BatchNumber,ExpiryDate,IsActive,CreatedAtUtc)
            VALUES(NEWID(),N'RPT-A-NULL',@productA,N'A-NO-EXPIRY',NULL,1,@anchor);
            SET @batchANull=SCOPE_IDENTITY();
            INSERT inventory.StockBatches(CreationOperationId,CreationFingerprint,ProductId,BatchNumber,ExpiryDate,IsActive,CreatedAtUtc)
            VALUES(NEWID(),N'RPT-A-EXP',@productA,N'A-EXPIRED',DATEADD(day,-1,CONVERT(date,@anchor)),1,@anchor);
            SET @batchAExpired=SCOPE_IDENTITY();
            INSERT inventory.StockBatches(CreationOperationId,CreationFingerprint,ProductId,BatchNumber,ExpiryDate,IsActive,CreatedAtUtc)
            VALUES(NEWID(),N'RPT-B-30',@productB,N'B-PLUS-30',DATEADD(day,30,CONVERT(date,@anchor)),1,@anchor);
            SET @batchB30=SCOPE_IDENTITY();
            INSERT inventory.StockBatches(CreationOperationId,CreationFingerprint,ProductId,BatchNumber,ExpiryDate,IsActive,CreatedAtUtc)
            VALUES(NEWID(),N'RPT-B-31',@productB,N'B-PLUS-31',DATEADD(day,31,CONVERT(date,@anchor)),1,@anchor);
            SET @batchB31=SCOPE_IDENTITY();

            INSERT inventory.StockBalances(StockLocationId,ProductId,StockBatchId,AvailableQuantity,UpdatedAtUtc) VALUES(@locationId,@productA,@batchA5,2,@anchor);
            SET @balanceA5=SCOPE_IDENTITY();
            INSERT inventory.StockBalances(StockLocationId,ProductId,StockBatchId,AvailableQuantity,UpdatedAtUtc) VALUES(@locationId,@productA,@batchANull,2,@anchor);
            SET @balanceANull=SCOPE_IDENTITY();
            INSERT inventory.StockBalances(StockLocationId,ProductId,StockBatchId,AvailableQuantity,UpdatedAtUtc) VALUES(@locationId,@productA,@batchAExpired,3,@anchor);
            INSERT inventory.StockBalances(StockLocationId,ProductId,StockBatchId,AvailableQuantity,UpdatedAtUtc) VALUES(@locationId,@productB,@batchB30,2,@anchor);
            INSERT inventory.StockBalances(StockLocationId,ProductId,StockBatchId,AvailableQuantity,UpdatedAtUtc) VALUES(@locationId,@productB,@batchB31,3,@anchor);

            INSERT customers.Customers(Reference,FullName,IsActive,CreditAllowed,CreatedAtUtc,UpdatedAtUtc)
            VALUES(N'RPT-CUST-A',N'Client A',1,1,@anchor,@anchor);
            SET @customerA=SCOPE_IDENTITY();
            INSERT customers.Customers(Reference,FullName,IsActive,CreditAllowed,CreatedAtUtc,UpdatedAtUtc)
            VALUES(N'RPT-CUST-B',N'Client B',1,1,@anchor,@anchor);
            SET @customerB=SCOPE_IDENTITY();
            INSERT customers.Customers(Reference,FullName,IsActive,CreditAllowed,CreatedAtUtc,UpdatedAtUtc)
            VALUES(N'RPT-CUST-C',N'Client C',1,1,@anchor,@anchor);
            SET @customerC=SCOPE_IDENTITY();
            INSERT customers.CustomerLedgerEntries(OperationId,CustomerId,EntryType,Amount,SourceType,SourceReference,OccurredAtUtc,CreatedAtUtc)
            VALUES(NEWID(),@customerA,N'CreditSale',60,N'Sale',N'RPT-SALE-A-FROM',DATEADD(hour,1,@anchor),DATEADD(hour,1,@anchor)),
                  (NEWID(),@customerC,N'CreditSale',25,N'Sale',N'RPT-CREDIT-C',DATEADD(hour,2,@anchor),DATEADD(hour,2,@anchor));

            -- Sale immediately before the selected day.
            DECLARE @op uniqueidentifier=NEWID();
            INSERT sales.SaleOperations(OperationId,OperationFingerprint,Status,CreatedAtUtc) VALUES(@op,N'RPT-BEFORE',N'Processing',DATEADD(second,-1,@anchor));
            INSERT sales.Sales(Reference,OperationId,Status,CompletedAtUtc,GrandTotal,PaidTotal,CreditTotal,ChangeGiven,TotalQuantity,LineCount,PaymentMethod)
            VALUES(N'RPT-BEFORE',@op,N'Completed',DATEADD(second,-1,@anchor),1,1,0,0,1,1,N'Cash');
            SET @saleBefore=SCOPE_IDENTITY();
            UPDATE sales.SaleOperations SET Status=N'Completed',ResultSaleId=@saleBefore,CompletedAtUtc=DATEADD(second,-1,@anchor) WHERE OperationId=@op;
            INSERT sales.SaleLines(SaleId,ProductId,ProductReferenceSnapshot,ProductNameSnapshot,UnitPrice,Quantity,LineTotal)
            VALUES(@saleBefore,@productC,N'RPT-C',N'Produit C',1,1,1);

            -- Sale A: exact lower boundary, two lines, cash + credit, and two FEFO allocations on one line.
            SET @op=NEWID();
            INSERT sales.SaleOperations(OperationId,OperationFingerprint,Status,CreatedAtUtc) VALUES(@op,N'RPT-SALE-A',N'Processing',@anchor);
            INSERT sales.Sales(Reference,OperationId,Status,CompletedAtUtc,GrandTotal,PaidTotal,CreditTotal,ChangeGiven,TotalQuantity,LineCount,PaymentMethod,CustomerId)
            VALUES(N'RPT-SALE-A-FROM',@op,N'Completed',@anchor,100,40,60,0,5,2,N'Cash',@customerA);
            SET @saleA=SCOPE_IDENTITY();
            UPDATE sales.SaleOperations SET Status=N'Completed',ResultSaleId=@saleA,CompletedAtUtc=@anchor WHERE OperationId=@op;
            INSERT sales.SaleLines(SaleId,ProductId,ProductReferenceSnapshot,ProductNameSnapshot,UnitPrice,Quantity,LineTotal)
            VALUES(@saleA,@productA,N'RPT-A',N'Paracétamol',20,3,60);
            SET @lineA1=SCOPE_IDENTITY();
            INSERT sales.SaleLines(SaleId,ProductId,ProductReferenceSnapshot,ProductNameSnapshot,UnitPrice,Quantity,LineTotal)
            VALUES(@saleA,@productC,N'RPT-C',N'Produit C',20,2,40);
            SET @lineA2=SCOPE_IDENTITY();
            INSERT sales.SaleLineAllocations(SaleLineId,StockBalanceId,StockBatchId,Quantity,BatchNumberSnapshot,ExpiryDateSnapshot)
            VALUES(@lineA1,@balanceA5,@batchA5,1,N'A-PLUS-5',DATEADD(day,5,CONVERT(date,@anchor))),
                  (@lineA1,@balanceANull,@batchANull,2,N'A-NO-EXPIRY',NULL);
            INSERT sales.Payments(SaleId,OperationId,PaymentMethod,Status,Amount,TenderedAmount,ChangeGiven,ExternalCardConfirmed,ConfirmedAtUtc)
            VALUES(@saleA,NEWID(),N'Cash',N'Completed',40,40,0,0,DATEADD(hour,1,@anchor));

            -- Sale B: one line and two external-card payment rows; no cash movement.
            SET @op=NEWID();
            INSERT sales.SaleOperations(OperationId,OperationFingerprint,Status,CreatedAtUtc) VALUES(@op,N'RPT-SALE-B',N'Processing',DATEADD(hour,8,@anchor));
            INSERT sales.Sales(Reference,OperationId,Status,CompletedAtUtc,GrandTotal,PaidTotal,CreditTotal,ChangeGiven,TotalQuantity,LineCount,PaymentMethod)
            VALUES(N'RPT-SALE-B-INSIDE',@op,N'Completed',DATEADD(hour,8,@anchor),10.99,10.99,0,0,2,1,N'ExternalCard');
            SET @saleB=SCOPE_IDENTITY();
            UPDATE sales.SaleOperations SET Status=N'Completed',ResultSaleId=@saleB,CompletedAtUtc=DATEADD(hour,8,@anchor) WHERE OperationId=@op;
            INSERT sales.SaleLines(SaleId,ProductId,ProductReferenceSnapshot,ProductNameSnapshot,UnitPrice,Quantity,LineTotal)
            VALUES(@saleB,@productA,N'RPT-A',N'Paracétamol',5.495,2,10.99);
            SET @lineB1=SCOPE_IDENTITY();
            INSERT sales.Payments(SaleId,OperationId,PaymentMethod,Status,Amount,TenderedAmount,ChangeGiven,ExternalCardConfirmed,ConfirmedAtUtc)
            VALUES(@saleB,NEWID(),N'ExternalCard',N'Completed',5,NULL,0,1,DATEADD(hour,8,@anchor)),
                  (@saleB,NEWID(),N'ExternalCard',N'Completed',5.99,NULL,0,1,DATEADD(hour,8,@anchor));

            -- Sale C: three lines carrying exact decimal values 0.01 and 0.10.
            SET @op=NEWID();
            INSERT sales.SaleOperations(OperationId,OperationFingerprint,Status,CreatedAtUtc) VALUES(@op,N'RPT-SALE-C',N'Processing',DATEADD(hour,18,@anchor));
            INSERT sales.Sales(Reference,OperationId,Status,CompletedAtUtc,GrandTotal,PaidTotal,CreditTotal,ChangeGiven,TotalQuantity,LineCount,PaymentMethod)
            VALUES(N'RPT-SALE-C-INSIDE',@op,N'Completed',DATEADD(hour,18,@anchor),0.11,0.11,0,0,3,3,N'ExternalCard');
            SET @saleC=SCOPE_IDENTITY();
            UPDATE sales.SaleOperations SET Status=N'Completed',ResultSaleId=@saleC,CompletedAtUtc=DATEADD(hour,18,@anchor) WHERE OperationId=@op;
            INSERT sales.SaleLines(SaleId,ProductId,ProductReferenceSnapshot,ProductNameSnapshot,UnitPrice,Quantity,LineTotal)
            VALUES(@saleC,@productB,N'RPT-B',N'Crème دواء',0.01,1,0.01);
            SET @lineC1=SCOPE_IDENTITY();
            INSERT sales.SaleLines(SaleId,ProductId,ProductReferenceSnapshot,ProductNameSnapshot,UnitPrice,Quantity,LineTotal)
            VALUES(@saleC,@productB,N'RPT-B',N'Crème دواء',0.10,1,0.10);
            SET @lineC2=SCOPE_IDENTITY();
            INSERT sales.SaleLines(SaleId,ProductId,ProductReferenceSnapshot,ProductNameSnapshot,UnitPrice,Quantity,LineTotal)
            VALUES(@saleC,@productB,N'RPT-B',N'Crème دواء',0,1,0);
            SET @lineC3=SCOPE_IDENTITY();
            INSERT sales.Payments(SaleId,OperationId,PaymentMethod,Status,Amount,TenderedAmount,ChangeGiven,ExternalCardConfirmed,ConfirmedAtUtc)
            VALUES(@saleC,NEWID(),N'ExternalCard',N'Completed',0.11,NULL,0,1,DATEADD(hour,18,@anchor));

            -- Two sales on day 2 and one exactly at the day-3 upper bound.
            SET @op=NEWID();
            INSERT sales.SaleOperations(OperationId,OperationFingerprint,Status,CreatedAtUtc) VALUES(@op,N'RPT-DAY2-A',N'Processing',DATEADD(day,1,@anchor));
            INSERT sales.Sales(Reference,OperationId,Status,CompletedAtUtc,GrandTotal,PaidTotal,CreditTotal,ChangeGiven,TotalQuantity,LineCount,PaymentMethod)
            VALUES(N'RPT-DAY2-AT-TO',@op,N'Completed',DATEADD(day,1,@anchor),5,5,0,0,1,1,N'Cash');
            SET @saleDay2A=SCOPE_IDENTITY(); UPDATE sales.SaleOperations SET Status=N'Completed',ResultSaleId=@saleDay2A,CompletedAtUtc=DATEADD(day,1,@anchor) WHERE OperationId=@op;
            INSERT sales.SaleLines(SaleId,ProductId,ProductReferenceSnapshot,ProductNameSnapshot,UnitPrice,Quantity,LineTotal) VALUES(@saleDay2A,@productC,N'RPT-C',N'Produit C',5,1,5);

            SET @op=NEWID();
            INSERT sales.SaleOperations(OperationId,OperationFingerprint,Status,CreatedAtUtc) VALUES(@op,N'RPT-DAY2-B',N'Processing',DATEADD(millisecond,1,DATEADD(day,1,@anchor)));
            INSERT sales.Sales(Reference,OperationId,Status,CompletedAtUtc,GrandTotal,PaidTotal,CreditTotal,ChangeGiven,TotalQuantity,LineCount,PaymentMethod)
            VALUES(N'RPT-DAY2-AFTER-TO',@op,N'Completed',DATEADD(millisecond,1,DATEADD(day,1,@anchor)),7,7,0,0,1,1,N'Cash');
            SET @saleDay2B=SCOPE_IDENTITY(); UPDATE sales.SaleOperations SET Status=N'Completed',ResultSaleId=@saleDay2B,CompletedAtUtc=DATEADD(millisecond,1,DATEADD(day,1,@anchor)) WHERE OperationId=@op;
            INSERT sales.SaleLines(SaleId,ProductId,ProductReferenceSnapshot,ProductNameSnapshot,UnitPrice,Quantity,LineTotal) VALUES(@saleDay2B,@productC,N'RPT-C',N'Produit C',7,1,7);

            SET @op=NEWID();
            INSERT sales.SaleOperations(OperationId,OperationFingerprint,Status,CreatedAtUtc) VALUES(@op,N'RPT-DAY3',N'Processing',DATEADD(day,2,@anchor));
            INSERT sales.Sales(Reference,OperationId,Status,CompletedAtUtc,GrandTotal,PaidTotal,CreditTotal,ChangeGiven,TotalQuantity,LineCount,PaymentMethod)
            VALUES(N'RPT-DAY3',@op,N'Completed',DATEADD(day,2,@anchor),10,10,0,0,1,1,N'Cash');
            SET @saleDay3=SCOPE_IDENTITY(); UPDATE sales.SaleOperations SET Status=N'Completed',ResultSaleId=@saleDay3,CompletedAtUtc=DATEADD(day,2,@anchor) WHERE OperationId=@op;
            INSERT sales.SaleLines(SaleId,ProductId,ProductReferenceSnapshot,ProductNameSnapshot,UnitPrice,Quantity,LineTotal) VALUES(@saleDay3,@productC,N'RPT-C',N'Produit C',10,1,10);

            -- One logical two-line return plus one cash return.
            INSERT sales.SaleReturns(OperationId,Reference,OriginalSaleId,Status,RefundAmount,RefundMethod,CompletedAtUtc)
            VALUES(NEWID(),N'RPT-RETURN-A',@saleA,N'Completed',20,N'CreditReduction',DATEADD(hour,20,@anchor));
            SET @returnA=SCOPE_IDENTITY();
            INSERT sales.SaleReturnLines(SaleReturnId,SaleLineId,ProductId,Quantity,UnitPrice,LineTotal,StockDestination)
            VALUES(@returnA,@lineA1,@productA,1,10,10,N'RestockApproved'),
                  (@returnA,@lineA2,@productC,1,10,10,N'NotRestockable');
            INSERT sales.SaleReturns(OperationId,Reference,OriginalSaleId,Status,RefundAmount,RefundMethod,CompletedAtUtc)
            VALUES(NEWID(),N'RPT-RETURN-B',@saleB,N'Completed',0.99,N'Cash',DATEADD(hour,21,@anchor));
            SET @returnB=SCOPE_IDENTITY();
            INSERT sales.SaleReturnLines(SaleReturnId,SaleLineId,ProductId,Quantity,UnitPrice,LineTotal,StockDestination)
            VALUES(@returnB,@lineB1,@productA,1,0.99,0.99,N'NotRestockable');

            INSERT cash.CashLedgerEntries(OperationId,EntryType,Amount,SourceType,SourceReference,OccurredAtUtc,CreatedAtUtc)
            VALUES(NEWID(),N'SaleCashIn',40,N'Sale',N'RPT-SALE-A-FROM',DATEADD(hour,1,@anchor),DATEADD(hour,1,@anchor)),
                  (NEWID(),N'CreditPaymentCashIn',20,N'CreditPayment',N'RPT-DEBT-PAY',DATEADD(hour,2,@anchor),DATEADD(hour,2,@anchor)),
                  (NEWID(),N'RefundCashOut',-0.99,N'SaleReturn',N'RPT-RETURN-B',DATEADD(hour,21,@anchor),DATEADD(hour,21,@anchor));

            INSERT inventory.StockMovements(OperationId,OperationFingerprint,ProductId,StockLocationId,StockBatchId,MovementType,Quantity,QuantityBefore,QuantityAfter,Reason,SourceType,SourceReference,OccurredAtUtc,CreatedAtUtc)
            VALUES(NEWID(),N'RPT-ADJ-PLUS',@productA,@locationId,@batchA5,N'InventoryIncrease',2,4,6,N'Inventaire +2',N'Inventory',N'RPT-ADJ-PLUS',DATEADD(hour,6,@anchor),DATEADD(hour,6,@anchor)),
                  (NEWID(),N'RPT-ADJ-MINUS',@productB,@locationId,@batchB30,N'InventoryDecrease',1,5,4,N'Inventaire -1',N'Inventory',N'RPT-ADJ-MINUS',DATEADD(hour,7,@anchor),DATEADD(hour,7,@anchor));

            INSERT purchasing.Suppliers(Code,Name,IsActive,CreatedAtUtc,UpdatedAtUtc)
            VALUES(N'RPT-SUP',N'Fournisseur RPT',1,@anchor,@anchor);
            SET @supplierId=SCOPE_IDENTITY();
            INSERT purchasing.PurchaseOrders(OrderNumber,SupplierId,Status,OrderDate,ExpectedDate,CreatedAtUtc,UpdatedAtUtc,ConfirmedAtUtc)
            VALUES(N'RPT-PO-001',@supplierId,N'PartiallyReceived',CONVERT(date,@anchor),DATEADD(day,1,CONVERT(date,@anchor)),DATEADD(hour,3,@anchor),DATEADD(hour,3,@anchor),DATEADD(hour,3,@anchor));
            SET @orderId=SCOPE_IDENTITY();
            INSERT purchasing.PurchaseOrderLines(PurchaseOrderId,ProductId,ProductCodeSnapshot,ProductNameSnapshot,OrderedQuantity,ReceivedQuantity,UnitPurchasePrice,CreatedAtUtc,UpdatedAtUtc)
            VALUES(@orderId,@productA,N'RPT-A',N'Paracétamol',10,4,5,DATEADD(hour,3,@anchor),DATEADD(hour,3,@anchor));
            SET @orderLineA=SCOPE_IDENTITY();
            INSERT purchasing.PurchaseOrderLines(PurchaseOrderId,ProductId,ProductCodeSnapshot,ProductNameSnapshot,OrderedQuantity,ReceivedQuantity,UnitPurchasePrice,CreatedAtUtc,UpdatedAtUtc)
            VALUES(@orderId,@productB,N'RPT-B',N'Crème دواء',20,5,9,DATEADD(hour,3,@anchor),DATEADD(hour,3,@anchor));
            SET @orderLineB=SCOPE_IDENTITY();

            INSERT inventory.StockMovements(OperationId,OperationFingerprint,ProductId,StockLocationId,StockBatchId,MovementType,Quantity,QuantityBefore,QuantityAfter,Reason,SourceType,SourceReference,OccurredAtUtc,CreatedAtUtc)
            VALUES(NEWID(),N'RPT-RECEIPT-A',@productA,@locationId,@batchA5,N'PurchaseReceipt',4,0,4,N'Réception A',N'GoodsReceipt',N'RPT-GR-001',DATEADD(hour,4,@anchor),DATEADD(hour,4,@anchor));
            SET @movementReceiptA=SCOPE_IDENTITY();
            INSERT purchasing.GoodsReceipts(ReceiptNumber,OperationId,OperationFingerprint,SupplierId,PurchaseOrderId,StockLocationId,ReceiptType,Status,DeliveryNoteNumber,DocumentDate,ReceivedAtUtc,CreatedAtUtc)
            VALUES(N'RPT-GR-001',NEWID(),N'RPT-GR-001',@supplierId,@orderId,@locationId,N'PurchaseOrder',N'Completed',N'RPT-BL-001',CONVERT(date,@anchor),DATEADD(hour,4,@anchor),DATEADD(hour,4,@anchor));
            SET @receiptA=SCOPE_IDENTITY();
            INSERT purchasing.GoodsReceiptLines(GoodsReceiptId,ProductId,PurchaseOrderLineId,Quantity,UnitPurchasePrice,ProductCodeSnapshot,ProductNameSnapshot,StockBatchId,LotNumberSnapshot,ExpiryDateSnapshot,StockMovementId,CreatedAtUtc)
            VALUES(@receiptA,@productA,@orderLineA,4,5,N'RPT-A',N'Paracétamol',@batchA5,N'A-PLUS-5',DATEADD(day,5,CONVERT(date,@anchor)),@movementReceiptA,DATEADD(hour,4,@anchor));

            INSERT inventory.StockMovements(OperationId,OperationFingerprint,ProductId,StockLocationId,StockBatchId,MovementType,Quantity,QuantityBefore,QuantityAfter,Reason,SourceType,SourceReference,OccurredAtUtc,CreatedAtUtc)
            VALUES(NEWID(),N'RPT-RECEIPT-B',@productB,@locationId,@batchB30,N'PurchaseReceipt',5,0,5,N'Réception B',N'GoodsReceipt',N'RPT-GR-002',DATEADD(hour,5,@anchor),DATEADD(hour,5,@anchor));
            SET @movementReceiptB=SCOPE_IDENTITY();
            INSERT purchasing.GoodsReceipts(ReceiptNumber,OperationId,OperationFingerprint,SupplierId,PurchaseOrderId,StockLocationId,ReceiptType,Status,DeliveryNoteNumber,DocumentDate,ReceivedAtUtc,CreatedAtUtc)
            VALUES(N'RPT-GR-002',NEWID(),N'RPT-GR-002',@supplierId,@orderId,@locationId,N'PurchaseOrder',N'Completed',N'RPT-BL-002',CONVERT(date,@anchor),DATEADD(hour,5,@anchor),DATEADD(hour,5,@anchor));
            SET @receiptB=SCOPE_IDENTITY();
            INSERT purchasing.GoodsReceiptLines(GoodsReceiptId,ProductId,PurchaseOrderLineId,Quantity,UnitPurchasePrice,ProductCodeSnapshot,ProductNameSnapshot,StockBatchId,LotNumberSnapshot,ExpiryDateSnapshot,StockMovementId,CreatedAtUtc)
            VALUES(@receiptB,@productB,@orderLineB,5,9,N'RPT-B',N'Crème دواء',@batchB30,N'B-PLUS-30',DATEADD(day,30,CONVERT(date,@anchor)),@movementReceiptB,DATEADD(hour,5,@anchor));
            """;

        await ExecuteAsync(sql, P("@anchor", AnchorUtc));
    }

    private async Task ExecuteAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(databaseConnection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 120;
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task ExecuteServerAsync(string sql)
    {
        await using var connection = new SqlConnection(serverConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ServerScalarAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(serverConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        Assert.NotNull(value);
        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    private static void ValidateDatabaseName(string value) =>
        Assert.Matches(new Regex($"^{Prefix}[0-9a-f]{{12}}$", RegexOptions.CultureInvariant), value);

    private sealed class TestDbContextFactory(DbContextOptions<PharmacyDbContext> options)
        : IDbContextFactory<PharmacyDbContext>
    {
        public PharmacyDbContext CreateDbContext() => new(options);
        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class FixedClock(DateTime utcNow) : ISecurityClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
