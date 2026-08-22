using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using Pharmacy.Application.Reporting.Contracts;
using Pharmacy.Application.Reporting.Models;
using Pharmacy.Infrastructure.Reporting;
using Xunit;
using Pharmacy.Domain.Sales;

namespace Pharmacy.IntegrationTests;

/// <summary>
/// ZCODE-04A / ZC-004: CSV export completeness above the reporting row limit.
/// Seeds boundary-sized sets of finalized sales inside one report period in a
/// disposable SQL database, then invokes the REAL reporting/export workflow with the same
/// default query the Desktop ReportingCoordinator builds
/// (new ReportingQuery(period) => Limit defaults to 200).
/// Invariant: a successful export contains every qualifying record.
/// </summary>
[Collection("Sprint 09 persistence serial")]
public sealed class Zc04ReportingCsvExportTests
{
    private const string OwnerPassword = "Zc04-Owner-Password-2026!";
    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(251)]
    public async Task ZC04A04_CsvExportMustContainEveryQualifyingSale(int seededSales)
    {
        var token = TestContext.Current.CancellationToken;
        var server = Zc04.BuildServerConnection();
        var databaseName = Zc04.NewDatabaseName();
        var databaseConnection = Zc04.BuildDatabaseConnection(server.ConnectionString, databaseName);
        await Zc04.CreateDatabaseAsync(server.ConnectionString, databaseName);

        try
        {
            await Zc04.MigrateAsync(databaseConnection);
            var factory = new Zc04.Factory(databaseConnection);
            var clock = new Zc04.Zc04Clock(new DateTime(2026, 8, 22, 11, 0, 0, DateTimeKind.Utc));
            var owner = await Zc04.BootstrapOwnerAsync(factory, clock, "zc04.owner.rep", OwnerPassword);

            var seeded = await SeededSaleFactory.SeedProductWithStockAsync(
                factory, clock, "REP", quantity: 1000, token: token);

            var saleTimestamp = clock.UtcNow.AddMinutes(-30);
            await SeedFinalizedSalesAsync(databaseConnection, saleTimestamp, seededSales);

            var qualifyingCount = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM sales.Sales WHERE CompletedAtUtc >= @from AND CompletedAtUtc < @to;",
                new SqlParameter("@from", SqlDbType.DateTime2) { Value = saleTimestamp.AddHours(-1) },
                new SqlParameter("@to", SqlDbType.DateTime2) { Value = saleTimestamp.AddHours(1) });
            Assert.Equal(seededSales, qualifyingCount);

            var reporting = new SqlReportingService(factory, owner.Authorizer, new Zc04.NoOpAuditRecorder(), clock);

            // Exactly what ReportingCoordinator.Query() builds: default Limit = 200.
            var period = new ReportingPeriod(
                saleTimestamp.AddHours(-1),
                saleTimestamp.AddHours(1));
            var query = new ReportingQuery(period);

            var snapshot = await reporting.GetSnapshotAsync(query, token);
            Assert.True(snapshot.IsSuccess, snapshot.IsFailure ? snapshot.Error.Code : null);

            var export = await reporting.ExportCsvAsync(
                new ReportingExportRequest(Guid.NewGuid(), ReportingExportKind.Sales, query), token);
            Assert.True(export.IsSuccess, export.IsFailure ? export.Error.Code : null);

            var csvRows = CountCsvDataRows(export.Value.Content);
            var summaryCount = snapshot.Value.Summary.SaleCount;
            Assert.Equal(qualifyingCount, summaryCount);
            Assert.Equal(qualifyingCount, csvRows);
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }

    /// <summary>Bulk-seeds finalized cash sales (same shape SqlSaleFinalizer persists).</summary>
    internal static async Task SeedFinalizedSalesAsync(
        string databaseConnection,
        DateTime completedAtUtc,
        int count,
        string referencePrefix = "ZC04-VTE")
    {
        await using var connection = new SqlConnection(databaseConnection);
        await connection.OpenAsync();
        for (var index = 1; index <= count; index++)
        {
            var operationId = Guid.NewGuid();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @saleId bigint;
                INSERT INTO sales.SaleOperations (OperationId, OperationFingerprint, Status, CreatedAtUtc)
                VALUES (@operationId, @fingerprint, N'Processing', @completedAtUtc);
                INSERT INTO sales.Sales
                    (Reference, OperationId, Status, CompletedAtUtc, GrandTotal, PaidTotal, CreditTotal,
                     ChangeGiven, TotalQuantity, LineCount, PaymentMethod, CustomerId, FinalizedByUserAccountId)
                VALUES
                    (@reference, @operationId, N'Completed', @completedAtUtc, 10, 10, 0, 0, 1, 1, N'Cash', NULL, NULL);
                SET @saleId = SCOPE_IDENTITY();
                UPDATE sales.SaleOperations
                SET Status = N'Completed', ResultSaleId = @saleId, CompletedAtUtc = @completedAtUtc
                WHERE OperationId = @operationId;
                """;
            command.Parameters.Add(new SqlParameter("@reference", SqlDbType.NVarChar, 64) { Value = $"{referencePrefix}-{index:000000}"});
            command.Parameters.Add(new SqlParameter("@operationId", SqlDbType.UniqueIdentifier) { Value = operationId });
            command.Parameters.Add(new SqlParameter("@fingerprint", SqlDbType.NVarChar, 128) { Value = operationId.ToString("N", System.Globalization.CultureInfo.InvariantCulture) + operationId.ToString("N", System.Globalization.CultureInfo.InvariantCulture) });
            command.Parameters.Add(new SqlParameter("@completedAtUtc", SqlDbType.DateTime2) { Value = completedAtUtc.AddSeconds(index) });
            await command.ExecuteNonQueryAsync();
        }
    }

    private static int CountCsvDataRows(byte[] content)
    {
        var text = Encoding.UTF8.GetString(content);
        var lines = text.Split('\n');
        var nonEmpty = lines.Count(line => !string.IsNullOrWhiteSpace(line));
        return nonEmpty - 1;
    }
}
