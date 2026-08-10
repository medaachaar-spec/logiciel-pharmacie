using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Audit;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Audit;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEventEntity>
{
    public void Configure(EntityTypeBuilder<AuditEventEntity> builder)
    {
        builder.ToTable("AuditEvents", AuditSchema.Name, table =>
        {
            table.HasCheckConstraint("CK_audit_AuditEvents_Result", "[Result] IN (N'Success', N'Failure', N'Denied')");
            table.HasCheckConstraint("CK_audit_AuditEvents_Action_NotBlank", "LEN(LTRIM(RTRIM([Action]))) > 0");
            table.HasCheckConstraint("CK_audit_AuditEvents_Module_NotBlank", "LEN(LTRIM(RTRIM([Module]))) > 0");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_audit_AuditEvents");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.EventType).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Module).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.EntityType).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.EntityId).HasColumnType("nvarchar(128)").HasMaxLength(128);
        builder.Property(entity => entity.Action).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.ActorDisplayName).HasColumnType("nvarchar(256)").HasMaxLength(256);
        builder.Property(entity => entity.WorkstationId).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Result).HasColumnType("nvarchar(32)").HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.OccurredAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.ReasonCode).HasColumnType("nvarchar(128)").HasMaxLength(128);
        builder.HasIndex(entity => new { entity.OccurredAtUtc, entity.Id }).HasDatabaseName("IX_audit_AuditEvents_Timeline");
        builder.HasIndex(entity => new { entity.EntityType, entity.EntityId }).HasDatabaseName("IX_audit_AuditEvents_Entity");
        builder.HasIndex(entity => entity.ActorUserId).HasDatabaseName("IX_audit_AuditEvents_ActorUserId");
        builder.HasIndex(entity => entity.OperationId).HasDatabaseName("IX_audit_AuditEvents_OperationId");
        builder.HasIndex(entity => entity.CorrelationId).HasDatabaseName("IX_audit_AuditEvents_CorrelationId");
    }
}

internal sealed class AuditEventDetailConfiguration : IEntityTypeConfiguration<AuditEventDetailEntity>
{
    public void Configure(EntityTypeBuilder<AuditEventDetailEntity> builder)
    {
        builder.ToTable("AuditEventDetails", AuditSchema.Name);
        builder.HasKey(entity => entity.Id).HasName("PK_audit_AuditEventDetails");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.FieldName).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.OldValueRedacted).HasColumnType("nvarchar(512)").HasMaxLength(512);
        builder.Property(entity => entity.NewValueRedacted).HasColumnType("nvarchar(512)").HasMaxLength(512);
        builder.HasOne(entity => entity.AuditEvent).WithMany(audit => audit.Details)
            .HasForeignKey(entity => entity.AuditEventId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_audit_AuditEventDetails_AuditEvents_AuditEventId");
        builder.HasIndex(entity => entity.AuditEventId).HasDatabaseName("IX_audit_AuditEventDetails_AuditEventId");
    }
}
