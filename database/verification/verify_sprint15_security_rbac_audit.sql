/* Sprint 15 read-only verification. Run only on a migrated development database. */
SET NOCOUNT ON;

SELECT s.name AS SchemaName, t.name AS TableName
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE s.name IN (N'security', N'audit')
ORDER BY s.name, t.name;

SELECT Code, Name, Module, IsSensitive
FROM security.Permissions
ORDER BY Code;

SELECT r.Code AS RoleCode, p.Code AS PermissionCode
FROM security.Roles AS r
JOIN security.RolePermissions AS rp ON rp.RoleId = r.Id
JOIN security.Permissions AS p ON p.Id = rp.PermissionId
ORDER BY r.Code, p.Code;

SELECT TOP (100)
    EventType, Module, EntityType, EntityId, Action,
    ActorDisplayName, Result, OccurredAtUtc, OperationId, CorrelationId, ReasonCode
FROM audit.AuditEvents
ORDER BY OccurredAtUtc DESC, Id DESC;

SELECT TOP (100)
    EventType, WasSuccessful, FailureReasonCode, OccurredAtUtc, CorrelationId
FROM security.AuthenticationEvents
ORDER BY OccurredAtUtc DESC, Id DESC;

SELECT
    (SELECT COUNT(*) FROM security.UserAccounts) AS UserCount,
    (SELECT COUNT(*) FROM security.RecoveryCodes WHERE UsedAtUtc IS NULL AND ExpiresAtUtc > SYSUTCDATETIME()) AS ActiveRecoveryCodeCount,
    (SELECT COUNT(*) FROM audit.AuditEvents) AS AuditEventCount;
