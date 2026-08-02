namespace Pharmacy.Infrastructure;

/// <summary>
/// Identifies the infrastructure assembly and its inward dependencies.
/// </summary>
public sealed class AssemblyMarker
{
    private AssemblyMarker()
    {
    }

    public static Type ApplicationDependency => typeof(Pharmacy.Application.AssemblyMarker);

    public static Type DomainDependency => typeof(Pharmacy.Domain.AssemblyMarker);
}
