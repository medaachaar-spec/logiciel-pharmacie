namespace Pharmacy.Domain.Security;

public static class LocalSecurityRules
{
    public const int MinimumPasswordLength = 15;
    public const int MaximumPasswordLength = 128;
    public const int MaximumFailedLoginCount = 5;
    public const int MaximumLoginLength = 128;
    public const int MaximumDisplayNameLength = 256;
    public const int MaximumRoleCodeLength = 128;
    public const int MaximumRoleNameLength = 256;

    public static readonly TimeSpan AccountLockoutDuration = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan InactivityLockDuration = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan RecentReauthenticationDuration = TimeSpan.FromMinutes(5);

    public static string NormalizeLogin(string login)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        return login.Trim().ToUpperInvariant();
    }

    public static string NormalizeRoleCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return code.Trim().ToUpperInvariant();
    }

    public static bool IsLoginValid(string? login) =>
        !string.IsNullOrWhiteSpace(login) && login.Trim().Length <= MaximumLoginLength;

    public static bool IsDisplayNameValid(string? displayName) =>
        !string.IsNullOrWhiteSpace(displayName) && displayName.Trim().Length <= MaximumDisplayNameLength;

    public static bool IsRoleCodeValid(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var normalized = code.Trim().ToUpperInvariant();
        return normalized.Length <= MaximumRoleCodeLength
            && char.IsAsciiLetterUpper(normalized[0])
            && normalized.All(character => char.IsAsciiLetterUpper(character)
                || char.IsAsciiDigit(character)
                || character is '-' or '_' or '.');
    }

    public static bool IsRoleNameValid(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= MaximumRoleNameLength;

    public static bool IsPasswordLengthValid(string? password)
    {
        return password is not null
            && password.Length >= MinimumPasswordLength
            && password.Length <= MaximumPasswordLength;
    }
}
