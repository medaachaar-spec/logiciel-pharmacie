using Pharmacy.Application.Reporting.Contracts;
using Pharmacy.Application.Reporting.Errors;
using Pharmacy.Application.Reporting.Models;
using Pharmacy.Desktop.Components.Reporting;
using Pharmacy.Domain.Common;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class ReportingCoordinatorGateTests
{
    [Fact]
    public async Task RPT007_SwitchingFromPopulatedToEmptyClearsPreviousProjection()
    {
        var populated = Snapshot(saleCount: 3, grossSales: 111.10m);
        var empty = Snapshot(saleCount: 0, grossSales: 0m);
        var coordinator = new ReportingCoordinator(new SequencedReportingService(
            Result<ReportingSnapshotView>.Success(populated),
            Result<ReportingSnapshotView>.Success(empty)));

        await coordinator.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, coordinator.Snapshot!.Summary.SaleCount);

        await coordinator.LoadAsync(TestContext.Current.CancellationToken);

        Assert.NotSame(populated, coordinator.Snapshot);
        Assert.Equal(0, coordinator.Snapshot!.Summary.SaleCount);
        Assert.Equal(0m, coordinator.Snapshot.Summary.GrossSales);
        Assert.Empty(coordinator.Snapshot.Sales);
        Assert.Null(coordinator.ErrorCode);
    }

    [Fact]
    public async Task RPT033_SQLFailureClearsPreviouslyDisplayedSnapshot()
    {
        var coordinator = new ReportingCoordinator(new SequencedReportingService(
            Result<ReportingSnapshotView>.Success(Snapshot(3, 111.10m)),
            Result<ReportingSnapshotView>.Failure(ReportingErrors.DatabaseUnavailable)));

        await coordinator.LoadAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(coordinator.Snapshot);

        await coordinator.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Null(coordinator.Snapshot);
        Assert.Equal(ReportingErrors.DatabaseUnavailable.Code, coordinator.ErrorCode);
        Assert.False(coordinator.IsBusy);
    }

    private static ReportingSnapshotView Snapshot(int saleCount, decimal grossSales)
    {
        var period = new ReportingPeriod(
            new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 21, 0, 0, 0, DateTimeKind.Utc));
        var summary = new ExecutiveSummaryView(
            period,
            period.FromUtc,
            "MAD",
            grossSales,
            0m,
            grossSales,
            saleCount,
            0,
            [],
            0m,
            0m,
            0,
            0,
            0,
            0,
            0,
            0,
            0);
        var sales = saleCount == 0
            ? Array.Empty<SaleReportRow>()
            : new[]
            {
                new SaleReportRow(1, "RPT-POPULATED", period.FromUtc, "Cash", 1, 1,
                    grossSales, grossSales, 0m, 0m, grossSales)
            };
        return new ReportingSnapshotView(
            summary,
            new AlertSettingsView(false, false, null, period.FromUtc, [1]),
            sales,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            []);
    }

    private sealed class SequencedReportingService(params Result<ReportingSnapshotView>[] snapshots)
        : IReportingService
    {
        private readonly Queue<Result<ReportingSnapshotView>> pending = new(snapshots);

        public Task<Result<ReportingSnapshotView>> GetSnapshotAsync(
            ReportingQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(pending.Dequeue());

        public Task<Result<AlertSettingsView>> UpdateSettingsAsync(
            UpdateAlertSettingsRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<CsvExportView>> ExportCsvAsync(
            ReportingExportRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
