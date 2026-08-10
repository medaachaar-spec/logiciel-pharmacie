namespace Pharmacy.Infrastructure.Persistence.Entities.Operations;

internal sealed class BackupRecordEntity
{
    public long Id { get; set; }
    public Guid OperationId { get; set; }
    public string BackupType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? FileHash { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
    public string ExternalCopyStatus { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

internal sealed class ImportJobEntity
{
    public long Id { get; set; }
    public Guid OperationId { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string ImportType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public string SourceFileHash { get; set; } = string.Empty;
    public int ReadCount { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int IgnoredCount { get; set; }
    public int RejectedCount { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
