using System.Reflection;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class FoundationIntegrationTests
{
    private static readonly Assembly DomainAssembly = typeof(Domain.AssemblyMarker).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Application.AssemblyMarker).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Infrastructure.AssemblyMarker).Assembly;

    private static readonly IReadOnlyDictionary<string, Assembly> FoundationAssemblies =
        new[] { DomainAssembly, ApplicationAssembly, InfrastructureAssembly }
            .ToDictionary(
                assembly => assembly.GetName().Name!,
                StringComparer.Ordinal);

    [Fact]
    public void InfrastructureAssembly_IsLoadable()
    {
        var loadedAssembly = Assembly.Load(InfrastructureAssembly.GetName());

        Assert.Equal("Pharmacy.Infrastructure", loadedAssembly.GetName().Name);
    }

    [Fact]
    public void Infrastructure_ReferencesApplication()
    {
        Assert.Contains("Pharmacy.Application", GetPharmacyReferences(InfrastructureAssembly));
    }

    [Fact]
    public void Infrastructure_ReferencesDomain()
    {
        Assert.Contains("Pharmacy.Domain", GetPharmacyReferences(InfrastructureAssembly));
    }

    [Fact]
    public void Infrastructure_DoesNotReferenceDesktop()
    {
        Assert.DoesNotContain("Pharmacy.Desktop", GetPharmacyReferences(InfrastructureAssembly));
    }

    [Fact]
    public void FoundationDependencyGraph_IsAcyclic()
    {
        var graph = FoundationAssemblies.ToDictionary(
            pair => pair.Key,
            pair => GetPharmacyReferences(pair.Value)
                .Where(FoundationAssemblies.ContainsKey)
                .ToArray(),
            StringComparer.Ordinal);
        var state = new Dictionary<string, VisitState>(StringComparer.Ordinal);

        foreach (var assemblyName in graph.Keys)
        {
            Assert.False(
                HasCycle(assemblyName, graph, state),
                $"A dependency cycle starts at {assemblyName}.");
        }
    }

    [Fact]
    public void FoundationAssemblies_DoNotContainProhibitedBusinessEntityTypes()
    {
        var prohibitedTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Product",
            "Produit",
            "Stock",
            "Batch",
            "Lot",
            "Sale",
            "Vente",
            "Payment",
            "Paiement",
            "CashRegister",
            "Caisse",
            "Supplier",
            "Fournisseur",
            "Customer",
            "Client",
            "Credit",
            "User",
            "Utilisateur",
            "Role"
        };

        var prohibitedTypes = FoundationAssemblies.Values
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => prohibitedTypeNames.Contains(type.Name))
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        Assert.Empty(prohibitedTypes);
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

    private static bool HasCycle(
        string node,
        IReadOnlyDictionary<string, string[]> graph,
        IDictionary<string, VisitState> state)
    {
        if (state.TryGetValue(node, out var currentState))
        {
            return currentState == VisitState.Visiting;
        }

        state[node] = VisitState.Visiting;

        foreach (var dependency in graph[node])
        {
            if (HasCycle(dependency, graph, state))
            {
                return true;
            }
        }

        state[node] = VisitState.Visited;
        return false;
    }

    private enum VisitState
    {
        Visiting,
        Visited
    }
}
