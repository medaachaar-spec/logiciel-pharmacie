using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Infrastructure.Persistence;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class PersistenceArchitectureTests
{
    private static readonly object EnvironmentLock = new();
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void EfCorePackagesAndLocalToolUseStableVersionTen()
    {
        var infrastructureProject = XDocument.Load(PathAt(
            "src",
            "Pharmacy.Infrastructure",
            "Pharmacy.Infrastructure.csproj"));
        var packages = infrastructureProject
            .Descendants("PackageReference")
            .ToDictionary(
                package => package.Attribute("Include")!.Value,
                package => package,
                StringComparer.Ordinal);

        Assert.Equal("10.0.10", packages["Microsoft.EntityFrameworkCore.SqlServer"].Attribute("Version")?.Value);
        Assert.Equal("10.0.10", packages["Microsoft.EntityFrameworkCore.Design"].Attribute("Version")?.Value);
        Assert.Equal("all", packages["Microsoft.EntityFrameworkCore.Design"].Element("PrivateAssets")?.Value);
        Assert.Contains("buildtransitive", packages["Microsoft.EntityFrameworkCore.Design"].Element("IncludeAssets")?.Value);

        using var manifest = JsonDocument.Parse(File.ReadAllText(PathAt(".config", "dotnet-tools.json")));
        var tool = manifest.RootElement.GetProperty("tools").GetProperty("dotnet-ef");
        Assert.Equal("10.0.10", tool.GetProperty("version").GetString());
        Assert.Contains(tool.GetProperty("commands").EnumerateArray(), command => command.GetString() == "dotnet-ef");
        Assert.DoesNotContain("preview", File.ReadAllText(PathAt(".config", "dotnet-tools.json")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DomainAndApplicationContainNoEfCorePackageReference()
    {
        foreach (var project in new[]
                 {
                     PathAt("src", "Pharmacy.Domain", "Pharmacy.Domain.csproj"),
                     PathAt("src", "Pharmacy.Application", "Pharmacy.Application.csproj")
                 })
        {
            var packages = XDocument.Load(project)
                .Descendants("PackageReference")
                .Select(package => package.Attribute("Include")?.Value)
                .ToArray();

            Assert.DoesNotContain(packages, package =>
                package?.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) == true);
        }
    }

    [Fact]
    public void DesktopHasNoDbContextOrAutomaticMigration()
    {
        var source = AllText(PathAt("src", "Pharmacy.Desktop"), "*.cs", "*.razor", "*.csproj");
        var prohibited = new[]
        {
            "DbContext",
            "Database.Migrate",
            "EnsureCreated",
            "EnsureDeleted",
            "AddPharmacySqlServerPersistence",
            "PHARMACY_SQLSERVER_CONNECTION"
        };

        Assert.All(prohibited, token =>
            Assert.DoesNotContain(token, source, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("AddCatalogBackedSales", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionSourcesContainNoEnsureCreatedOrSensitiveLogging()
    {
        var source = string.Join(Environment.NewLine, new[]
        {
            AllText(PathAt("src", "Pharmacy.Domain"), "*.cs", "*.csproj"),
            AllText(PathAt("src", "Pharmacy.Application"), "*.cs", "*.csproj"),
            AllText(PathAt("src", "Pharmacy.Desktop"), "*.cs", "*.csproj", "*.razor"),
            string.Join(Environment.NewLine, Directory.EnumerateFiles(PathAt("src", "Pharmacy.Infrastructure"), "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Deployment{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText))
        });

        Assert.DoesNotContain("EnsureCreated", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureDeleted", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EnableSensitiveDataLogging", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Database.Migrate", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FactoryRefusesMissingConnectionWithoutRevealingAValue()
    {
        lock (EnvironmentLock)
        {
            var previous = Environment.GetEnvironmentVariable(
                PharmacyDbContextFactory.ConnectionEnvironmentVariable);

            try
            {
                Environment.SetEnvironmentVariable(
                    PharmacyDbContextFactory.ConnectionEnvironmentVariable,
                    null);
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    new PharmacyDbContextFactory().CreateDbContext([]));

                Assert.Contains(
                    PharmacyDbContextFactory.ConnectionEnvironmentVariable,
                    exception.Message,
                    StringComparison.Ordinal);
                Assert.DoesNotContain("Server=", exception.Message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Password=", exception.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    PharmacyDbContextFactory.ConnectionEnvironmentVariable,
                    previous);
            }
        }
    }

    [Fact]
    public void SqlServerOptionsDeclareCompatibilityRetryAndExplicitMigrationsAssembly()
    {
        var source = File.ReadAllText(PathAt(
            "src",
            "Pharmacy.Infrastructure",
            "Persistence",
            "PharmacySqlServerOptions.cs"));

        Assert.Contains("UseSqlServer", source, StringComparison.Ordinal);
        Assert.Contains("UseCompatibilityLevel(CompatibilityLevel)", source, StringComparison.Ordinal);
        Assert.Contains("CompatibilityLevel = 170", source, StringComparison.Ordinal);
        Assert.Contains("EnableRetryOnFailure()", source, StringComparison.Ordinal);
        Assert.Contains("MigrationsAssembly", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EnableSensitiveDataLogging", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ModelContainsConfirmedBusinessSecurityAndAuditTables()
    {
        using var context = CreateModelContext();
        var tables = context.Model.GetEntityTypes()
            .Select(entity => $"{entity.GetSchema()}.{entity.GetTableName()}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "audit.AuditEventDetails",
                "audit.AuditEvents",
                "cash.CashLedgerEntries",
                "catalog.ProductBarcodes",
                "catalog.ProductCategories",
                "catalog.ProductLocalSettings",
                "catalog.ProductPriceHistories",
                "catalog.Products",
                "configuration.PharmacyBranding",
                "customers.CustomerLedgerEntries",
                "customers.Customers",
                "inventory.InventoryCountLines",
                "inventory.InventorySessions",
                "inventory.StockBalances",
                "inventory.StockBatches",
                "inventory.StockLocations",
                "inventory.StockMovements",
                "operations.BackupRecords",
                "operations.ImportJobs",
                "purchasing.GoodsReceiptLines",
                "purchasing.GoodsReceipts",
                "purchasing.PurchaseOrderLines",
                "purchasing.PurchaseOrders",
                "purchasing.Suppliers",
                "reporting.ReportSettings",
                "sales.Payments",
                "sales.SaleLineAllocations",
                "sales.SaleLines",
                "sales.SaleOperations",
                "sales.SaleReturnLines",
                "sales.SaleReturns",
                "sales.Sales",
                "sales.SuspendedSaleLines",
                "sales.SuspendedSales",
                "security.AuthenticationEvents",
                "security.Permissions",
                "security.RecoveryCodes",
                "security.RolePermissions",
                "security.Roles",
                "security.UserAccounts",
                "security.UserRoles"
            },
            tables);
        Assert.All(context.Model.GetEntityTypes(), entity => Assert.False(entity.ClrType.IsPublic));
    }

    [Fact]
    public void ModelUsesBigintKeysExplicitTextLengthsAndApprovedNumericTypes()
    {
        using var context = CreateModelContext();

        Assert.All(context.Model.GetEntityTypes(), entity =>
        {
            var primaryKey = Assert.Single(entity.FindPrimaryKey()!.Properties);
            Assert.Equal(typeof(long), primaryKey.ClrType);
            Assert.Equal("bigint", primaryKey.GetColumnType());
        });

        var stringProperties = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Where(property => property.ClrType == typeof(string))
            .ToArray();
        Assert.NotEmpty(stringProperties);
        Assert.All(stringProperties, property =>
        {
            Assert.NotNull(property.GetMaxLength());
            Assert.StartsWith("nvarchar(", property.GetColumnType(), StringComparison.Ordinal);
        });

        var storeTypes = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Select(property => property.GetColumnType())
            .Where(type => type is not null)
            .ToArray();
        Assert.Contains("decimal(19,4)", storeTypes);
        Assert.Contains("decimal(18,3)", storeTypes);
        Assert.Contains("datetime2(7)", storeTypes);
        Assert.DoesNotContain(storeTypes, type =>
            type is "float" or "real" or "money" or "smallmoney" or "datetime");
    }

    [Fact]
    public void MutableTablesUseRowVersionAndRelationshipsNeverCascade()
    {
        using var context = CreateModelContext();
        var rowVersions = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Where(property => property.GetColumnType() == "rowversion")
            .ToArray();

        Assert.Equal(22, rowVersions.Length);
        Assert.All(rowVersions, property =>
        {
            Assert.True(property.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
        });

        var foreignKeys = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetForeignKeys())
            .ToArray();
        Assert.NotEmpty(foreignKeys);
        Assert.All(foreignKeys, foreignKey =>
            Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void MigrationAndIdempotentScriptContainNoSeedOrSecret()
    {
        using var context = CreateModelContext();
        Assert.Single(context.Database.GetMigrations(), migration =>
            migration.EndsWith("_InitialCatalogPersistenceV01", StringComparison.Ordinal));

        var migrationSource = AllText(
            PathAt("src", "Pharmacy.Infrastructure", "Persistence", "Migrations"),
            "*.cs");
        Assert.DoesNotContain("InsertData(\r\n                schema: \"security\",\r\n                table: \"UserAccounts\"", migrationSource, StringComparison.Ordinal);
        Assert.Contains("InsertData", migrationSource, StringComparison.Ordinal);
        Assert.Contains("protected override void Down", migrationSource, StringComparison.Ordinal);

        var script = File.ReadAllText(PathAt(
            "database",
            "scripts",
            "001_initial_catalog_foundation_idempotent.sql"));
        Assert.Contains("IF NOT EXISTS", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("__EFMigrationsHistory", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Pwd=", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DesktopCompositionUsesSqlCatalogAndKeepsOnlyDraftStateInMemory()
    {
        var contracts = new[]
        {
            typeof(IProductCatalogReader),
            typeof(IStockAvailabilityReader),
            typeof(ISaleDraftStore),
            typeof(ISaleDraftIdGenerator),
            typeof(ISaleFinalizationPreparationStore),
            typeof(ISaleFinalizer),
            typeof(ISalesReader)
        };
        var implementations = typeof(Pharmacy.Infrastructure.AssemblyMarker).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && contracts.Any(contract => contract.IsAssignableFrom(type)))
            .ToArray();

        Assert.Contains(implementations, implementation =>
            implementation.FullName == "Pharmacy.Infrastructure.Catalog.SqlProductCatalogReader");
        Assert.Contains(implementations, implementation =>
            implementation.FullName == "Pharmacy.Infrastructure.Inventory.SqlInventoryStockAvailabilityReader");
        Assert.Contains(implementations, implementation =>
            implementation.FullName == "Pharmacy.Infrastructure.Sales.SqlSaleFinalizer");
        Assert.Contains(implementations, implementation =>
            implementation.FullName == "Pharmacy.Infrastructure.Sales.SqlSalesReader");

        var composition = File.ReadAllText(PathAt(
            "src",
            "Pharmacy.Desktop",
            "SalesServiceCollectionExtensions.cs"));
        Assert.Contains("AddSqlCatalogAdapters(databaseEndpoint)", composition, StringComparison.Ordinal);
        Assert.Contains("AddSqlInventoryAdapters", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("SyntheticCatalogStockAvailabilityReader", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("InMemoryProductCatalogReader", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("InMemoryStockAvailabilityReader", composition, StringComparison.Ordinal);
        Assert.Contains("AddSqlSalesAdapters", composition, StringComparison.Ordinal);

        var finalizer = File.ReadAllText(PathAt(
            "src", "Pharmacy.Infrastructure", "Sales", "SqlSaleFinalizer.cs"));
        var failures = File.ReadAllText(PathAt(
            "src", "Pharmacy.Infrastructure", "Sales", "SalesSqlFailure.cs"));
        Assert.Contains("IsolationLevel.Serializable", finalizer, StringComparison.Ordinal);
        Assert.Contains("StockConcurrencyConflict", finalizer, StringComparison.Ordinal);
        Assert.Contains("1205 or 1222 or 3960", failures, StringComparison.Ordinal);
    }

    private static PharmacyDbContext CreateModelContext()
    {
        var options = new DbContextOptionsBuilder<PharmacyDbContext>();
        options.UsePharmacySqlServer(
            "Server=not-used.invalid;Database=ModelOnly;Integrated Security=True;Encrypt=True");
        return new PharmacyDbContext(options.Options);
    }

    private static string AllText(string root, params string[] patterns)
    {
        return string.Join(
            Environment.NewLine,
            patterns.SelectMany(pattern => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
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

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}

[CollectionDefinition("Sprint 09 persistence serial", DisableParallelization = true)]
public sealed class Sprint09PersistenceCollection
{
}
