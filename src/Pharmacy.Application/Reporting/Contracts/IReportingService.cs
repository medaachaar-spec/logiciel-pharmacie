using Pharmacy.Application.Reporting.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Reporting.Contracts;

public interface IReportingService
{
    Task<Result<ReportingSnapshotView>> GetSnapshotAsync(
        ReportingQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<AlertSettingsView>> UpdateSettingsAsync(
        UpdateAlertSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CsvExportView>> ExportCsvAsync(
        ReportingExportRequest request,
        CancellationToken cancellationToken = default);
}
