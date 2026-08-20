namespace Pharmacy.Application.Security.Models;

public sealed record LoginRequest(string Login, string Password, Guid CorrelationId);

public sealed record UserSessionView(
    long UserId,
    string Login,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlySet<string> Permissions,
    DateTime AuthenticatedAtUtc,
    DateTime LastReauthenticatedAtUtc);

public sealed record AuthenticationResultView(
    UserSessionView Session,
    bool MustChangePassword);

public sealed record BootstrapOwnerRequest(
    Guid OperationId,
    string Login,
    string DisplayName,
    string Password);

public sealed record RecoveryCodeBatchView(
    long UserId,
    IReadOnlyList<string> Codes,
    DateTime GeneratedAtUtc,
    DateTime ExpiresAtUtc);

public sealed record SecurityUserSummaryView(
    long UserId,
    string Login,
    string DisplayName,
    bool IsActive,
    bool MustChangePassword,
    int FailedLoginCount,
    DateTime? LockedUntilUtc,
    DateTime? LastLoginAtUtc,
    IReadOnlyList<string> Roles,
    byte[] RowVersion);

public sealed record SecurityRoleView(
    long RoleId,
    string Code,
    string Name,
    string? Description,
    bool IsSystemRole,
    bool IsActive,
    IReadOnlyList<SecurityPermissionView> Permissions,
    byte[] RowVersion);

public sealed record SecurityPermissionView(
    long PermissionId,
    string Code,
    string Name,
    string Module,
    string? Description,
    bool IsSensitive);

public sealed record CreateSecurityUserRequest(
    Guid OperationId,
    string Login,
    string DisplayName,
    string InitialPassword,
    IReadOnlyList<long> RoleIds);

public sealed record SetUserActiveRequest(
    Guid OperationId,
    long UserId,
    bool IsActive,
    byte[] RowVersion);

public sealed record SetUserRolesRequest(
    Guid OperationId,
    long UserId,
    IReadOnlyList<long> RoleIds,
    byte[] RowVersion);

public sealed record CreateSecurityRoleRequest(
    Guid OperationId,
    string Code,
    string Name,
    IReadOnlyList<long> PermissionIds);

public sealed record UpdateSecurityRoleRequest(
    Guid OperationId,
    long RoleId,
    string Name,
    IReadOnlyList<long> PermissionIds,
    byte[] RowVersion);

public sealed record AuditEventView(
    long AuditEventId,
    string EventType,
    string Module,
    string EntityType,
    string? EntityId,
    string Action,
    long? ActorUserId,
    string? ActorDisplayName,
    string Result,
    DateTime OccurredAtUtc,
    Guid? OperationId,
    Guid CorrelationId,
    string? ReasonCode);

public sealed record AuditSearchQuery(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Module = null,
    string? Action = null,
    long? ActorUserId = null,
    int Limit = 100);

public sealed record AppendAuditEventRequest(
    string EventType,
    string Module,
    string EntityType,
    string? EntityId,
    string Action,
    long? ActorUserId,
    string? ActorDisplayName,
    string Result,
    DateTime OccurredAtUtc,
    Guid? OperationId,
    Guid CorrelationId,
    string? ReasonCode = null);
