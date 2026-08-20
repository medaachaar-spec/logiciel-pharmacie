using Pharmacy.Application.Reporting.Contracts;
using Pharmacy.Application.Reporting.Models;
using System.IO;

namespace Pharmacy.Desktop.Components.Reporting;

public sealed class ReportingCoordinator
{
    private readonly IReportingService service;

    public ReportingCoordinator(IReportingService service)
    {
        this.service = service;
        var now = DateTime.UtcNow;
        FromDate = new DateOnly(now.Year, now.Month, 1);
        ToDate = DateOnly.FromDateTime(now).AddDays(1);
    }

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public ReportingSnapshotView? Snapshot { get; private set; }
    public bool IsBusy { get; private set; }
    public string? Message { get; private set; }
    public string? ErrorCode { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        Message = null;
        ErrorCode = null;
        try
        {
            var result = await service.GetSnapshotAsync(Query(), cancellationToken);
            if (result.IsFailure)
            {
                Snapshot = null;
                ErrorCode = result.Error.Code;
                Message = result.Error.Message;
                return;
            }

            Snapshot = result.Value;
        }
        catch (Exception)
        {
            Snapshot = null;
            ErrorCode = "reporting.unexpected";
            Message = "Le rapport n’a pas pu être chargé. Réessayez ou consultez le diagnostic SQL.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task UpdateSettingsAsync(
        bool lowStockEnabled,
        bool expiryEnabled,
        int? horizonDays,
        CancellationToken cancellationToken = default)
    {
        if (Snapshot is null) return;
        IsBusy = true;
        Message = null;
        ErrorCode = null;
        try
        {
            var result = await service.UpdateSettingsAsync(new UpdateAlertSettingsRequest(
                Guid.NewGuid(), lowStockEnabled, expiryEnabled, horizonDays, Snapshot.Settings.RowVersion), cancellationToken);
            if (result.IsFailure)
            {
                ErrorCode = result.Error.Code;
                Message = result.Error.Message;
                return;
            }
            Message = "Paramètres d’alerte enregistrés et audités.";
            await LoadAsync(cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ExportAsync(ReportingExportKind kind, CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        Message = null;
        ErrorCode = null;
        try
        {
            var result = await service.ExportCsvAsync(
                new ReportingExportRequest(Guid.NewGuid(), kind, Query()), cancellationToken);
            if (result.IsFailure)
            {
                ErrorCode = result.Error.Code;
                Message = result.Error.Message;
                return;
            }

            var exportDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Logiciel Pharmacie", "Exports");
            Directory.CreateDirectory(exportDirectory);
            var fullPath = Path.Combine(exportDirectory, result.Value.FileName);
            await File.WriteAllBytesAsync(fullPath, result.Value.Content, cancellationToken);
            Message = $"Export CSV créé : {fullPath}";
        }
        catch (IOException exception)
        {
            ErrorCode = "reporting.export.io";
            Message = $"L’export n’a pas pu être écrit : {exception.Message}";
        }
        catch (UnauthorizedAccessException exception)
        {
            ErrorCode = "reporting.export.access_denied";
            Message = $"L’export n’a pas pu être écrit : {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ReportingQuery Query()
    {
        var from = DateTime.SpecifyKind(FromDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(ToDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        return new ReportingQuery(new ReportingPeriod(from, to));
    }
}
