using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class UiFoundationTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ExpectedCssFoundationFiles_Exist()
    {
        var cssDirectory = Path.Combine(
            RepositoryRoot,
            "src",
            "Pharmacy.Desktop",
            "wwwroot",
            "css");
        var expectedFiles = new[]
        {
            "tokens.css",
            "base.css",
            "components.css",
            "utilities.css",
            "app.css"
        };

        Assert.All(
            expectedFiles,
            fileName => Assert.True(
                File.Exists(Path.Combine(cssDirectory, fileName)),
                $"Expected CSS foundation file is missing: {fileName}"));
    }

    [Fact]
    public void TokensCss_DefinesRequiredDesignTokens()
    {
        var tokens = File.ReadAllText(DesktopPath("wwwroot", "css", "tokens.css"));
        var requiredTokens = new[]
        {
            "--color-accent:",
            "--color-accent-hover:",
            "--color-app-background:",
            "--color-surface:",
            "--color-text-primary:",
            "--color-success:",
            "--color-warning:",
            "--color-danger:",
            "--color-information:",
            "--space-1:",
            "--space-12:",
            "--radius-medium:",
            "--shadow-small:",
            "--control-height-medium:",
            "--content-max-width:",
            "--transition-standard:",
            "--z-index-navigation:",
            "--focus-ring-width:"
        };

        Assert.All(
            requiredTokens,
            token => Assert.Contains(token, tokens, StringComparison.Ordinal));
    }

    [Fact]
    public void DesktopWebAssets_DoNotContainExternalUrls()
    {
        var desktopRoot = DesktopPath();
        var files = Directory
            .EnumerateFiles(desktopRoot, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("index.html", StringComparison.OrdinalIgnoreCase))
            .Where(path => !IsGeneratedPath(path));
        var violations = files
            .Where(path => Regex.IsMatch(
                File.ReadAllText(path),
                @"https?://",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void DesktopWebAssets_DoNotReferenceACdn()
    {
        var files = Directory
            .EnumerateFiles(DesktopPath(), "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("index.html", StringComparison.OrdinalIgnoreCase))
            .Where(path => !IsGeneratedPath(path));
        var violations = files
            .Where(path => File.ReadAllText(path).Contains("cdn", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void EightRequiredUiComponents_Exist()
    {
        var uiDirectory = DesktopPath("Components", "Ui");
        var expectedComponents = new[]
        {
            "UiButton.razor",
            "UiBadge.razor",
            "UiCard.razor",
            "UiAlert.razor",
            "UiTextField.razor",
            "UiSelect.razor",
            "UiSpinner.razor",
            "UiEmptyState.razor"
        };

        Assert.All(
            expectedComponents,
            component => Assert.True(
                File.Exists(Path.Combine(uiDirectory, component)),
                $"Expected UI component is missing: {component}"));
    }

    [Fact]
    public void DesktopAssembly_IsLoadable()
    {
        var configuration = GetBuildConfiguration();
        var outputRoot = DesktopPath("bin", configuration);
        var frameworkDirectory = Directory
            .EnumerateDirectories(outputRoot, "net10.0-windows*", SearchOption.TopDirectoryOnly)
            .Single();
        var assemblyPath = Path.Combine(frameworkDirectory, "Pharmacy.Desktop.dll");
        var loadContext = new AssemblyLoadContext("Pharmacy.Desktop.UiFoundationTests", isCollectible: true);

        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

            Assert.Equal("Pharmacy.Desktop", assembly.GetName().Name);
        }
        finally
        {
            loadContext.Unload();
        }
    }

    [Fact]
    public void ProjectReferenceGraph_RemainsExactlyAllowed()
    {
        var expectedGraph = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Pharmacy.Domain"] = Array.Empty<string>(),
            ["Pharmacy.Application"] = new[] { "Pharmacy.Domain" },
            ["Pharmacy.Infrastructure"] = new[] { "Pharmacy.Application", "Pharmacy.Domain" },
            ["Pharmacy.Desktop"] = new[] { "Pharmacy.Application", "Pharmacy.Infrastructure" },
            ["Pharmacy.Deployment"] = new[] { "Pharmacy.Infrastructure" },
            ["Pharmacy.UnitTests"] = new[] { "Pharmacy.Application", "Pharmacy.Domain" },
            ["Pharmacy.IntegrationTests"] = new[]
            {
                "Pharmacy.Application",
                "Pharmacy.Domain",
                "Pharmacy.Infrastructure"
            }
        };
        var projectFiles = Directory
            .EnumerateFiles(RepositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .ToArray();

        Assert.Equal(expectedGraph.Count, projectFiles.Length);

        foreach (var projectFile in projectFiles)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectFile);
            var document = XDocument.Load(projectFile);
            var actualReferences = document
                .Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(include => !string.IsNullOrWhiteSpace(include))
                .Select(include => Path.GetFullPath(
                    Path.Combine(Path.GetDirectoryName(projectFile)!, include!)))
                .Select(Path.GetFileNameWithoutExtension)
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.True(
                expectedGraph.TryGetValue(projectName, out var expectedReferences),
                $"Unexpected project found: {projectName}");
            Assert.Equal(
                expectedReferences.Order(StringComparer.Ordinal),
                actualReferences);
        }
    }

    [Fact]
    public void DesktopProject_DoesNotReferenceAnExternalUiPackage()
    {
        var document = XDocument.Load(DesktopPath("Pharmacy.Desktop.csproj"));
        var packageReferences = document
            .Descendants("PackageReference")
            .Select(element => new
            {
                Name = element.Attribute("Include")?.Value,
                Version = element.Attribute("Version")?.Value
            })
            .ToArray();

        var package = Assert.Single(packageReferences);
        Assert.Equal("Microsoft.AspNetCore.Components.WebView.Wpf", package.Name);
        Assert.Equal("10.0.10", package.Version);
    }

    [Fact]
    public void SprintTwoSource_DoesNotDeclareAProhibitedBusinessType()
    {
        var prohibitedTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Product",
            "Produit",
            "Stock",
            "Batch",
            "Lot",
            "Inventory",
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
        var sourceFiles = Directory
            .EnumerateFiles(DesktopPath(), "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Where(path => !IsGeneratedPath(path));
        var declarationPattern = new Regex(
            @"\b(?:class|record|struct|interface)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.CultureInvariant);
        var prohibitedDeclarations = sourceFiles
            .SelectMany(path => declarationPattern
                .Matches(File.ReadAllText(path))
                .Select(match => match.Groups["name"].Value))
            .Where(prohibitedTypeNames.Contains)
            .ToArray();

        Assert.Empty(prohibitedDeclarations);
    }

    [Fact]
    public void ArchitectureMarkers_AreStillAvailable()
    {
        Assert.Equal(
            typeof(Domain.AssemblyMarker),
            typeof(Domain.AssemblyMarker).Assembly.GetType("Pharmacy.Domain.AssemblyMarker"));
        Assert.Equal(
            typeof(Application.AssemblyMarker),
            typeof(Application.AssemblyMarker).Assembly.GetType("Pharmacy.Application.AssemblyMarker"));
        Assert.Equal(
            typeof(Infrastructure.AssemblyMarker),
            typeof(Infrastructure.AssemblyMarker).Assembly.GetType("Pharmacy.Infrastructure.AssemblyMarker"));
    }

    private static string DesktopPath(params string[] segments)
    {
        return Path.Combine(
            new[] { RepositoryRoot, "src", "Pharmacy.Desktop" }.Concat(segments).ToArray());
    }

    private static bool IsGeneratedPath(string path)
    {
        var relativePath = Path.GetRelativePath(RepositoryRoot, path);
        return relativePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetBuildConfiguration()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (directory.Name is "Debug" or "Release")
            {
                return directory.Name;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The current build configuration could not be resolved.");
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
