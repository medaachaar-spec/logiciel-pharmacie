using System.Xml.Linq;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Desktop.Components.Pos;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class SalesArchitectureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Check55_DomainReferencesNoOtherPharmacyProject()
    {
        var project = Project("src", "Pharmacy.Domain", "Pharmacy.Domain.csproj");

        Assert.Empty(project.Descendants("ProjectReference"));
    }

    [Fact]
    public void Check56_DomainDoesNotReferenceEntityFrameworkCore()
    {
        Assert.DoesNotContain("EntityFramework", SalesSources("Pharmacy.Domain"), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DbContext", SalesSources("Pharmacy.Domain"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Check57_DomainDoesNotReferenceWpfOrBlazor()
    {
        var source = SalesSources("Pharmacy.Domain");

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.AspNetCore.Components", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Blazor", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Check58_ApplicationDoesNotReferenceInfrastructure()
    {
        var project = Project("src", "Pharmacy.Application", "Pharmacy.Application.csproj");

        Assert.DoesNotContain(project.Descendants("ProjectReference"), reference =>
            reference.Attribute("Include")?.Value.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Check59_ApplicationDoesNotReferenceDesktop()
    {
        var project = Project("src", "Pharmacy.Application", "Pharmacy.Application.csproj");

        Assert.DoesNotContain(project.Descendants("ProjectReference"), reference =>
            reference.Attribute("Include")?.Value.Contains("Desktop", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain("Pharmacy.Desktop", SalesSources("Pharmacy.Application"), StringComparison.Ordinal);
    }

    [Fact]
    public void Check60_InfrastructureContainsOnlyApprovedInMemorySalesAdapters()
    {
        var contractTypes = new[]
        {
            typeof(IProductCatalogReader),
            typeof(IStockAvailabilityReader),
            typeof(ISaleDraftStore),
            typeof(ISaleDraftIdGenerator),
            typeof(ISaleFinalizationPreparationStore)
        };
        var infrastructureTypes = typeof(Pharmacy.Infrastructure.AssemblyMarker).Assembly.GetTypes();
        var implementations = infrastructureTypes
            .Where(type => !type.IsAbstract
                && contractTypes.Any(contract => contract.IsAssignableFrom(type)))
            .ToArray();

        Assert.Equal(5, implementations.Length);
        Assert.All(implementations, implementation =>
            Assert.Equal("Pharmacy.Infrastructure.Sales.InMemory", implementation.Namespace));
        Assert.DoesNotContain(infrastructureTypes, type =>
            !type.IsAbstract && typeof(ISaleFinalizer).IsAssignableFrom(type));
    }

    [Fact]
    public void Check61_DesktopSalesConnectionIsIsolatedInTheCoordinatorAndCompositionRoot()
    {
        var desktopSource = AllSource("src", "Pharmacy.Desktop");
        var page = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src",
                "Pharmacy.Desktop",
                "Components",
                "Pages",
                "DemoPosPage.razor"));

        Assert.Contains("SalesPosCoordinator", desktopSource, StringComparison.Ordinal);
        Assert.Contains("AddInMemorySalesAdapters", desktopSource, StringComparison.Ordinal);
        Assert.Contains("@inject SalesPosCoordinator", page, StringComparison.Ordinal);
        Assert.DoesNotContain("InMemorySaleDraftStore", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Check62_NoUnauthorizedPackageWasAdded()
    {
        var packages = Directory
            .EnumerateFiles(RepositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(path => XDocument.Load(path).Descendants("PackageReference"))
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(name => name is not null)
            .ToArray();
        var allowedPackages = new[]
        {
            "Microsoft.AspNetCore.Components.WebView.Wpf",
            "xunit.v3.mtp-v2"
        };

        Assert.All(packages, package => Assert.Contains(package, allowedPackages));
    }

    [Fact]
    public void Check63_RepositoryContainsNoSqlFile()
    {
        Assert.Empty(Directory.EnumerateFiles(RepositoryRoot, "*.sql", SearchOption.AllDirectories));
    }

    [Fact]
    public void Check64_SalesCoreContainsNoExternalUrl()
    {
        var source = SalesSources("Pharmacy.Domain") + SalesSources("Pharmacy.Application");

        Assert.DoesNotContain("http://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Check65_SalesCoreContainsNoClinicalType()
    {
        var source = SalesSources("Pharmacy.Domain") + SalesSources("Pharmacy.Application");
        var prohibitedTerms = new[]
        {
            "Prescription",
            "Posology",
            "TherapeuticAdvice",
            "ClinicalRecord",
            "DiagnosticSuggestion"
        };

        Assert.All(prohibitedTerms, term =>
            Assert.DoesNotContain(term, source, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Check66_SalesCoreContainsNoBankingDataType()
    {
        var source = SalesSources("Pharmacy.Domain") + SalesSources("Pharmacy.Application");
        var prohibitedTerms = new[]
        {
            "CardNumber",
            "BankAccount",
            "SecurityCode",
            "Cryptogram",
            "MagneticStripe"
        };

        Assert.All(prohibitedTerms, term =>
            Assert.DoesNotContain(term, source, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Check67_SyntheticPosRemainsCompilable()
    {
        var session = new DemoPosSession();

        Assert.NotEmpty(session.Catalog);
        Assert.All(session.Catalog, item => Assert.StartsWith("DEMO-", item.Code));
    }

    [Fact]
    public void Check68_DiagnosticAndUiLabRemainPresent()
    {
        var diagnostic = File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "Components", "DiagnosticScreen.razor"));
        var uiLab = File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "Components", "Pages", "UiLab.razor"));

        Assert.Contains("@page \"/diagnostic\"", diagnostic, StringComparison.Ordinal);
        Assert.Contains("@page \"/ui-lab\"", uiLab, StringComparison.Ordinal);
    }

    private static XDocument Project(params string[] segments)
    {
        return XDocument.Load(PathAt(segments));
    }

    private static string SalesSources(string projectName)
    {
        var salesPath = PathAt("src", projectName, "Sales");
        return Directory.Exists(salesPath) ? AllSource("src", projectName, "Sales") : string.Empty;
    }

    private static string AllSource(params string[] segments)
    {
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(PathAt(segments), "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));
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
