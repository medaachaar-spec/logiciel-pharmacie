using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Inventory.Models;
using Pharmacy.Application.Inventory.Queries;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Inventory;
using Pharmacy.Infrastructure.Persistence;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class InventoryArchitectureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void InventoryDomainAndApplicationRemainIndependentFromEfAndDesktop()
    {
        var source = AllText(PathAt("src", "Pharmacy.Domain", "Inventory"))
            + AllText(PathAt("src", "Pharmacy.Application", "Inventory"));

        Assert.DoesNotContain("EntityFrameworkCore", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Pharmacy.Infrastructure", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Pharmacy.Desktop", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IQueryable", source, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryModelContainsExactlySixTablesWithIntegerQuantitiesAndNoCascade()
    {
        using var context = CreateModelContext();
        var inventoryTypes = context.Model.GetEntityTypes()
            .Where(entity => entity.GetSchema() == "inventory")
            .ToArray();

        Assert.Equal(
            new[]
            {
                "InventoryCountLines", "InventorySessions", "StockBalances",
                "StockBatches", "StockLocations", "StockMovements"
            },
            inventoryTypes.Select(entity => entity.GetTableName()).Order(StringComparer.Ordinal));
        Assert.All(
            inventoryTypes.SelectMany(entity => entity.GetForeignKeys()),
            foreignKey => Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior));
        Assert.All(
            inventoryTypes.SelectMany(entity => entity.GetProperties())
                .Where(property => property.Name.Contains("Quantity", StringComparison.Ordinal)),
            property => Assert.Equal(typeof(int), Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType));
        Assert.Contains(
            inventoryTypes.SelectMany(entity => entity.GetProperties()),
            property => property.GetColumnType() == "date");
    }

    [Fact]
    public void MigrationCreatesOnlyInventoryTablesAndHasSafeDownOrder()
    {
        var migration = File.ReadAllText(PathAt(
            "src", "Pharmacy.Infrastructure", "Persistence", "Migrations",
            "20260804181233_InventoryPersistenceV01.cs"));

        Assert.Equal(6, Count(migration, "migrationBuilder.CreateTable("));
        Assert.Equal(6, Count(migration, "migrationBuilder.DropTable("));
        Assert.DoesNotContain("InsertData", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("ReferentialAction.Cascade", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("type: \"float\"", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("type: \"real\"", migration, StringComparison.Ordinal);
        Assert.True(
            migration.IndexOf("name: \"InventoryCountLines\"", migration.IndexOf("protected override void Down", StringComparison.Ordinal), StringComparison.Ordinal)
            < migration.LastIndexOf("name: \"StockLocations\"", StringComparison.Ordinal));
    }

    [Fact]
    public void DesktopUsesInventoryCoordinatorAndNeverOwnsADbContextOrMigration()
    {
        var desktop = AllText(PathAt("src", "Pharmacy.Desktop"), "*.cs", "*.razor");
        var composition = File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "SalesServiceCollectionExtensions.cs"));
        var page = File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "Components", "Pages", "InventoryPage.razor"));

        Assert.Contains("AddSqlInventoryAdapters", composition, StringComparison.Ordinal);
        Assert.Contains("InventoryCoordinator", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("SyntheticCatalogStockAvailabilityReader", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", desktop, StringComparison.Ordinal);
        Assert.DoesNotContain("Database.Migrate", desktop, StringComparison.Ordinal);
        Assert.Contains("@page \"/module/inventory\"", page, StringComparison.Ordinal);
        Assert.Contains("Finalisation atomique", page, StringComparison.Ordinal);
        Assert.Contains("Proposition FEFO", page, StringComparison.Ordinal);
        Assert.Contains("Lots expirés — non vendables", page, StringComparison.Ordinal);
        Assert.Contains("role=\"alertdialog\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryPageReusesCatalogSurfacesAndDesignSystemTokens()
    {
        var page = File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "Components", "Pages", "InventoryPage.razor"));
        var inventoryCss = File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "wwwroot", "css", "inventory.css"));
        var catalogCss = File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "wwwroot", "css", "catalog.css"));

        Assert.Contains("<h1 id=\"inventory-title\" tabindex=\"-1\">Stock SQL</h1>", page, StringComparison.Ordinal);
        Assert.Contains("Les ventes restent non persistées et ne consomment aucun stock.", page, StringComparison.Ordinal);
        Assert.Equal(4, Count(page, "<UiCard Density=\"UiCardDensity.Compact\">"));
        Assert.Contains("catalog-toolbar inventory-toolbar", page, StringComparison.Ordinal);
        Assert.Contains("catalog-panel inventory-panel inventory-list-panel", page, StringComparison.Ordinal);
        Assert.Contains("catalog-panel inventory-panel inventory-details-panel", page, StringComparison.Ordinal);
        Assert.Contains("catalog-table-scroll inventory-table-wrap", page, StringComparison.Ordinal);
        Assert.Contains("catalog-table inventory-table", page, StringComparison.Ordinal);
        Assert.Contains("catalog-row--selected", page, StringComparison.Ordinal);
        Assert.Contains("role=\"search\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-busy=", page, StringComparison.Ordinal);
        Assert.Contains("Sélectionnez un produit", page, StringComparison.Ordinal);
        Assert.Contains("Chargement du Stock SQL", page, StringComparison.Ordinal);
        Assert.Contains("Stock SQL indisponible", page, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"0\" role=\"region\"", page, StringComparison.Ordinal);

        Assert.Contains("overflow: auto", catalogCss, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 75rem)", inventoryCss, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 64rem)", inventoryCss, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 48rem)", inventoryCss, StringComparison.Ordinal);
        Assert.DoesNotContain("#", inventoryCss, StringComparison.Ordinal);
        Assert.DoesNotContain("--radius-lg", inventoryCss, StringComparison.Ordinal);
        Assert.DoesNotContain("--radius-md", inventoryCss, StringComparison.Ordinal);
        Assert.DoesNotContain("--color-brand-", inventoryCss, StringComparison.Ordinal);
        Assert.DoesNotContain("--color-text-muted", inventoryCss, StringComparison.Ordinal);
        Assert.DoesNotContain("--space-5", inventoryCss, StringComparison.Ordinal);
    }

    [Fact]
    public void PosUsesSqlAvailabilityAndSaleFinalizationConsumesStockThroughInfrastructure()
    {
        var composition = File.ReadAllText(PathAt("src", "Pharmacy.Infrastructure", "Inventory", "InventoryServiceCollectionExtensions.cs"));
        var adapter = File.ReadAllText(PathAt("src", "Pharmacy.Infrastructure", "Inventory", "SqlInventoryStockAvailabilityReader.cs"));
        var pos = File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "Components", "Pages", "DemoPosPage.razor"));

        Assert.Contains("IStockAvailabilityReader, SqlInventoryStockAvailabilityReader", composition, StringComparison.Ordinal);
        Assert.Contains("balance.StockBatch.ExpiryDate >= currentDate", adapter, StringComparison.Ordinal);
        Assert.Contains("allocations FEFO", pos, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordMovement", pos, StringComparison.Ordinal);
    }

    [Fact]
    public void DevelopmentInventoryScriptsAreGuardedIdempotentAndContainNoSecret()
    {
        var paths = new[]
        {
            PathAt("database", "dev", "seed_synthetic_inventory_v01.sql"),
            PathAt("database", "dev", "clear_synthetic_inventory_v01.sql"),
            PathAt("database", "verification", "verify_sprint11_inventory.sql"),
            PathAt("database", "scripts", "003_inventory_persistence_v01_idempotent.sql")
        };
        var scripts = paths.Select(File.ReadAllText).ToArray();

        Assert.All(scripts.Take(3), script => Assert.Contains("Pharmacy_Development", script, StringComparison.Ordinal));
        Assert.Contains("BEGIN TRANSACTION", scripts[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OperationId", scripts[0], StringComparison.Ordinal);
        Assert.Contains("DEMO-LOCATION-MAIN", scripts[0], StringComparison.Ordinal);
        Assert.DoesNotContain("LIKE N'DEMO-%'", scripts[1], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("__EFMigrationsHistory", scripts[3], StringComparison.Ordinal);
        Assert.All(scripts, script =>
        {
            Assert.DoesNotContain("Password=", script, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Pwd=", script, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task SqlOutageReturnsStableApplicationErrorsWithoutFallback()
    {
        var factory = new ThrowingDbContextFactory();
        var date = new FixedDateProvider();
        var queries = new InventoryQueryHandlers(new SqlInventoryReader(factory), date);
        var result = await queries.SearchStockAsync(
            new StockSearchCriteria("TEST"), TestContext.Current.CancellationToken);
        var availability = await new SqlInventoryStockAvailabilityReader(factory, date).GetAvailabilityAsync(
            ProductId.Create(1).Value,
            SaleQuantity.Create(1).Value,
            TestContext.Current.CancellationToken);

        Assert.Equal("inventory.database.unavailable", result.Error.Code);
        Assert.Equal("inventory.database.unavailable", availability.Error.Code);
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static PharmacyDbContext CreateModelContext()
    {
        var options = new DbContextOptionsBuilder<PharmacyDbContext>();
        options.UsePharmacySqlServer("Server=not-used.invalid;Database=ModelOnly;Integrated Security=True;Encrypt=True");
        return new PharmacyDbContext(options.Options);
    }

    private static string AllText(string root, params string[] patterns)
    {
        patterns = patterns.Length == 0 ? new[] { "*.cs" } : patterns;
        return string.Join(Environment.NewLine, patterns
            .SelectMany(pattern => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(File.ReadAllText));
    }

    private static string PathAt(params string[] segments) =>
        Path.Combine(new[] { RepositoryRoot }.Concat(segments).ToArray());

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class FixedDateProvider : Pharmacy.Application.Inventory.Contracts.IInventoryDateProvider
    {
        public DateOnly CurrentDate => new(2026, 8, 4);
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<PharmacyDbContext>
    {
        public PharmacyDbContext CreateDbContext() => throw new InvalidOperationException("Connexion SQL non configurée.");

        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<PharmacyDbContext>(new InvalidOperationException("Connexion SQL non configurée."));
    }
}
