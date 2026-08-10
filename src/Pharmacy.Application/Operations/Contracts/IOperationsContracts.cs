using Pharmacy.Application.Operations.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Operations.Contracts;

public interface IBackupRecoveryService
{
    Task<Result<BackupConfigurationView>> GetConfigurationAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BackupFileView>>> ListAsync(CancellationToken cancellationToken = default);
    Task<Result<BackupExecutionView>> CreateAsync(CreateBackupRequest request, CancellationToken cancellationToken = default);
    Task<Result<BackupFileView>> VerifyAsync(VerifyBackupRequest request, CancellationToken cancellationToken = default);
    Task<Result<RestoreTestView>> RestoreTestAsync(RestoreTestRequest request, CancellationToken cancellationToken = default);
}

public interface IControlledImportService
{
    Task<Result<ImportPreviewView>> PreviewAsync(PreviewImportRequest request, CancellationToken cancellationToken = default);
    Task<Result<ImportReportView>> ConfirmAsync(ConfirmImportRequest request, CancellationToken cancellationToken = default);
}

public interface IReceiptPrinter
{
    Task<Result<ReceiptPrintResult>> PrintAsync(ReceiptPrintRequest request, CancellationToken cancellationToken = default);
}

public interface IManualPaymentTerminal
{
    Task<Result<ManualTerminalResult>> ConfirmAsync(ManualTerminalOutcome outcome, CancellationToken cancellationToken = default);
}

public interface IDeviceDiagnostics
{
    Task<Result<DeviceDiagnosticView>> CheckAsync(CancellationToken cancellationToken = default);
}
