namespace Pharmacy.Application.Operations.Models;

public enum SqlBackupType { Full, Differential, TransactionLog }

public sealed record BackupConfigurationView(
    string DatabaseName,
    string SqlEdition,
    string RecoveryModel,
    string LocalDirectory,
    string? ExternalDirectory,
    bool ExternalCopyConfigured,
    bool EncryptionConfigured,
    string AutomationMechanism,
    string CapabilityNote);

public sealed record BackupFileView(
    string FileName,
    string FullPath,
    SqlBackupType Type,
    long SizeBytes,
    DateTime LastWriteAtUtc,
    string? Sha256,
    string VerificationStatus,
    string ExternalCopyStatus);

public sealed record CreateBackupRequest(Guid OperationId, SqlBackupType Type, bool CopyToExternal);
public sealed record VerifyBackupRequest(Guid OperationId, string FullPath);
public sealed record RestoreTestRequest(Guid OperationId, string FullPath);

public sealed record BackupExecutionView(
    Guid OperationId,
    BackupFileView Backup,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc);

public sealed record RestoreTestView(
    Guid OperationId,
    string TemporaryDatabaseName,
    int AppliedMigrationCount,
    int CriticalTableCount,
    bool HealthCheckPassed,
    bool CleanupPassed,
    DateTime CompletedAtUtc);
