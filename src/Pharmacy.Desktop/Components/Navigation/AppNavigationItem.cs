using Pharmacy.Desktop.Components.Ui;

namespace Pharmacy.Desktop.Components.Navigation;

public enum AppNavigationGroup
{
    Activity,
    Management,
    Steering,
    Configuration,
    Development
}

public enum AppNavigationAccessState
{
    Available,
    Soon,
    Locked
}

public enum AppNavigationKind
{
    Business,
    Technical
}

public sealed record AppNavigationItem(
    string Id,
    string Label,
    string Route,
    string Description,
    AppNavigationGroup Group,
    UiIconName Icon,
    int Order,
    AppNavigationAccessState InitialAccessState,
    bool DevelopmentOnly,
    AppNavigationKind Kind,
    string? ModuleId = null,
    IReadOnlyList<string>? FutureCapabilities = null,
    string? RequiredPermission = null);
