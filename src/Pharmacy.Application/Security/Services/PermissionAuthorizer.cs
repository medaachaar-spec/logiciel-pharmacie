using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Security;

namespace Pharmacy.Application.Security.Services;

public sealed class PermissionAuthorizer : IPermissionAuthorizer
{
    private readonly ICurrentSecuritySession session;
    private readonly ISecurityClock clock;
    private readonly ISecuritySessionValidator? sessionValidator;

    public PermissionAuthorizer(ICurrentSecuritySession session, ISecurityClock clock)
        : this(session, clock, null)
    {
    }

    public PermissionAuthorizer(
        ICurrentSecuritySession session,
        ISecurityClock clock,
        ISecuritySessionValidator? sessionValidator)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.sessionValidator = sessionValidator;
    }

    public Result Authorize(string permission, bool requireRecentReauthentication = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        var user = session.User;
        if (user is null || session.IsLocked)
        {
            return Result.Failure(SecurityErrors.AuthenticationRequired);
        }

        var activeValidation = sessionValidator?.ValidateActive(user.UserId);
        if (activeValidation is { IsFailure: true })
        {
            return activeValidation;
        }

        if (!user.Permissions.Contains(permission))
        {
            return Result.Failure(SecurityErrors.PermissionDenied);
        }

        if (requireRecentReauthentication
            && clock.UtcNow - user.LastReauthenticatedAtUtc > LocalSecurityRules.RecentReauthenticationDuration)
        {
            return Result.Failure(SecurityErrors.ReauthenticationRequired);
        }

        return Result.Success();
    }
}
