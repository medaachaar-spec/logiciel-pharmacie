namespace Pharmacy.Application;

/// <summary>
/// Identifies the application assembly and its domain dependency.
/// </summary>
public sealed class AssemblyMarker
{
    private AssemblyMarker()
    {
    }

    public static Type DomainDependency => typeof(Pharmacy.Domain.AssemblyMarker);
}
