using System.Text.RegularExpressions;
using Pharmacy.Application.Reporting.Models;
using Pharmacy.Application.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class ReportingArchitectureTests
{
    [Fact]
    public void ReportingKeepsApplicationContractsIndependentFromInfrastructure()
    {
        var root = RepositoryRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "Pharmacy.Application", "Reporting"), "*.cs", SearchOption.AllDirectories);
        Assert.All(files, file => Assert.DoesNotContain("Pharmacy.Infrastructure", File.ReadAllText(file), StringComparison.Ordinal));
    }

    [Fact]
    public void ReportingRouteIsAvailableAndPermissionProtected()
    {
        var root = RepositoryRoot();
        var navigation = File.ReadAllText(Path.Combine(root, "src", "Pharmacy.Desktop", "Components", "Navigation", "AppNavigationCatalog.cs"));
        var page = File.ReadAllText(Path.Combine(root, "src", "Pharmacy.Desktop", "Components", "Pages", "ReportingPage.razor"));
        Assert.Contains("\"/module/reporting\"", navigation, StringComparison.Ordinal);
        Assert.Contains("SecurityPermissions.ReportsView", navigation, StringComparison.Ordinal);
        Assert.Contains("AppNavigationAccessState.Available", navigation, StringComparison.Ordinal);
        Assert.Contains("@page \"/module/reporting\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void CsvExportKindsCoverAllDocumentedDetailedReports()
    {
        Assert.Equal(9, Enum.GetValues<ReportingExportKind>().Length);
        Assert.Contains(ReportingExportKind.InventoryAdjustments, Enum.GetValues<ReportingExportKind>());
        Assert.Contains(ReportingExportKind.CustomerCredit, Enum.GetValues<ReportingExportKind>());
        Assert.Contains(ReportingExportKind.Returns, Enum.GetValues<ReportingExportKind>());
        Assert.Contains(ReportingExportKind.Cash, Enum.GetValues<ReportingExportKind>());
    }

    [Fact]
    public void ReportingCssUsesExistingTokensAndDefinesResponsiveBreakpoints()
    {
        var root = RepositoryRoot();
        var css = File.ReadAllText(Path.Combine(root, "src", "Pharmacy.Desktop", "wwwroot", "css", "reporting.css"));
        Assert.DoesNotMatch(new Regex("#[0-9a-fA-F]{3,8}", RegexOptions.CultureInvariant), css);
        Assert.Contains("var(--space-", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 72rem)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 48rem)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportingMigrationCreatesGranularReadOnlyViewsAndNullableHorizon()
    {
        var root = RepositoryRoot();
        var migrations = Directory.EnumerateFiles(Path.Combine(root, "src", "Pharmacy.Infrastructure", "Persistence", "Migrations"), "*_ReportingViewsAndAlertSettingsV01.cs").ToArray();
        var migration = File.ReadAllText(Assert.Single(migrations));
        Assert.Equal(11, Regex.Matches(migration, "CREATE VIEW reporting\\.").Count);
        Assert.Contains("ExpiryHorizonDays = table.Column<int>(type: \"int\", nullable: true)", migration, StringComparison.Ordinal);
        Assert.Contains("Id = table.Column<long>(type: \"bigint\", nullable: false)", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE MATERIALIZED", migration, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx"))) directory = directory.Parent;
        return Assert.IsType<DirectoryInfo>(directory).FullName;
    }
}
