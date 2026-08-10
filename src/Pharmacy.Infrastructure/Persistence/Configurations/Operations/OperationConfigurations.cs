using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Operations;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Operations;

internal sealed class BackupRecordConfiguration : IEntityTypeConfiguration<BackupRecordEntity>
{
    public void Configure(EntityTypeBuilder<BackupRecordEntity> b)
    {
        b.ToTable("BackupRecords", OperationsSchema.Name, t =>
        {
            t.HasCheckConstraint("CK_operations_BackupRecords_Type", "[BackupType] IN (N'Full',N'Differential',N'TransactionLog')");
            t.HasCheckConstraint("CK_operations_BackupRecords_Status", "[Status] IN (N'Running',N'Completed',N'Failed')");
            t.HasCheckConstraint("CK_operations_BackupRecords_FileName", "LEN(LTRIM(RTRIM([FileName]))) > 0 AND [FileName] NOT LIKE N'%\\%' AND [FileName] NOT LIKE N'%/%'");
        });
        b.HasKey(x => x.Id).HasName("PK_operations_BackupRecords");
        b.Property(x => x.Id).UseIdentityColumn();
        b.Property(x => x.OperationId).IsRequired();
        b.Property(x => x.BackupType).HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        b.Property(x => x.Status).HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        b.Property(x => x.FileName).HasColumnType("nvarchar(260)").HasMaxLength(260).IsRequired();
        b.Property(x => x.FileHash).HasColumnType("nvarchar(64)").HasMaxLength(64);
        b.Property(x => x.VerificationStatus).HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        b.Property(x => x.ExternalCopyStatus).HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        b.Property(x => x.ErrorCode).HasColumnType("nvarchar(128)").HasMaxLength(128);
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        b.Property(x => x.CompletedAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.HasIndex(x => x.OperationId).IsUnique().HasDatabaseName("UX_operations_BackupRecords_OperationId");
        b.HasIndex(x => new { x.CreatedAtUtc, x.Id }).HasDatabaseName("IX_operations_BackupRecords_Timeline");
    }
}

internal sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJobEntity>
{
    public void Configure(EntityTypeBuilder<ImportJobEntity> b)
    {
        b.ToTable("ImportJobs", OperationsSchema.Name, t =>
        {
            t.HasCheckConstraint("CK_operations_ImportJobs_Type", "[ImportType] IN (N'Catalogue',N'OpeningStock',N'Lots')");
            t.HasCheckConstraint("CK_operations_ImportJobs_Status", "[Status] IN (N'Completed',N'Failed')");
            t.HasCheckConstraint("CK_operations_ImportJobs_Counts", "[ReadCount]>=0 AND [CreatedCount]>=0 AND [UpdatedCount]>=0 AND [IgnoredCount]>=0 AND [RejectedCount]>=0");
            t.HasCheckConstraint("CK_operations_ImportJobs_FileName", "LEN(LTRIM(RTRIM([SourceFileName]))) > 0 AND [SourceFileName] NOT LIKE N'%\\%' AND [SourceFileName] NOT LIKE N'%/%'");
        });
        b.HasKey(x => x.Id).HasName("PK_operations_ImportJobs");
        b.Property(x => x.Id).UseIdentityColumn();
        b.Property(x => x.OperationId).IsRequired();
        b.Property(x => x.Fingerprint).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired();
        b.Property(x => x.ImportType).HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        b.Property(x => x.Status).HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        b.Property(x => x.SourceFileName).HasColumnType("nvarchar(260)").HasMaxLength(260).IsRequired();
        b.Property(x => x.SourceFileHash).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        b.Property(x => x.StartedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        b.Property(x => x.CompletedAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.HasIndex(x => x.OperationId).IsUnique().HasDatabaseName("UX_operations_ImportJobs_OperationId");
        b.HasIndex(x => x.Fingerprint).IsUnique().HasDatabaseName("UX_operations_ImportJobs_Fingerprint");
        b.HasIndex(x => new { x.StartedAtUtc, x.Id }).HasDatabaseName("IX_operations_ImportJobs_Timeline");
    }
}
