using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Reporting.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Reporting;
using Pharmacy.Infrastructure.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class SqlReportingIntegrationTests
{
    private const string Prefix = "Pharmacy_Sprint16_Test_";
    private static readonly DateTime Now = new(2026, 8, 10, 18, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RealSqlReportingValidatesViewsSettingsRbacAlertsExportAndCleanup()
    {
        var token = TestContext.Current.CancellationToken;
        var server = BuildServerConnection();
        var databaseName = Prefix + Guid.NewGuid().ToString("N")[..12];
        Assert.Matches(new Regex($"^{Prefix}[0-9a-f]{{12}}$", RegexOptions.CultureInvariant), databaseName);
        var database = new SqlConnectionStringBuilder(server)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Pharmacy.Sprint16.Tests",
            Pooling = false
        };
        await CreateDatabaseAsync(server, databaseName);

        try
        {
            var factory = new Factory(database.ConnectionString);
            await using (var context = await factory.CreateDbContextAsync(token))
            {
                await context.Database.MigrateAsync(token);
                Assert.Empty(await context.Database.GetPendingMigrationsAsync(token));
                Assert.Contains(await context.Database.GetAppliedMigrationsAsync(token), item =>
                    item.EndsWith("_ReportingViewsAndAlertSettingsV01", StringComparison.Ordinal));
            }

            Assert.Equal(11, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM sys.views v JOIN sys.schemas s ON s.schema_id=v.schema_id WHERE s.name=N'reporting';"));
            Assert.Equal(28, await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM security.Permissions;"));
            Assert.Equal(1, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM reporting.ReportSettings WHERE Id=1 AND LowStockAlertsEnabled=0 AND ExpiryAlertsEnabled=0 AND ExpiryHorizonDays IS NULL;"));

            await SeedScenarioAsync(database.ConnectionString);
            Assert.Equal(5L, await ScalarAsync<long>(database.ConnectionString,
                "SELECT SellableQuantity FROM reporting.vw_StockSummary WHERE ProductReference=N'RPT-001';"));
            Assert.Equal(100m, await ScalarAsync<decimal>(database.ConnectionString,
                "SELECT GrossAmount FROM reporting.vw_SalesSummary WHERE Reference=N'VTE-RPT-001';"));
            Assert.Equal(40m, await ScalarAsync<decimal>(database.ConnectionString,
                "SELECT RefundAmount FROM reporting.vw_SalesSummary WHERE Reference=N'VTE-RPT-001';"));
            Assert.Equal(60m, await ScalarAsync<decimal>(database.ConnectionString,
                "SELECT NetAmount FROM reporting.vw_SalesSummary WHERE Reference=N'VTE-RPT-001';"));
            Assert.Equal(2, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM reporting.vw_Payments WHERE SaleReference=N'VTE-RPT-001';"));

            var clock = new FixedClock(Now);
            var session = new CurrentSecuritySession();
            var authorizer = new PermissionAuthorizer(session, clock);
            var auditStore = new SqlAuditStore(factory, authorizer);
            var audit = new BusinessAuditRecorder(auditStore, session, clock);
            var service = new SqlReportingService(factory, authorizer, audit, clock);
            var query = new ReportingQuery(new ReportingPeriod(Now.AddDays(-30), Now.AddDays(1)));

            var denied = await service.GetSnapshotAsync(query, token);
            Assert.Equal("security.authorization.authentication_required", denied.Error.Code);

            session.Establish(new UserSessionView(
                9000, "viewer", "Lecteur reporting", ["VIEWER"],
                new HashSet<string>([SecurityPermissions.ReportsView], StringComparer.Ordinal),
                Now, Now), Now);
            var exportDenied = await service.ExportCsvAsync(
                new ReportingExportRequest(Guid.NewGuid(), ReportingExportKind.Stock, query), token);
            Assert.Equal("security.authorization.denied", exportDenied.Error.Code);
            var settingsDenied = await service.UpdateSettingsAsync(
                new UpdateAlertSettingsRequest(Guid.NewGuid(), true, true, 30, []), token);
            Assert.Equal("security.authorization.denied", settingsDenied.Error.Code);

            session.Establish(new UserSessionView(
                9001, "reporter", "Responsable reporting", ["OWNER"],
                new HashSet<string>([SecurityPermissions.ReportsView, SecurityPermissions.ReportsExport, SecurityPermissions.ReportsManageSettings], StringComparer.Ordinal),
                Now, Now), Now);

            var snapshot = await service.GetSnapshotAsync(query, token);
            Assert.True(snapshot.IsSuccess, snapshot.IsFailure ? snapshot.Error.Message : null);
            Assert.Equal(5, snapshot.Value.Summary.CurrentSellableStock);
            Assert.Equal(100m, snapshot.Value.Summary.GrossSales);
            Assert.Equal(40m, snapshot.Value.Summary.Refunds);
            Assert.Equal(60m, snapshot.Value.Summary.NetSales);
            Assert.Equal(2, snapshot.Value.Summary.Payments.Sum(item => item.Count));
            Assert.Equal(30m, snapshot.Value.Summary.OpenCredit);
            Assert.Equal(10m, snapshot.Value.Summary.CreditPayments);
            Assert.Equal(1, snapshot.Value.Summary.ExpiredBatches);
            Assert.Equal(1, snapshot.Value.Summary.PurchaseOrders);
            Assert.Equal(5, snapshot.Value.Summary.ReceivedQuantity);
            Assert.Equal(0, snapshot.Value.Summary.LowStockProducts);
            Assert.Contains(snapshot.Value.Stock, item => item.ProductReference == "RPT-001" && item.Status == "Sous seuil configuré");
            Assert.Contains(snapshot.Value.Stock, item => item.ProductReference == "RPT-002" && item.Status == "Rupture");
            Assert.Contains(snapshot.Value.ProductSales, item => item.ProductReference == "RPT-001"
                && item.SoldQuantity == 3 && item.ReturnedQuantity == 2 && item.NetAmount == 20m);
            Assert.Single(snapshot.Value.Purchasing);
            Assert.Single(snapshot.Value.InventoryAdjustments);
            Assert.Single(snapshot.Value.CustomerCredit);
            Assert.Equal(2, snapshot.Value.Returns.Count);
            Assert.Equal(3, snapshot.Value.Cash.Count);

            var settings = await service.UpdateSettingsAsync(new UpdateAlertSettingsRequest(
                Guid.NewGuid(), true, true, 30, snapshot.Value.Settings.RowVersion), token);
            Assert.True(settings.IsSuccess, settings.IsFailure ? settings.Error.Message : null);
            Assert.Equal(30, settings.Value.ExpiryHorizonDays);

            var refreshed = await service.GetSnapshotAsync(query, token);
            Assert.Equal(1, refreshed.Value.Summary.LowStockProducts);
            Assert.Equal(1, refreshed.Value.Summary.NearExpiryBatches);
            var export = await service.ExportCsvAsync(new ReportingExportRequest(Guid.NewGuid(), ReportingExportKind.Stock, query), token);
            Assert.True(export.IsSuccess, export.IsFailure ? export.Error.Message : null);
            Assert.True(export.Value.Content.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
            var csv = Encoding.UTF8.GetString(export.Value.Content);
            Assert.Contains("\"'=FORMULA\"", csv, StringComparison.Ordinal);
            Assert.DoesNotContain("Phone", csv, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Email", csv, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM audit.AuditEvents WHERE Module=N'reporting' AND Action IN (N'alerts.settings.update', N'report.export.csv');"));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(server, databaseName);
        }
    }

    private static async Task SeedScenarioAsync(string connectionString)
    {
        const string sql = """
            DECLARE @categoryId bigint, @productId bigint, @secondProductId bigint, @locationId bigint;
            DECLARE @batchOneId bigint, @batchTwoId bigint, @expiredBatchId bigint;
            DECLARE @balanceOneId bigint, @balanceTwoId bigint, @saleId bigint, @lineOneId bigint, @lineTwoId bigint;
            DECLARE @returnOneId bigint, @returnTwoId bigint, @customerId bigint, @supplierId bigint;
            DECLARE @orderId bigint, @orderLineId bigint, @movementId bigint, @receiptId bigint;
            DECLARE @saleOperation uniqueidentifier = NEWID();
            INSERT catalog.ProductCategories(Code, Name, IsActive, DefaultTracksLots, DefaultTracksExpiry, DefaultDiscountAllowed, SortOrder)
            VALUES(N'RPT', N'Reporting', 1, 1, 1, 0, 0);
            SET @categoryId = SCOPE_IDENTITY();
            INSERT catalog.Products(Name, IsActive, CreatedAtUtc, UpdatedAtUtc)
            VALUES(N'=FORMULA', 1, @now, @now);
            SET @productId = SCOPE_IDENTITY();
            INSERT catalog.ProductLocalSettings(ProductId, InternalCode, CategoryId, IsActive, IsSellable, IsPurchasable, TracksLots, TracksExpiry, DiscountAllowed, QuantityScale, MinimumStock, CurrentSalePrice, CurrencyCode)
            VALUES(@productId, N'RPT-001', @categoryId, 1, 1, 1, 1, 1, 0, 0, 10, 20, N'MAD');
            INSERT catalog.Products(Name, IsActive, CreatedAtUtc, UpdatedAtUtc)
            VALUES(N'Produit périmé', 1, @now, @now);
            SET @secondProductId = SCOPE_IDENTITY();
            INSERT catalog.ProductLocalSettings(ProductId, InternalCode, CategoryId, IsActive, IsSellable, IsPurchasable, TracksLots, TracksExpiry, DiscountAllowed, QuantityScale, MinimumStock, CurrentSalePrice, CurrencyCode)
            VALUES(@secondProductId, N'RPT-002', @categoryId, 1, 1, 1, 1, 1, 0, 0, NULL, 20, N'MAD');
            INSERT inventory.StockLocations(CreationOperationId, CreationFingerprint, Code, Name, IsActive, IsPrimary, CreatedAtUtc)
            VALUES(@operationId, N'SPRINT16-TEST', N'MAIN', N'Principal', 1, 1, @now);
            SET @locationId = SCOPE_IDENTITY();
            INSERT inventory.StockBatches(CreationOperationId, CreationFingerprint, ProductId, BatchNumber, ExpiryDate, IsActive, CreatedAtUtc)
            VALUES(NEWID(), N'RPT-BATCH-1', @productId, N'LOT-10', DATEADD(day, 10, CONVERT(date, @now)), 1, @now);
            SET @batchOneId = SCOPE_IDENTITY();
            INSERT inventory.StockBatches(CreationOperationId, CreationFingerprint, ProductId, BatchNumber, ExpiryDate, IsActive, CreatedAtUtc)
            VALUES(NEWID(), N'RPT-BATCH-2', @productId, N'LOT-60', DATEADD(day, 60, CONVERT(date, @now)), 1, @now);
            SET @batchTwoId = SCOPE_IDENTITY();
            INSERT inventory.StockBatches(CreationOperationId, CreationFingerprint, ProductId, BatchNumber, ExpiryDate, IsActive, CreatedAtUtc)
            VALUES(NEWID(), N'RPT-BATCH-EXP', @secondProductId, N'LOT-EXP', DATEADD(day, -1, CONVERT(date, @now)), 1, @now);
            SET @expiredBatchId = SCOPE_IDENTITY();
            INSERT inventory.StockBalances(StockLocationId, ProductId, StockBatchId, AvailableQuantity, UpdatedAtUtc)
            VALUES(@locationId, @productId, @batchOneId, 2, @now);
            SET @balanceOneId = SCOPE_IDENTITY();
            INSERT inventory.StockBalances(StockLocationId, ProductId, StockBatchId, AvailableQuantity, UpdatedAtUtc)
            VALUES(@locationId, @productId, @batchTwoId, 3, @now);
            SET @balanceTwoId = SCOPE_IDENTITY();
            INSERT inventory.StockBalances(StockLocationId, ProductId, StockBatchId, AvailableQuantity, UpdatedAtUtc)
            VALUES(@locationId, @secondProductId, @expiredBatchId, 4, @now);

            INSERT customers.Customers(Reference, FullName, IsActive, CreditAllowed, CreatedAtUtc, UpdatedAtUtc)
            VALUES(N'CLI-RPT-001', N'Client synthétique', 1, 1, @now, @now);
            SET @customerId = SCOPE_IDENTITY();
            INSERT sales.SaleOperations(OperationId, OperationFingerprint, Status, ResultSaleId, CreatedAtUtc, CompletedAtUtc)
            VALUES(@saleOperation, N'RPT-SALE', N'Processing', NULL, @now, NULL);
            INSERT sales.Sales(Reference, OperationId, Status, CompletedAtUtc, GrandTotal, PaidTotal, CreditTotal, ChangeGiven, TotalQuantity, LineCount, PaymentMethod, CustomerId)
            VALUES(N'VTE-RPT-001', @saleOperation, N'Completed', @now, 100, 60, 40, 0, 5, 2, N'Cash', @customerId);
            SET @saleId = SCOPE_IDENTITY();
            UPDATE sales.SaleOperations SET Status=N'Completed', ResultSaleId=@saleId, CompletedAtUtc=@now WHERE OperationId=@saleOperation;
            INSERT sales.SaleLines(SaleId, ProductId, ProductReferenceSnapshot, ProductNameSnapshot, UnitPrice, Quantity, LineTotal)
            VALUES(@saleId, @productId, N'RPT-001', N'=FORMULA', 20, 3, 60);
            SET @lineOneId = SCOPE_IDENTITY();
            INSERT sales.SaleLines(SaleId, ProductId, ProductReferenceSnapshot, ProductNameSnapshot, UnitPrice, Quantity, LineTotal)
            VALUES(@saleId, @secondProductId, N'RPT-002', N'Produit périmé', 20, 2, 40);
            SET @lineTwoId = SCOPE_IDENTITY();
            INSERT sales.SaleLineAllocations(SaleLineId, StockBalanceId, StockBatchId, Quantity, BatchNumberSnapshot, ExpiryDateSnapshot)
            VALUES(@lineOneId, @balanceOneId, @batchOneId, 1, N'LOT-10', DATEADD(day, 10, CONVERT(date, @now))),
                  (@lineOneId, @balanceTwoId, @batchTwoId, 2, N'LOT-60', DATEADD(day, 60, CONVERT(date, @now)));
            INSERT sales.Payments(SaleId, OperationId, PaymentMethod, Status, Amount, TenderedAmount, ChangeGiven, ExternalCardConfirmed, ConfirmedAtUtc)
            VALUES(@saleId, NEWID(), N'Cash', N'Completed', 30, 30, 0, 0, @now),
                  (@saleId, NEWID(), N'ExternalCard', N'Completed', 30, NULL, 0, 1, @now);
            INSERT sales.SaleReturns(OperationId, Reference, OriginalSaleId, Status, RefundAmount, RefundMethod, CompletedAtUtc)
            VALUES(NEWID(), N'RET-RPT-001', @saleId, N'Completed', 20, N'Cash', @now);
            SET @returnOneId = SCOPE_IDENTITY();
            INSERT sales.SaleReturnLines(SaleReturnId, SaleLineId, ProductId, Quantity, UnitPrice, LineTotal, StockDestination)
            VALUES(@returnOneId, @lineOneId, @productId, 1, 20, 20, N'RestockApproved');
            INSERT sales.SaleReturns(OperationId, Reference, OriginalSaleId, Status, RefundAmount, RefundMethod, CompletedAtUtc)
            VALUES(NEWID(), N'RET-RPT-002', @saleId, N'Completed', 20, N'Cash', @now);
            SET @returnTwoId = SCOPE_IDENTITY();
            INSERT sales.SaleReturnLines(SaleReturnId, SaleLineId, ProductId, Quantity, UnitPrice, LineTotal, StockDestination)
            VALUES(@returnTwoId, @lineOneId, @productId, 1, 20, 20, N'NotRestockable');

            INSERT customers.CustomerLedgerEntries(OperationId, CustomerId, EntryType, Amount, SourceType, SourceReference, OccurredAtUtc, CreatedAtUtc)
            VALUES(NEWID(), @customerId, N'CreditSale', 40, N'Sale', N'VTE-RPT-001', @now, @now),
                  (NEWID(), @customerId, N'CreditPayment', 10, N'CreditPayment', N'CRP-RPT-001', @now, @now);
            INSERT cash.CashLedgerEntries(OperationId, EntryType, Amount, SourceType, SourceReference, OccurredAtUtc, CreatedAtUtc)
            VALUES(NEWID(), N'SaleCashIn', 30, N'Sale', N'VTE-RPT-001', @now, @now),
                  (NEWID(), N'CreditPaymentCashIn', 10, N'CreditPayment', N'CRP-RPT-001', @now, @now),
                  (NEWID(), N'RefundCashOut', -40, N'SaleReturn', N'RET-RPT', @now, @now);

            INSERT inventory.StockMovements(OperationId, OperationFingerprint, ProductId, StockLocationId, StockBatchId, MovementType, Quantity, QuantityBefore, QuantityAfter, Reason, SourceType, SourceReference, OccurredAtUtc, CreatedAtUtc)
            VALUES(NEWID(), N'RPT-ADJUST', @secondProductId, @locationId, @expiredBatchId, N'ManualIncrease', 1, 4, 5, N'Ajustement synthétique', N'ManualAdjustment', N'ADJ-RPT-001', @now, @now);

            INSERT purchasing.Suppliers(Code, Name, IsActive, CreatedAtUtc, UpdatedAtUtc)
            VALUES(N'SUP-RPT', N'Fournisseur synthétique', 1, @now, @now);
            SET @supplierId = SCOPE_IDENTITY();
            INSERT purchasing.PurchaseOrders(OrderNumber, SupplierId, Status, OrderDate, ExpectedDate, CreatedAtUtc, UpdatedAtUtc, ConfirmedAtUtc)
            VALUES(N'PO-RPT-001', @supplierId, N'FullyReceived', CONVERT(date,@now), CONVERT(date,@now), @now, @now, @now);
            SET @orderId = SCOPE_IDENTITY();
            INSERT purchasing.PurchaseOrderLines(PurchaseOrderId, ProductId, ProductCodeSnapshot, ProductNameSnapshot, OrderedQuantity, ReceivedQuantity, UnitPurchasePrice, CreatedAtUtc, UpdatedAtUtc)
            VALUES(@orderId, @productId, N'RPT-001', N'=FORMULA', 5, 5, 10, @now, @now);
            SET @orderLineId = SCOPE_IDENTITY();
            INSERT inventory.StockMovements(OperationId, OperationFingerprint, ProductId, StockLocationId, StockBatchId, MovementType, Quantity, QuantityBefore, QuantityAfter, Reason, SourceType, SourceReference, OccurredAtUtc, CreatedAtUtc)
            VALUES(NEWID(), N'RPT-RECEIPT-MOVE', @productId, @locationId, @batchOneId, N'PurchaseReceipt', 5, 0, 5, N'Réception synthétique', N'GoodsReceipt', N'GR-RPT-001', @now, @now);
            SET @movementId = SCOPE_IDENTITY();
            INSERT purchasing.GoodsReceipts(ReceiptNumber, OperationId, OperationFingerprint, SupplierId, PurchaseOrderId, StockLocationId, ReceiptType, Status, DeliveryNoteNumber, DocumentDate, ReceivedAtUtc, CreatedAtUtc)
            VALUES(N'GR-RPT-001', NEWID(), N'RPT-RECEIPT', @supplierId, @orderId, @locationId, N'PurchaseOrder', N'Completed', N'BL-RPT-001', CONVERT(date,@now), @now, @now);
            SET @receiptId = SCOPE_IDENTITY();
            INSERT purchasing.GoodsReceiptLines(GoodsReceiptId, ProductId, PurchaseOrderLineId, Quantity, UnitPurchasePrice, ProductCodeSnapshot, ProductNameSnapshot, StockBatchId, LotNumberSnapshot, ExpiryDateSnapshot, StockMovementId, CreatedAtUtc)
            VALUES(@receiptId, @productId, @orderLineId, 5, 10, N'RPT-001', N'=FORMULA', @batchOneId, N'LOT-10', DATEADD(day,10,CONVERT(date,@now)), @movementId, @now);
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@now", Now);
        command.Parameters.AddWithValue("@operationId", Guid.NewGuid());
        await command.ExecuteNonQueryAsync();
    }

    private static string BuildServerConnection()
    {
        var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException("PHARMACY_TEST_SQLSERVER_CONNECTION est requise.");
        return new SqlConnectionStringBuilder(raw) { InitialCatalog = "master", Pooling = false }.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string server, string name)
    {
        await using var connection = new SqlConnection(server);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{name}];";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string server, string name)
    {
        Assert.StartsWith(Prefix, name, StringComparison.Ordinal);
        await using var connection = new SqlConnection(server);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END;";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        Assert.NotNull(value);
        return (T)Convert.ChangeType(value, typeof(T));
    }

    private sealed class Factory : IDbContextFactory<PharmacyDbContext>
    {
        private readonly DbContextOptions<PharmacyDbContext> options;
        public Factory(string connectionString)
        {
            var builder = new DbContextOptionsBuilder<PharmacyDbContext>();
            builder.UsePharmacySqlServer(connectionString);
            options = builder.Options;
        }
        public PharmacyDbContext CreateDbContext() => new(options);
        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class FixedClock(DateTime now) : ISecurityClock
    {
        public DateTime UtcNow { get; } = now;
    }
}
