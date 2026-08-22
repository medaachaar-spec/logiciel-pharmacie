using System.Data;
using Microsoft.Data.SqlClient;
using Pharmacy.Application.Dashboard.Models;
using Pharmacy.Infrastructure.Reporting;
using Xunit;

namespace Pharmacy.IntegrationTests;

/// <summary>
/// ZCODE-04A / ZC-019: semantics of "Aujourd’hui" dashboard labels.
/// UTC persistence is intentional and correct; this test documents the
/// business-day BOUNDARY semantics of SqlDashboardService around a
/// local-midnight instant where the UTC calendar date differs from the
/// local calendar date. The assumed business timezone for the demonstration
/// is UTC+01:00 (Africa/Casablanca), labeled explicitly because the
/// repository defines NO business-timezone policy (see the ZCODE-04A report:
/// classification TIMEZONE POLICY REQUIRED). The test stays green and records
/// the exact behavior; it does not invent a production timezone.
/// </summary>
[Collection("Sprint 09 persistence serial")]
public sealed class Zc04BusinessDayTests
{
    private const string OwnerPassword = "Zc04-Owner-Password-2026!";
    private static readonly TimeSpan AssumedBusinessOffset = TimeSpan.FromHours(1);

    [Fact]
    public async Task ZC04A09_DashboardTodayWindowUsesUtcMidnightAroundLocalMidnightBoundary()
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

            // Business date D = 2026-08-22 in the assumed UTC+01:00 business timezone.
            // Sale A: 2026-08-22 00:30 local (= 2026-08-21 23:30 UTC) — local date D,
            //         UTC calendar date D-1.
            // Sale B: 2026-08-22 12:00 local (= 2026-08-22 11:00 UTC) — local date D,
            //         UTC calendar date D.
            var saleAUtc = new DateTime(2026, 8, 21, 23, 30, 0, DateTimeKind.Utc);
            var saleBUtc = new DateTime(2026, 8, 22, 11, 0, 0, DateTimeKind.Utc);
            var viewingMomentUtc = new DateTime(2026, 8, 22, 10, 0, 0, DateTimeKind.Utc);

            await Zc04ReportingCsvExportTests.SeedFinalizedSalesAsync(databaseConnection, saleAUtc, 1, "A09A");
            await Zc04ReportingCsvExportTests.SeedFinalizedSalesAsync(databaseConnection, saleBUtc, 1, "A09B");

            var clock = new Zc04.Zc04Clock(viewingMomentUtc);
            var owner = await Zc04.BootstrapOwnerAsync(factory, clock, "zc04.owner.day", OwnerPassword);
            var dashboard = new SqlDashboardService(factory, owner.Authorizer, clock);

            var globalSales = await dashboard.GetGlobalSalesAsync(new DashboardSalesQuery(Days: 7), token);
            Assert.True(globalSales.IsSuccess, globalSales.IsFailure ? globalSales.Error.Code : null);

            var saleALocal = saleAUtc + AssumedBusinessOffset;
            var saleBLocal = saleBUtc + AssumedBusinessOffset;
            var viewingLocal = viewingMomentUtc + AssumedBusinessOffset;

            // Premise: the demonstration boundary is real — Sale A's local calendar date
            // matches the viewing local date while its UTC calendar date does not.
            Assert.Equal(
                DateOnly.FromDateTime(viewingLocal),
                DateOnly.FromDateTime(saleALocal));
            Assert.NotEqual(
                DateOnly.FromDateTime(saleAUtc),
                DateOnly.FromDateTime(saleALocal));

            // Documented behavior (evidence for the report): the dashboard "Aujourd’hui"
            // window [UTC midnight, UTC midnight+1) around the viewing instant counts ONLY
            // Sale B; Sale A — same local business date, 30 minutes after local midnight —
            // is attributed to the previous business day. This is correct per the (absent)
            // UTC-only policy and incorrect per a local-calendar business-day expectation.
            Assert.Equal(1, globalSales.Value.TodaySaleCount);

            var todayUtc = DateTime.SpecifyKind(viewingMomentUtc.Date, DateTimeKind.Utc);
            var counted = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM sales.Sales WHERE CompletedAtUtc >= @from AND CompletedAtUtc < @to;",
                new SqlParameter("@from", SqlDbType.DateTime2) { Value = todayUtc },
                new SqlParameter("@to", SqlDbType.DateTime2) { Value = todayUtc.AddDays(1) });
            Assert.Equal(counted, globalSales.Value.TodaySaleCount);
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }
}
