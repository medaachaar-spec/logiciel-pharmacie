using Pharmacy.Application.Security.Models;

namespace Pharmacy.Application.Security.Services;

public static class EffectivePermissionResolver
{
    public static IReadOnlySet<string> Resolve(
        IEnumerable<string> roleGrants,
        IEnumerable<KeyValuePair<string, SecurityPermissionOverrideEffect>> overrides)
    {
        ArgumentNullException.ThrowIfNull(roleGrants);
        ArgumentNullException.ThrowIfNull(overrides);

        var effective = roleGrants
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.Ordinal);
        var denied = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in overrides)
        {
            if (string.IsNullOrWhiteSpace(item.Key)) continue;
            if (item.Value == SecurityPermissionOverrideEffect.Deny)
                denied.Add(item.Key);
            else
                effective.Add(item.Key);
        }

        effective.ExceptWith(denied);
        return effective;
    }
}
