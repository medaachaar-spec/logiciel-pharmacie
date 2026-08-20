using Pharmacy.Application.Security.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Security.Contracts;

public interface ISecurityClock
{
    DateTime UtcNow { get; }
}

public interface ICurrentSecuritySession
{
    event Action? Changed;

    bool IsAuthenticated { get; }
    bool IsLocked { get; }
    UserSessionView? User { get; }
    DateTime? LastActivityAtUtc { get; }

    void Establish(UserSessionView user, DateTime nowUtc);
    void Touch(DateTime nowUtc);
    bool LockIfInactive(DateTime nowUtc);
    void Lock(DateTime nowUtc);
    void Unlock(DateTime nowUtc);
    void MarkReauthenticated(DateTime nowUtc);
    void Clear();
}

public interface IPermissionAuthorizer
{
    Result Authorize(string permission, bool requireRecentReauthentication = false);
}

public interface IRecoveryCodeGenerator
{
    IReadOnlyList<string> Generate(int count);
}

public interface IAuthenticationService
{
    Task<Result<bool>> HasUsersAsync(CancellationToken cancellationToken = default);
    Task<Result<AuthenticationResultView>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthenticationResultView>> UnlockAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result> LockAsync(Guid correlationId, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(Guid correlationId, CancellationToken cancellationToken = default);
    Task<Result> ReauthenticateAsync(string password, Guid correlationId, CancellationToken cancellationToken = default);
    Task<Result> ChangePasswordAsync(string currentPassword, string newPassword, Guid correlationId, CancellationToken cancellationToken = default);
    Task<Result> RecoverPasswordAsync(string login, string recoveryCode, string newPassword, Guid correlationId, CancellationToken cancellationToken = default);
}

public interface ISecurityBootstrapService
{
    Task<Result<RecoveryCodeBatchView>> BootstrapOwnerAsync(BootstrapOwnerRequest request, CancellationToken cancellationToken = default);
}

public interface ISecurityAdministrationService
{
    Task<Result<IReadOnlyList<SecurityUserSummaryView>>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SecurityRoleView>>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SecurityPermissionView>>> GetPermissionsAsync(CancellationToken cancellationToken = default);
    Task<Result<SecurityUserSummaryView>> CreateUserAsync(CreateSecurityUserRequest request, CancellationToken cancellationToken = default);
    Task<Result<SecurityUserSummaryView>> SetUserActiveAsync(SetUserActiveRequest request, CancellationToken cancellationToken = default);
    Task<Result<SecurityUserSummaryView>> SetUserRolesAsync(SetUserRolesRequest request, CancellationToken cancellationToken = default);
    Task<Result<SecurityUserSummaryView>> SetUserPermissionOverridesAsync(SetUserPermissionOverridesRequest request, CancellationToken cancellationToken = default);
    Task<Result<RecoveryCodeBatchView>> GenerateRecoveryCodesAsync(long userId, Guid operationId, CancellationToken cancellationToken = default);
    Task<Result<SecurityRoleView>> CreateRoleAsync(CreateSecurityRoleRequest request, CancellationToken cancellationToken = default);
    Task<Result<SecurityRoleView>> UpdateRoleAsync(UpdateSecurityRoleRequest request, CancellationToken cancellationToken = default);
}

public interface IAuditReader
{
    Task<Result<IReadOnlyList<AuditEventView>>> SearchAsync(AuditSearchQuery query, CancellationToken cancellationToken = default);
}

public interface IAuditWriter
{
    Task<Result> AppendAsync(AppendAuditEventRequest request, CancellationToken cancellationToken = default);
}

public interface IBusinessAuditRecorder
{
    Task RecordSuccessAsync(
        string module,
        string entityType,
        string? entityId,
        string action,
        Guid? operationId,
        CancellationToken cancellationToken = default);

    Task RecordFailureAsync(
        string module,
        string entityType,
        string? entityId,
        string action,
        Guid? operationId,
        string reasonCode,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
