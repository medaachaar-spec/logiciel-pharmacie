using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;

namespace Pharmacy.Desktop.Components.Security;

public enum SecurityEntryMode
{
    Checking,
    BootstrapRequired,
    LoginRequired,
    Unavailable
}

public sealed class SecurityCoordinator
{
    private readonly IAuthenticationService authentication;
    private readonly ISecurityBootstrapService bootstrap;
    private readonly ISecurityAdministrationService administration;
    private readonly IAuditReader auditReader;
    private readonly ICurrentSecuritySession session;
    private int operationInProgress;

    public SecurityCoordinator(
        IAuthenticationService authentication,
        ISecurityBootstrapService bootstrap,
        ISecurityAdministrationService administration,
        IAuditReader auditReader,
        ICurrentSecuritySession session)
    {
        this.authentication = authentication;
        this.bootstrap = bootstrap;
        this.administration = administration;
        this.auditReader = auditReader;
        this.session = session;
    }

    public SecurityEntryMode EntryMode { get; private set; } = SecurityEntryMode.Checking;
    public bool IsInitialized => EntryMode != SecurityEntryMode.Checking;
    public bool RequiresBootstrap => EntryMode == SecurityEntryMode.BootstrapRequired;
    public bool IsUnavailable => EntryMode == SecurityEntryMode.Unavailable;
    public bool IsBusy { get; private set; }
    public bool MustChangePassword { get; private set; }
    public string? Message { get; private set; }
    public IReadOnlyList<string> RecoveryCodes { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<SecurityUserSummaryView> Users { get; private set; } = Array.Empty<SecurityUserSummaryView>();
    public IReadOnlyList<SecurityRoleView> Roles { get; private set; } = Array.Empty<SecurityRoleView>();
    public IReadOnlyList<SecurityPermissionView> Permissions { get; private set; } = Array.Empty<SecurityPermissionView>();
    public IReadOnlyList<AuditEventView> AuditEvents { get; private set; } = Array.Empty<AuditEventView>();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (EntryMode != SecurityEntryMode.Checking) return;
        await DetectEntryModeAsync(cancellationToken);
    }

    public async Task RetryInitializationAsync(CancellationToken cancellationToken = default)
    {
        if (!IsUnavailable) return;
        await RunAsync(async () =>
        {
            EntryMode = SecurityEntryMode.Checking;
            await DetectEntryModeAsync(cancellationToken);
        });
    }

    private async Task DetectEntryModeAsync(CancellationToken cancellationToken)
    {
        var result = await authentication.HasUsersAsync(cancellationToken);
        if (result.IsFailure)
        {
            EntryMode = SecurityEntryMode.Unavailable;
            Message = result.Error.Message;
            return;
        }

        EntryMode = result.Value
            ? SecurityEntryMode.LoginRequired
            : SecurityEntryMode.BootstrapRequired;
        Message = null;
    }

    public async Task BootstrapAsync(string login, string displayName, string password, CancellationToken cancellationToken = default)
    {
        if (!RequiresBootstrap) return;
        await RunAsync(async () =>
        {
            if (!RequiresBootstrap) return;
            var result = await bootstrap.BootstrapOwnerAsync(
                new BootstrapOwnerRequest(Guid.NewGuid(), login, displayName, password), cancellationToken);
            if (result.IsFailure)
            {
                if (result.Error.Code == "security.bootstrap.already_completed")
                {
                    await DetectEntryModeAsync(cancellationToken);
                }
                Message = result.Error.Message;
                return;
            }
            RecoveryCodes = result.Value.Codes;
            EntryMode = SecurityEntryMode.LoginRequired;
            Message = "Compte propriétaire créé. Conservez les codes hors de l’application puis connectez-vous.";
        });
    }

    public async Task LoginAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var result = await authentication.LoginAsync(new LoginRequest(login, password, Guid.NewGuid()), cancellationToken);
            Message = result.IsFailure ? result.Error.Message : null;
            MustChangePassword = result.IsSuccess && result.Value.MustChangePassword;
        });
    }

    public async Task UnlockAsync(string password, CancellationToken cancellationToken = default)
    {
        var login = session.User?.Login ?? string.Empty;
        await RunAsync(async () =>
        {
            var result = await authentication.UnlockAsync(new LoginRequest(login, password, Guid.NewGuid()), cancellationToken);
            Message = result.IsFailure ? result.Error.Message : null;
        });
    }

    public async Task LockAsync(CancellationToken cancellationToken = default)
    {
        var result = await authentication.LockAsync(Guid.NewGuid(), cancellationToken);
        Message = result.IsFailure ? result.Error.Message : null;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var result = await authentication.LogoutAsync(Guid.NewGuid(), cancellationToken);
        Message = result.IsFailure ? result.Error.Message : null;
        MustChangePassword = false;
    }

    public async Task RecoverAsync(string login, string code, string newPassword, CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var result = await authentication.RecoverPasswordAsync(login, code, newPassword, Guid.NewGuid(), cancellationToken);
            Message = result.IsFailure ? result.Error.Message : "Mot de passe renouvelé. Le code utilisé est désormais invalidé.";
            if (result.IsSuccess) MustChangePassword = false;
        });
    }

    public async Task ReauthenticateAsync(string password, CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var result = await authentication.ReauthenticateAsync(password, Guid.NewGuid(), cancellationToken);
            Message = result.IsFailure ? result.Error.Message : "Réauthentification confirmée pour cinq minutes.";
        });
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var result = await authentication.ChangePasswordAsync(currentPassword, newPassword, Guid.NewGuid(), cancellationToken);
            Message = result.IsFailure ? result.Error.Message : "Mot de passe changé. Aucune valeur sensible n’a été conservée dans l’interface.";
            if (result.IsSuccess) MustChangePassword = false;
        });
    }

    public async Task LoadAdministrationAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var users = await administration.GetUsersAsync(cancellationToken);
            var roles = await administration.GetRolesAsync(cancellationToken);
            var permissions = await administration.GetPermissionsAsync(cancellationToken);
            if (users.IsFailure) { Message = users.Error.Message; return; }
            if (roles.IsFailure) { Message = roles.Error.Message; return; }
            if (permissions.IsFailure) { Message = permissions.Error.Message; return; }
            Users = users.Value;
            Roles = roles.Value;
            Permissions = permissions.Value;
        });
    }

    public async Task LoadAuditAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var result = await auditReader.SearchAsync(new AuditSearchQuery(Limit: 200), cancellationToken);
            if (result.IsFailure) { Message = result.Error.Message; return; }
            AuditEvents = result.Value;
        });
    }

    public async Task CreateUserAsync(
        string login,
        string displayName,
        string initialPassword,
        IReadOnlyList<long> roleIds,
        CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var result = await administration.CreateUserAsync(
                new CreateSecurityUserRequest(Guid.NewGuid(), login, displayName, initialPassword, roleIds), cancellationToken);
            if (result.IsFailure) { Message = result.Error.Message; return; }
            Message = "Compte créé. Le changement du mot de passe initial est requis.";
            await RefreshAdministrationCoreAsync(cancellationToken);
        });
    }

    public async Task SetUserActiveAsync(SecurityUserSummaryView user, bool isActive, CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var result = await administration.SetUserActiveAsync(
                new SetUserActiveRequest(Guid.NewGuid(), user.UserId, isActive, user.RowVersion), cancellationToken);
            if (result.IsFailure) { Message = result.Error.Message; return; }
            Message = isActive ? "Compte réactivé." : "Compte désactivé sans suppression de son historique.";
            await RefreshAdministrationCoreAsync(cancellationToken);
        });
    }

    public async Task GenerateRecoveryCodesAsync(SecurityUserSummaryView user, CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var result = await administration.GenerateRecoveryCodesAsync(user.UserId, Guid.NewGuid(), cancellationToken);
            if (result.IsFailure) { Message = result.Error.Message; return; }
            RecoveryCodes = result.Value.Codes;
            Message = "Nouveaux codes générés. Les précédents sont invalidés.";
        });
    }

    public async Task SetUserRoleAsync(SecurityUserSummaryView user, long roleId, CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var result = await administration.SetUserRolesAsync(
                new SetUserRolesRequest(Guid.NewGuid(), user.UserId, new[] { roleId }, user.RowVersion), cancellationToken);
            if (result.IsFailure) { Message = result.Error.Message; return; }
            Message = "Rôle affecté. La prochaine autorisation utilisera la nouvelle matrice.";
            await RefreshAdministrationCoreAsync(cancellationToken);
        });
    }

    public async Task<bool> CreateRoleAsync(
        string code,
        string name,
        IReadOnlyList<long> permissionIds,
        CancellationToken cancellationToken = default)
    {
        var succeeded = false;
        await RunAsync(async () =>
        {
            var result = await administration.CreateRoleAsync(
                new CreateSecurityRoleRequest(Guid.NewGuid(), code, name, permissionIds), cancellationToken);
            if (result.IsFailure) { Message = result.Error.Message; return; }
            Message = $"Rôle {result.Value.Code} créé avec {result.Value.Permissions.Count} permission(s).";
            await RefreshAdministrationCoreAsync(cancellationToken);
            succeeded = true;
        });
        return succeeded;
    }

    public async Task<bool> UpdateRoleAsync(
        SecurityRoleView role,
        string name,
        IReadOnlyList<long> permissionIds,
        CancellationToken cancellationToken = default)
    {
        var succeeded = false;
        await RunAsync(async () =>
        {
            var result = await administration.UpdateRoleAsync(
                new UpdateSecurityRoleRequest(Guid.NewGuid(), role.RoleId, name, permissionIds, role.RowVersion), cancellationToken);
            if (result.IsFailure) { Message = result.Error.Message; return; }
            Message = $"Rôle {result.Value.Code} mis à jour avec {result.Value.Permissions.Count} permission(s).";
            await RefreshAdministrationCoreAsync(cancellationToken);
            succeeded = true;
        });
        return succeeded;
    }

    private async Task RefreshAdministrationCoreAsync(CancellationToken cancellationToken)
    {
        var users = await administration.GetUsersAsync(cancellationToken);
        var roles = await administration.GetRolesAsync(cancellationToken);
        var permissions = await administration.GetPermissionsAsync(cancellationToken);
        if (users.IsSuccess) Users = users.Value;
        if (roles.IsSuccess) Roles = roles.Value;
        if (permissions.IsSuccess) Permissions = permissions.Value;
    }

    private async Task RunAsync(Func<Task> operation)
    {
        if (Interlocked.CompareExchange(ref operationInProgress, 1, 0) != 0) return;
        IsBusy = true;
        Message = null;
        try { await operation(); }
        finally
        {
            IsBusy = false;
            Interlocked.Exchange(ref operationInProgress, 0);
        }
    }
}
