using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Domain.Security;

namespace Pharmacy.Application.Security.Services;

public sealed class CurrentSecuritySession : ICurrentSecuritySession
{
    private readonly object gate = new();
    private UserSessionView? user;
    private bool isLocked;
    private DateTime? lastActivityAtUtc;

    public event Action? Changed;

    public bool IsAuthenticated
    {
        get { lock (gate) return user is not null; }
    }

    public bool IsLocked
    {
        get { lock (gate) return isLocked; }
    }

    public UserSessionView? User
    {
        get { lock (gate) return user; }
    }

    public DateTime? LastActivityAtUtc
    {
        get { lock (gate) return lastActivityAtUtc; }
    }

    public void Establish(UserSessionView authenticatedUser, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(authenticatedUser);
        lock (gate)
        {
            user = authenticatedUser;
            isLocked = false;
            lastActivityAtUtc = nowUtc;
        }
        Changed?.Invoke();
    }

    public void Touch(DateTime nowUtc)
    {
        lock (gate)
        {
            if (user is null || isLocked) return;
            lastActivityAtUtc = nowUtc;
        }
    }

    public bool LockIfInactive(DateTime nowUtc)
    {
        lock (gate)
        {
            if (user is null || isLocked || lastActivityAtUtc is null
                || nowUtc - lastActivityAtUtc.Value < LocalSecurityRules.InactivityLockDuration)
            {
                return false;
            }
            isLocked = true;
        }
        Changed?.Invoke();
        return true;
    }

    public void Lock(DateTime nowUtc)
    {
        lock (gate)
        {
            if (user is null) return;
            isLocked = true;
            lastActivityAtUtc = nowUtc;
        }
        Changed?.Invoke();
    }

    public void Unlock(DateTime nowUtc)
    {
        lock (gate)
        {
            if (user is null) return;
            isLocked = false;
            lastActivityAtUtc = nowUtc;
        }
        Changed?.Invoke();
    }

    public void MarkReauthenticated(DateTime nowUtc)
    {
        lock (gate)
        {
            if (user is null) return;
            user = user with { LastReauthenticatedAtUtc = nowUtc };
            lastActivityAtUtc = nowUtc;
        }
        Changed?.Invoke();
    }

    public void Clear()
    {
        lock (gate)
        {
            user = null;
            isLocked = false;
            lastActivityAtUtc = null;
        }
        Changed?.Invoke();
    }
}
