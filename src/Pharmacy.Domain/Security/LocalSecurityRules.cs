namespace Pharmacy.Domain.Security;

public static class LocalSecurityRules
{
    public const int MinimumPasswordLength = 15;
    public const int MaximumPasswordLength = 128;
    public const int MaximumFailedLoginCount = 5;

    public static readonly TimeSpan AccountLockoutDuration = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan InactivityLockDuration = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan RecentReauthenticationDuration = TimeSpan.FromMinutes(5);

    public static string NormalizeLogin(string login)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        return login.Trim().ToUpperInvariant();
    }

    public static bool IsPasswordLengthValid(string? password)
    {
        return password is not null
            && password.Length >= MinimumPasswordLength
            && password.Length <= MaximumPasswordLength;
    }
}
