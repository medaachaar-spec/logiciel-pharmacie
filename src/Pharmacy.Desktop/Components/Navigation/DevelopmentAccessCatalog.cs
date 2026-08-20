namespace Pharmacy.Desktop.Components.Navigation;

public sealed record DevelopmentAccessProfile(
    string Id,
    string Label,
    IReadOnlySet<string> AccessibleNavigationIds,
    bool AllowsAll);

public static class DevelopmentAccessCatalog
{
    public const string Warning = "Profils internes sans effet sur les permissions métier";

    public static IReadOnlyList<DevelopmentAccessProfile> Profiles { get; } =
        new[]
        {
            new DevelopmentAccessProfile(
                "full",
                "Accès complet",
                new HashSet<string>(StringComparer.Ordinal),
                true),
            new DevelopmentAccessProfile(
                "operations",
                "Opérations",
                CreateSet("home", "sales", "catalog", "inventory", "purchasing", "cash", "customers", "diagnostic", "ui-lab"),
                false),
            new DevelopmentAccessProfile(
                "read",
                "Lecture",
                CreateSet("home", "catalog", "inventory", "reporting", "diagnostic", "ui-lab"),
                false)
        };

    public static DevelopmentAccessProfile Default => Profiles[0];

    public static AppNavigationAccessState ResolveState(
        AppNavigationItem item,
        DevelopmentAccessProfile profile)
    {
        if (!profile.AllowsAll && !profile.AccessibleNavigationIds.Contains(item.Id))
        {
            return AppNavigationAccessState.Locked;
        }

        return item.InitialAccessState;
    }

    private static IReadOnlySet<string> CreateSet(params string[] values)
    {
        return values.ToHashSet(StringComparer.Ordinal);
    }
}
