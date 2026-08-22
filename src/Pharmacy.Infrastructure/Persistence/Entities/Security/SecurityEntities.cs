namespace Pharmacy.Infrastructure.Persistence.Entities.Security;

internal sealed class UserAccountEntity
{
    public long Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string NormalizedLogin { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? DisabledAtUtc { get; set; }
    public long? DisabledByUserId { get; set; }
    public Guid SecurityStamp { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<UserRoleEntity> UserRoles { get; } = new List<UserRoleEntity>();
    public ICollection<UserPermissionOverrideEntity> PermissionOverrides { get; } = new List<UserPermissionOverrideEntity>();
    public ICollection<RecoveryCodeEntity> RecoveryCodes { get; } = new List<RecoveryCodeEntity>();
}

internal sealed class UserSessionEntity
{
    public long Id { get; set; }
    public long UserAccountId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? EndReason { get; set; }
    public UserAccountEntity UserAccount { get; set; } = null!;
}

internal sealed class RoleEntity
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public bool IsActive { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<UserRoleEntity> UserRoles { get; } = new List<UserRoleEntity>();
    public ICollection<RolePermissionEntity> RolePermissions { get; } = new List<RolePermissionEntity>();
}

internal sealed class PermissionEntity
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSensitive { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<RolePermissionEntity> RolePermissions { get; } = new List<RolePermissionEntity>();
    public ICollection<UserPermissionOverrideEntity> UserOverrides { get; } = new List<UserPermissionOverrideEntity>();
}

internal sealed class UserRoleEntity
{
    public long Id { get; set; }
    public long UserAccountId { get; set; }
    public long RoleId { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public long? AssignedByUserId { get; set; }
    public UserAccountEntity UserAccount { get; set; } = null!;
    public RoleEntity Role { get; set; } = null!;
}

internal sealed class RolePermissionEntity
{
    public long Id { get; set; }
    public long RoleId { get; set; }
    public long PermissionId { get; set; }
    public RoleEntity Role { get; set; } = null!;
    public PermissionEntity Permission { get; set; } = null!;
}

internal sealed class UserPermissionOverrideEntity
{
    public long Id { get; set; }
    public long UserAccountId { get; set; }
    public long PermissionId { get; set; }
    public string Effect { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
    public long UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public UserAccountEntity UserAccount { get; set; } = null!;
    public PermissionEntity Permission { get; set; } = null!;
}

internal sealed class RecoveryCodeEntity
{
    public long Id { get; set; }
    public long UserAccountId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public UserAccountEntity UserAccount { get; set; } = null!;
}

internal sealed class AuthenticationEventEntity
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public long? UserAccountId { get; set; }
    public string? UsernameAttemptHash { get; set; }
    public string WorkstationId { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public bool WasSuccessful { get; set; }
    public string? FailureReasonCode { get; set; }
    public Guid CorrelationId { get; set; }
}
