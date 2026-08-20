using Pharmacy.Application.Operations.Contracts;
using Pharmacy.Application.Operations.Models;

namespace Pharmacy.Desktop.Components.Operations;

public sealed class OperationsCoordinator
{
    private readonly IBackupRecoveryService backups;
    private readonly IControlledImportService imports;
    private readonly IDeviceDiagnostics diagnostics;
    private Guid pendingImportOperationId;
    private int operationInProgress;

    public OperationsCoordinator(
        IBackupRecoveryService backups,
        IControlledImportService imports,
        IDeviceDiagnostics diagnostics)
    {
        this.backups = backups;
        this.imports = imports;
        this.diagnostics = diagnostics;
    }

    public BackupConfigurationView? Configuration { get; private set; }
    public IReadOnlyList<BackupFileView> BackupFiles { get; private set; } = Array.Empty<BackupFileView>();
    public ImportPreviewView? ImportPreview { get; private set; }
    public ImportReportView? ImportReport { get; private set; }
    public DeviceDiagnosticView? DeviceDiagnostic { get; private set; }
    public bool IsBusy => Volatile.Read(ref operationInProgress) != 0;
    public string? Message { get; private set; }
    public string? Error { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var configuration = await backups.GetConfigurationAsync(cancellationToken);
            if (configuration.IsFailure) { Fail(configuration.Error.Code, configuration.Error.Message); return; }
            Configuration = configuration.Value;
            var files = await backups.ListAsync(cancellationToken);
            if (files.IsFailure) { Fail(files.Error.Code, files.Error.Message); return; }
            BackupFiles = files.Value;
        });
    }

    public async Task CreateBackupAsync(SqlBackupType type, bool externalCopy, CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var result = await backups.CreateAsync(new CreateBackupRequest(Guid.NewGuid(), type, externalCopy), cancellationToken);
            if (result.IsFailure) { Fail(result.Error.Code, result.Error.Message); return; }
            Message = $"Sauvegarde {result.Value.Backup.FileName} créée, vérifiée et hachée.";
            var files = await backups.ListAsync(cancellationToken);
            if (files.IsSuccess) BackupFiles = files.Value;
        });
    }

    public async Task VerifyBackupAsync(string path, CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var result = await backups.VerifyAsync(new VerifyBackupRequest(Guid.NewGuid(), path), cancellationToken);
            if (result.IsFailure) { Fail(result.Error.Code, result.Error.Message); return; }
            Message = $"{result.Value.FileName} : VERIFYONLY et SHA-256 réussis.";
        });
    }

    public async Task RestoreTestAsync(string path, CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var result = await backups.RestoreTestAsync(new RestoreTestRequest(Guid.NewGuid(), path), cancellationToken);
            if (result.IsFailure) { Fail(result.Error.Code, result.Error.Message); return; }
            Message = $"Restauration isolée validée ({result.Value.AppliedMigrationCount} migrations, {result.Value.CriticalTableCount} tables critiques) et base temporaire supprimée.";
        });
    }

    public async Task PreviewImportAsync(string fileName, byte[] content, ControlledImportType type, CsvImportMapping mapping, CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            ImportReport = null;
            pendingImportOperationId = Guid.NewGuid();
            var result = await imports.PreviewAsync(new PreviewImportRequest(fileName, content, type, mapping), cancellationToken);
            if (result.IsFailure) { ImportPreview = null; Fail(result.Error.Code, result.Error.Message); return; }
            ImportPreview = result.Value;
            Message = $"Prévisualisation terminée : {result.Value.TotalRows} ligne(s), {result.Value.Issues.Count} alerte(s).";
        });
    }

    public async Task ConfirmImportAsync(string fileName, byte[] content, ControlledImportType type, CsvImportMapping mapping, CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            pendingImportOperationId = pendingImportOperationId == Guid.Empty ? Guid.NewGuid() : pendingImportOperationId;
            var result = await imports.ConfirmAsync(new ConfirmImportRequest(pendingImportOperationId, fileName, content, type, mapping), cancellationToken);
            if (result.IsFailure) { Fail(result.Error.Code, result.Error.Message); return; }
            ImportReport = result.Value;
            Message = $"Import atomique terminé : {result.Value.CreatedCount} créé(s), {result.Value.IgnoredCount} ignoré(s), {result.Value.RejectedCount} rejeté(s).";
        });
    }

    public async Task CheckDevicesAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var result = await diagnostics.CheckAsync(cancellationToken);
            if (result.IsFailure) { Fail(result.Error.Code, result.Error.Message); return; }
            DeviceDiagnostic = result.Value;
            Message = "Diagnostic local actualisé ; les simulateurs ne valent pas certification matérielle.";
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (Interlocked.CompareExchange(ref operationInProgress, 1, 0) != 0) return;
        Message = null; Error = null;
        try { await action(); }
        catch (Exception) { Fail("operations.unexpected", "L’opération n’a pas abouti. Consultez le diagnostic et le journal d’audit."); }
        finally { Interlocked.Exchange(ref operationInProgress, 0); }
    }

    private void Fail(string code, string message) => Error = $"{code} — {message}";
}
