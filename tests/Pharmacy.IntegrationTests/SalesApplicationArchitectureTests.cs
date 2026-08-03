using System.Reflection;
using Pharmacy.Application.Sales.Drafts.Commands;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Finalization.Models;
using Pharmacy.Infrastructure.Sales.InMemory;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class SalesApplicationArchitectureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void SalesSourcesContainNoForbiddenTechnicalDependency()
    {
        var sources = SalesSources();
        var prohibitedTokens = new[]
        {
            "DbContext",
            "EntityFramework",
            "SqlConnection",
            "ConnectionString",
            "IQueryable",
            "HttpClient",
            "http://",
            "https://",
            "localhost",
            "Task.Run",
            ".Wait("
        };

        Assert.All(prohibitedTokens, token =>
            Assert.DoesNotContain(token, sources, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InMemoryAdaptersUseOnlyTheApprovedNamespace()
    {
        var adapters = new[]
        {
            typeof(InMemorySaleDraftStore),
            typeof(InMemoryProductCatalogReader),
            typeof(InMemoryStockAvailabilityReader),
            typeof(InMemorySaleDraftIdGenerator),
            typeof(InMemorySaleFinalizationPreparationStore)
        };

        Assert.All(adapters, adapter =>
            Assert.Equal("Pharmacy.Infrastructure.Sales.InMemory", adapter.Namespace));
    }

    [Fact]
    public void InMemoryAdaptersContainNoGlobalStaticMutableState()
    {
        var adapterTypes = typeof(InMemorySaleDraftStore).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "Pharmacy.Infrastructure.Sales.InMemory")
            .Where(type => type.IsPublic && type.Name.StartsWith("InMemory", StringComparison.Ordinal))
            .ToArray();

        Assert.DoesNotContain(
            adapterTypes.SelectMany(type => type.GetFields(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)),
            field => !field.IsLiteral && !field.IsInitOnly);
    }

    [Fact]
    public void EveryDraftMutationCommandRequiresExpectedVersion()
    {
        var mutationCommands = new[]
        {
            typeof(AddProductToSaleDraftCommand),
            typeof(ChangeSaleItemQuantityCommand),
            typeof(RemoveSaleItemCommand),
            typeof(ClearSaleDraftCommand),
            typeof(PrepareSaleFinalizationCommand)
        };

        Assert.All(mutationCommands, command =>
        {
            var property = command.GetProperty("ExpectedVersion");
            Assert.NotNull(property);
            Assert.Equal(typeof(long), property.PropertyType);
        });
    }

    [Fact]
    public void EveryAsyncApplicationHandlerAcceptsCancellationToken()
    {
        var handlerTypes = typeof(CreateSaleDraftHandler).Assembly
            .GetTypes()
            .Where(type => type.Namespace?.StartsWith("Pharmacy.Application.Sales", StringComparison.Ordinal) == true)
            .Where(type => type.Name.EndsWith("Handler", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(handlerTypes);
        Assert.All(handlerTypes, handler =>
        {
            var method = Assert.Single(handler.GetMethods(BindingFlags.Public | BindingFlags.Instance),
                method => method.Name == "HandleAsync");
            Assert.Equal(typeof(CancellationToken), method.GetParameters()[^1].ParameterType);
            Assert.True(method.ReturnType.IsGenericType);
            Assert.Equal(typeof(Task<>), method.ReturnType.GetGenericTypeDefinition());
        });
    }

    [Fact]
    public void ReadModelsExposeNoPublicMutableSetter()
    {
        var readModels = new[]
        {
            typeof(SaleDraftView),
            typeof(SaleDraftLineView),
            typeof(SaleFinalizationPreparation)
        };

        Assert.All(readModels, model =>
            Assert.DoesNotContain(
                model.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.SetMethod?.IsPublic == true && !IsInitOnly(property)));
    }

    [Fact]
    public void ReadModelCollectionsAreReadOnlyContracts()
    {
        Assert.Equal(
            typeof(IReadOnlyList<SaleDraftLineView>),
            typeof(SaleDraftView).GetProperty(nameof(SaleDraftView.Lines))?.PropertyType);
        Assert.Equal(
            typeof(IReadOnlyList<SaleDraftLineView>),
            typeof(SaleFinalizationPreparation)
                .GetProperty(nameof(SaleFinalizationPreparation.Lines))?.PropertyType);
    }

    [Fact]
    public void FinalizationPreparationDoesNotCallRealFinalizer()
    {
        var source = File.ReadAllText(PathAt(
            "src",
            "Pharmacy.Application",
            "Sales",
            "Finalization",
            "Commands",
            "PrepareSaleFinalizationHandler.cs"));

        Assert.DoesNotContain("ISaleFinalizer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FinalizeAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SalesImplementationAddsNoDataFile()
    {
        var salesDirectories = new[]
        {
            PathAt("src", "Pharmacy.Application", "Sales"),
            PathAt("src", "Pharmacy.Infrastructure", "Sales")
        };

        Assert.DoesNotContain(
            salesDirectories.SelectMany(directory =>
                Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)),
            path => path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DesktopConnectsThroughCoordinatorWithoutConcreteAdaptersInRazor()
    {
        var razorSources = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(PathAt("src", "Pharmacy.Desktop"), "*.razor", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        var coordinator = File.ReadAllText(
            PathAt("src", "Pharmacy.Desktop", "Components", "Pos", "SalesPosCoordinator.cs"));
        var page = File.ReadAllText(
            PathAt("src", "Pharmacy.Desktop", "Components", "Pages", "DemoPosPage.razor"));

        Assert.Contains("Pharmacy.Application.Sales", coordinator, StringComparison.Ordinal);
        Assert.Contains("@inject SalesPosCoordinator", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Pharmacy.Infrastructure.Sales", razorSources, StringComparison.Ordinal);
        Assert.DoesNotContain("new InMemory", razorSources, StringComparison.Ordinal);
    }

    private static string SalesSources()
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                PathAt("src", "Pharmacy.Application", "Sales"),
                PathAt("src", "Pharmacy.Infrastructure", "Sales")
            }
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Select(File.ReadAllText));
    }

    private static bool IsInitOnly(PropertyInfo property)
    {
        return property.SetMethod?.ReturnParameter
            .GetRequiredCustomModifiers()
            .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)) == true;
    }

    private static string PathAt(params string[] segments)
    {
        return Path.Combine(new[] { RepositoryRoot }.Concat(segments).ToArray());
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startPath);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("The repository root containing Pharmacy.slnx was not found.");
    }
}
