using Pharmacy.Application.Inventory.Models;
using Pharmacy.Domain.Inventory;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class InventoryDesktopGateTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void STK0613_DesktopGuardsDoubleSubmitAndKeepsOneFinalizationEntryPoint()
    {
        var coordinator = File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "Components", "Inventory", "InventoryCoordinator.cs"));
        Assert.Contains("SelectedSession is null || !TryBegin()", coordinator, StringComparison.Ordinal);
        Assert.Contains("Interlocked.CompareExchange(ref operationInProgress, 1, 0) == 0", coordinator, StringComparison.Ordinal);
        Assert.Contains("finally", coordinator, StringComparison.Ordinal);
        Assert.Contains("End();", coordinator, StringComparison.Ordinal);
        Assert.Equal(1, Count(coordinator, "new FinalizeInventorySessionCommand(Guid.NewGuid()"));
    }

    [Fact]
    public void STK0630_VarianceProjectionAndDesktopStatesAreExplicitAndControlled()
    {
        var view = new InventorySessionView(
            1, "STK-UI", 1, "MAIN", InventorySessionStatus.InProgress,
            new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc), null, null,
            Convert.ToBase64String(new byte[8]),
            [new InventoryCountLineView(1, 1, "P-1", "Produit", 1, "LOT-A", 10, 7, -3, Convert.ToBase64String(new byte[8]))]);
        Assert.Equal(-3, view.Lines[0].DifferenceQuantity);

        var page = File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "Components", "Pages", "InventoryPage.razor"));
        Assert.Contains("Inventory.SelectedSession.Lines.Any(line => line.CountedQuantity is null)", page, StringComparison.Ordinal);
        Assert.Contains("line.DifferenceQuantity?.ToString(\"+0;-0;0\")", page, StringComparison.Ordinal);
        Assert.Contains("inventory.database.unavailable", page, StringComparison.Ordinal);
        Assert.Contains("<UiEmptyState", page, StringComparison.Ordinal);
        Assert.Contains("aria-busy", page, StringComparison.Ordinal);
        Assert.Contains("Disabled=\"@Inventory.IsBusy\"", page, StringComparison.Ordinal);
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static string PathAt(params string[] segments) =>
        Path.Combine(new[] { RepositoryRoot }.Concat(segments).ToArray());

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx"))) return directory.FullName;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
