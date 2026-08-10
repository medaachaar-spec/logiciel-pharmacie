namespace Pharmacy.Infrastructure.Persistence.Entities.Audit;

internal sealed class AuditEventEntity
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public long? ActorUserId { get; set; }
    public string? ActorDisplayName { get; set; }
    public string WorkstationId { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public Guid? OperationId { get; set; }
    public Guid CorrelationId { get; set; }
    public string? ReasonCode { get; set; }
    public ICollection<AuditEventDetailEntity> Details { get; } = new List<AuditEventDetailEntity>();
}

internal sealed class AuditEventDetailEntity
{
    public long Id { get; set; }
    public long AuditEventId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OldValueRedacted { get; set; }
    public string? NewValueRedacted { get; set; }
    public AuditEventEntity AuditEvent { get; set; } = null!;
}
