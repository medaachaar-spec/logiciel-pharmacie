using System.Reflection;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class FoundationArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(Domain.AssemblyMarker).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Application.AssemblyMarker).Assembly;

    [Fact]
    public void DomainAssembly_IsLoadable()
    {
        var loadedAssembly = Assembly.Load(DomainAssembly.GetName());

        Assert.Equal("Pharmacy.Domain", loadedAssembly.GetName().Name);
    }

    [Fact]
    public void ApplicationAssembly_IsLoadable()
    {
        var loadedAssembly = Assembly.Load(ApplicationAssembly.GetName());

        Assert.Equal("Pharmacy.Application", loadedAssembly.GetName().Name);
    }

    [Fact]
    public void Domain_DoesNotReferenceAnotherPharmacyAssembly()
    {
        Assert.Empty(GetPharmacyReferences(DomainAssembly));
    }

    [Fact]
    public void Application_ReferencesDomain()
    {
        Assert.Contains("Pharmacy.Domain", GetPharmacyReferences(ApplicationAssembly));
    }

    [Fact]
    public void Application_DoesNotReferenceInfrastructureOrDesktop()
    {
        var references = GetPharmacyReferences(ApplicationAssembly);

        Assert.DoesNotContain("Pharmacy.Infrastructure", references);
        Assert.DoesNotContain("Pharmacy.Desktop", references);
    }

    [Fact]
    public void LibraryAssemblyMarkers_Exist()
    {
        Assert.Equal(
            typeof(Domain.AssemblyMarker),
            DomainAssembly.GetType("Pharmacy.Domain.AssemblyMarker"));
        Assert.Equal(
            typeof(Application.AssemblyMarker),
            ApplicationAssembly.GetType("Pharmacy.Application.AssemblyMarker"));
    }

    private static IReadOnlySet<string> GetPharmacyReferences(Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name?.StartsWith("Pharmacy.", StringComparison.Ordinal) is true)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);
    }
}
