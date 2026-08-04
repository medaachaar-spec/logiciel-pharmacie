using System.Reflection;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Catalog.Contracts;
using Pharmacy.Application.Catalog.Models;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Infrastructure.Catalog;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class CatalogArchitectureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void CatalogApplicationDoesNotDependOnInfrastructureOrEfCore()
    {
        var project = XDocument.Load(PathAt("src", "Pharmacy.Application", "Pharmacy.Application.csproj"));
        var source = AllText(PathAt("src", "Pharmacy.Application", "Catalog"), "*.cs");

        Assert.DoesNotContain(project.Descendants("ProjectReference"), reference =>
            reference.Attribute("Include")?.Value.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain("EntityFrameworkCore", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DbContext", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IQueryable", PublicContractText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DesktopContainsNoDbContextAndUsesSqlCatalogComposition()
    {
        var desktop = AllText(PathAt("src", "Pharmacy.Desktop"), "*.cs", "*.razor");
        var composition = File.ReadAllText(PathAt(
            "src", "Pharmacy.Desktop", "SalesServiceCollectionExtensions.cs"));

        Assert.DoesNotContain("DbContext", desktop, StringComparison.Ordinal);
        Assert.Contains("AddSqlCatalogAdaptersFromEnvironment", composition, StringComparison.Ordinal);
        Assert.Contains("AddSqlInventoryAdapters", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("InMemoryProductCatalogReader", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("SyntheticCatalogStockAvailabilityReader", composition, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlCatalogUsesShortContextsNoTrackingLimitsAndNoGenericRepository()
    {
        var source = AllText(PathAt("src"), "*.cs");
        var reader = File.ReadAllText(PathAt(
            "src", "Pharmacy.Infrastructure", "Catalog", "SqlProductCatalogReader.cs"));
        var store = File.ReadAllText(PathAt(
            "src", "Pharmacy.Infrastructure", "Catalog", "SqlCatalogStore.cs"));

        Assert.Contains("IDbContextFactory<PharmacyDbContext>", reader, StringComparison.Ordinal);
        Assert.Contains("IDbContextFactory<PharmacyDbContext>", store, StringComparison.Ordinal);
        Assert.Contains("AsNoTracking()", reader, StringComparison.Ordinal);
        Assert.Contains("Take(25)", reader, StringComparison.Ordinal);
        Assert.Contains("Take(criteria.Limit)", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("IRepository<", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Run", reader + store, StringComparison.Ordinal);
        Assert.DoesNotContain(".Result", reader + store, StringComparison.Ordinal);
        Assert.DoesNotContain(".Wait(", reader + store, StringComparison.Ordinal);
    }

    [Fact]
    public void EfEntitiesRemainInternalAndApplicationModelsUseReadOnlyCollections()
    {
        var infrastructureTypes = typeof(SqlProductCatalogReader).Assembly.GetTypes();
        Assert.All(
            infrastructureTypes.Where(type => type.Namespace ==
                "Pharmacy.Infrastructure.Persistence.Entities.Catalog"),
            type => Assert.False(type.IsPublic));

        Assert.Equal(
            typeof(IReadOnlyList<CatalogBarcodeView>),
            typeof(CatalogProductDetails).GetProperty(nameof(CatalogProductDetails.Barcodes))?.PropertyType);
        Assert.Equal(
            typeof(IReadOnlyList<CatalogPriceHistoryView>),
            typeof(CatalogProductDetails).GetProperty(nameof(CatalogProductDetails.PriceHistory))?.PropertyType);
    }

    [Fact]
    public void OnlyTheMinimalProductsActivationMigrationWasAdded()
    {
        var migrations = Directory.EnumerateFiles(
                PathAt("src", "Pharmacy.Infrastructure", "Persistence", "Migrations"),
                "*CatalogPersistenceEnhancementsV01.cs")
            .Where(path => !path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var migration = File.ReadAllText(Assert.Single(migrations));

        Assert.Contains("AddColumn<bool>", migration, StringComparison.Ordinal);
        Assert.Contains("name: \"IsActive\"", migration, StringComparison.Ordinal);
        Assert.Contains("table: \"Products\"", migration, StringComparison.Ordinal);
        Assert.Contains("defaultValue: true", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateTable", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("InsertData", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopCatalogCoversRequiredStatesAndNoSqlFallback()
    {
        var page = File.ReadAllText(PathAt(
            "src", "Pharmacy.Desktop", "Components", "Pages", "CatalogPage.razor"));
        var diagnostic = File.ReadAllText(PathAt(
            "src", "Pharmacy.Desktop", "Components", "DiagnosticScreen.razor"));
        var pos = File.ReadAllText(PathAt(
            "src", "Pharmacy.Desktop", "Components", "Pages", "DemoPosPage.razor"));

        Assert.Contains("Recherche SQL", page, StringComparison.Ordinal);
        Assert.Contains("Nouveau produit", page, StringComparison.Ordinal);
        Assert.Contains("Codes-barres", page, StringComparison.Ordinal);
        Assert.Contains("Prix et historique", page, StringComparison.Ordinal);
        Assert.Contains("Catégories", page, StringComparison.Ordinal);
        Assert.Contains("UiSpinner", page, StringComparison.Ordinal);
        Assert.Contains("UiEmptyState", page, StringComparison.Ordinal);
        Assert.Contains("Catalog.IsBusy", page, StringComparison.Ordinal);
        Assert.Contains("Aucun fallback", diagnostic, StringComparison.Ordinal);
        Assert.Contains("Catalogue SQL", pos, StringComparison.Ordinal);
        Assert.Contains("vente n’est pas persistée", pos, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DevelopmentScriptsAreGuardedSyntheticAndContainNoSecret()
    {
        var seed = File.ReadAllText(PathAt("database", "dev", "seed_synthetic_catalog_v01.sql"));
        var clear = File.ReadAllText(PathAt("database", "dev", "clear_synthetic_catalog_v01.sql"));
        var verification = File.ReadAllText(PathAt(
            "database", "verification", "verify_sprint10_catalog.sql"));
        var all = seed + clear + verification;

        Assert.All(new[] { seed, clear, verification }, script =>
            Assert.Contains("Pharmacy_Development", script, StringComparison.Ordinal));
        Assert.Contains("DEMO-000001", seed, StringComparison.Ordinal);
        Assert.Contains("DEMO-000010", seed, StringComparison.Ordinal);
        Assert.Contains("BEGIN TRANSACTION", seed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT EXISTS", seed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("InternalCode LIKE N'DEMO-%'", clear, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", clear, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Pwd=", all, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CatalogContractsAndSalesContractHaveOnlyApprovedSqlExposure()
    {
        var publicTypes = typeof(ICatalogProductReader).Assembly.GetExportedTypes()
            .Where(type => type.Namespace?.StartsWith("Pharmacy.Application.Catalog", StringComparison.Ordinal) == true)
            .ToArray();
        var prohibited = new[] { typeof(DbContext), typeof(IQueryable) };

        Assert.DoesNotContain(
            publicTypes.SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)),
            member => prohibited.Any(type => member.ToString()?.Contains(type.Name, StringComparison.Ordinal) == true));
        Assert.Contains(typeof(IProductCatalogReader), typeof(SqlProductCatalogReader).GetInterfaces());
        Assert.Contains(typeof(ICatalogProductReader), typeof(SqlProductCatalogReader).GetInterfaces());
    }

    private static string PublicContractText() => string.Join(
        Environment.NewLine,
        typeof(ICatalogProductReader).Assembly.GetExportedTypes()
            .Where(type => type.Namespace?.StartsWith("Pharmacy.Application.Catalog", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetMembers())
            .Select(member => member.ToString()));

    private static string AllText(string root, params string[] patterns) => string.Join(
        Environment.NewLine,
        patterns.SelectMany(pattern => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(File.ReadAllText));

    private static string PathAt(params string[] segments) =>
        Path.Combine(new[] { RepositoryRoot }.Concat(segments).ToArray());

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
