using Microsoft.EntityFrameworkCore;
using Pharmacy.Infrastructure.Persistence;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class PurchasingArchitectureTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void PurchasingLayersPreserveDependencyDirection()
    {
        var domain = AllText(PathAt("src", "Pharmacy.Domain", "Purchasing"));
        var application = AllText(PathAt("src", "Pharmacy.Application", "Purchasing"));
        var desktop = AllText(PathAt("src", "Pharmacy.Desktop"), "*.cs", "*.razor");
        Assert.DoesNotContain("EntityFrameworkCore", domain, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DbContext", domain, StringComparison.Ordinal);
        Assert.DoesNotContain("Pharmacy.Infrastructure", application, StringComparison.Ordinal);
        Assert.DoesNotContain("IQueryable", application, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", desktop, StringComparison.Ordinal);
        Assert.DoesNotContain("GenericRepository", AllText(PathAt("src")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PurchasingModelContainsExactlyFiveBigintTablesWithNoCascade()
    {
        using var context = CreateModelContext();
        var entities = context.Model.GetEntityTypes().Where(x => x.GetSchema() == "purchasing").ToArray();
        Assert.Equal(new[] { "GoodsReceiptLines", "GoodsReceipts", "PurchaseOrderLines", "PurchaseOrders", "Suppliers" }, entities.Select(x => x.GetTableName()).Order(StringComparer.Ordinal));
        Assert.All(entities, entity => Assert.Equal(typeof(long), entity.FindPrimaryKey()!.Properties.Single().ClrType));
        Assert.All(entities.SelectMany(x => x.GetForeignKeys()), foreignKey => Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior));
        Assert.DoesNotContain(entities.SelectMany(x => x.GetProperties()), property => property.GetColumnType() is "float" or "real" or "money");
        Assert.Equal(4, entities.SelectMany(x => x.GetProperties()).Count(x => x.IsConcurrencyToken && x.ClrType == typeof(byte[])));
        Assert.All(entities.SelectMany(x => x.GetProperties()).Where(x => x.Name.Contains("Quantity", StringComparison.Ordinal)), property => Assert.Equal(typeof(int), Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType));
        Assert.All(entities.SelectMany(x => x.GetProperties()).Where(x => x.Name.Contains("Price", StringComparison.Ordinal)), property => Assert.Equal("decimal(19,4)", property.GetColumnType()));
    }

    [Fact]
    public void MigrationAndScriptsAreAdditiveSyntheticAndOperational()
    {
        var migration = File.ReadAllText(PathAt("src", "Pharmacy.Infrastructure", "Persistence", "Migrations", "20260804214802_PurchasingPersistenceV01.cs"));
        Assert.Equal(5, Count(migration, "migrationBuilder.CreateTable("));
        Assert.Equal(5, Count(migration, "migrationBuilder.DropTable("));
        Assert.DoesNotContain("InsertData", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("ReferentialAction.Cascade", migration, StringComparison.Ordinal);
        Assert.Contains("PurchaseReceipt", migration, StringComparison.Ordinal);
        foreach (var path in new[] { PathAt("database", "dev", "seed_synthetic_purchasing_v01.sql"), PathAt("database", "dev", "clear_synthetic_purchasing_v01.sql"), PathAt("database", "verification", "verify_sprint12_purchasing.sql"), PathAt("database", "scripts", "004_purchasing_persistence_v01_idempotent.sql") }) Assert.True(File.Exists(path), path);
        var seed = File.ReadAllText(PathAt("database", "dev", "seed_synthetic_purchasing_v01.sql"));
        Assert.Contains("DB_NAME() <> N'Pharmacy_Development'", seed, StringComparison.Ordinal);
        Assert.Contains("BEGIN TRANSACTION", seed, StringComparison.Ordinal);
        Assert.Contains("DEMO-S12-", seed, StringComparison.Ordinal);
        Assert.DoesNotContain("GETDATE()", seed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DesktopProvidesAccessiblePurchasingWorkflowsAndNoFallback()
    {
        var page = File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "Components", "Pages", "PurchasingPage.razor"));
        var navigation = File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "Components", "Navigation", "AppNavigationCatalog.cs"));
        var css = File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "wwwroot", "css", "purchasing.css"));
        Assert.Contains("@page \"/module/purchasing\"", page, StringComparison.Ordinal);
        Assert.Contains("Fournisseurs", page, StringComparison.Ordinal); Assert.Contains("Commandes", page, StringComparison.Ordinal); Assert.Contains("Réceptions", page, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", page, StringComparison.Ordinal); Assert.Contains("role=\"alertdialog\"", page, StringComparison.Ordinal); Assert.Contains("Purchasing.IsBusy", page, StringComparison.Ordinal);
        Assert.Contains("Réception directe", page, StringComparison.Ordinal); Assert.Contains("ventes restent non persistées", page, StringComparison.OrdinalIgnoreCase); Assert.Contains("aucun calcul de marge", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"purchasing\"", navigation, StringComparison.Ordinal); Assert.Contains("AppNavigationAccessState.Available", navigation[navigation.IndexOf("\"purchasing\"", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.DoesNotContain("#", css, StringComparison.Ordinal); Assert.Contains("@media", css, StringComparison.Ordinal); Assert.Contains("overflow", css + File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "wwwroot", "css", "catalog.css")), StringComparison.Ordinal);
    }

    [Fact]
    public void RequiredSprint12DocumentationExists()
    {
        foreach (var relative in new[] { "docs/domain/PURCHASING_CORE_V0.1.md", "docs/application/PURCHASING_USE_CASES_V0.1.md", "docs/infrastructure/SQL_PURCHASING_ADAPTERS_V0.1.md", "docs/database/PURCHASING_PHYSICAL_SCHEMA_V0.1.md", "docs/database/PURCHASING_ERD_V0.1.mmd", "docs/ux/PURCHASING_RECEIVING_UI_V0.1.md", "docs/reports/sprint-12/purchasing-validation.md", "docs/reports/sprint-12/codex-report.md" }) Assert.True(File.Exists(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar))), relative);
    }

    private static PharmacyDbContext CreateModelContext() { var builder = new DbContextOptionsBuilder<PharmacyDbContext>(); builder.UsePharmacySqlServer("Server=localhost;Database=Pharmacy_Architecture_Only;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"); return new(builder.Options); }
    private static int Count(string text, string value) { var count=0; for(var i=0;(i=text.IndexOf(value,i,StringComparison.Ordinal))>=0;i+=value.Length)count++; return count; }
    private static string PathAt(params string[] parts) => Path.Combine([Root, .. parts]);
    private static string AllText(string path, params string[] patterns) { var filters=patterns.Length==0?["*.cs"]:patterns; return string.Join(Environment.NewLine,filters.SelectMany(pattern=>Directory.EnumerateFiles(path,pattern,SearchOption.AllDirectories)).Distinct().Select(File.ReadAllText)); }
    private static string FindRoot() { var directory=new DirectoryInfo(AppContext.BaseDirectory); while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"Pharmacy.slnx")))directory=directory.Parent; return directory?.FullName??throw new InvalidOperationException("Racine introuvable."); }
}
