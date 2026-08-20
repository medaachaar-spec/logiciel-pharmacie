using Pharmacy.Infrastructure.Purchasing;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class PurchasingDesktopGateTests
{
    [Fact]
    public void PUR018_DesktopDoubleClickIsGuardedByBusyStateAndStableOperationId()
    {
        var root = FindRoot();
        var coordinator = File.ReadAllText(Path.Combine(root, "src", "Pharmacy.Desktop", "Components", "Purchasing", "PurchasingCoordinator.cs"));
        var page = File.ReadAllText(Path.Combine(root, "src", "Pharmacy.Desktop", "Components", "Pages", "PurchasingPage.razor"));

        Assert.Contains("Interlocked.CompareExchange(ref operationInProgress, 1, 0) != 0", coordinator, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(ref operationInProgress, 0)", coordinator, StringComparison.Ordinal);
        Assert.Contains("Disabled=\"@Purchasing.IsBusy\"", page, StringComparison.Ordinal);
        Assert.Contains("receiptOperationId", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Guid.NewGuid()", coordinator, StringComparison.Ordinal);
    }

    [Fact]
    public void PurchasingSqlFailure_RecognizesInvalidOperationWrappedByPersistenceLayer()
    {
        var wrapped = new Exception(
            "Persistence wrapper",
            new InvalidOperationException("SQL operation failed"));

        Assert.True(PurchasingSqlFailure.IsUnavailable(wrapped));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Racine introuvable.");
    }
}
