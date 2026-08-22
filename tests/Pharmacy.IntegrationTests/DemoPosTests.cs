using Pharmacy.Desktop.Components.Pos;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class DemoPosTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Catalog_ContainsBetweenEightAndTwelveSyntheticItems()
    {
        Assert.InRange(DemoCatalog.Items.Count, 8, 12);
        Assert.All(DemoCatalog.Items, item => Assert.StartsWith("DEMO-", item.Code));
    }

    [Fact]
    public void Catalog_CodesAreUnique()
    {
        Assert.Equal(
            DemoCatalog.Items.Count,
            DemoCatalog.Items.Select(item => item.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Catalog_IdentifiersAreUnique()
    {
        Assert.Equal(
            DemoCatalog.Items.Count,
            DemoCatalog.Items.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public void Catalog_PricesAndAvailabilityAreStrictlyPositive()
    {
        Assert.All(DemoCatalog.Items, item =>
        {
            Assert.True(item.UnitPrice > 0m);
            Assert.True(item.AvailableQuantity > 0);
        });
    }

    [Fact]
    public void FinancialProperties_UseDecimalAmounts()
    {
        var amountProperties = new[]
        {
            typeof(DemoCatalogItem).GetProperty(nameof(DemoCatalogItem.UnitPrice)),
            typeof(DemoCartLine).GetProperty(nameof(DemoCartLine.Subtotal)),
            typeof(DemoPosSession).GetProperty(nameof(DemoPosSession.Total)),
            typeof(DemoPosSession).GetProperty(nameof(DemoPosSession.ReceivedAmount)),
            typeof(DemoPosSession).GetProperty(nameof(DemoPosSession.Change)),
            typeof(DemoCompletedSale).GetProperty(nameof(DemoCompletedSale.Total)),
            typeof(DemoCompletedSale).GetProperty(nameof(DemoCompletedSale.Change))
        };

        Assert.All(amountProperties, property =>
        {
            Assert.NotNull(property);
            Assert.True(property.PropertyType == typeof(decimal)
                || property.PropertyType == typeof(decimal?));
        });
    }

    [Fact]
    public void AddItem_CreatesOneLineAtQuantityOne()
    {
        var session = new DemoPosSession();

        var result = session.AddItem(DemoCatalog.Items[0].Id);

        Assert.Equal(DemoCartMutationResult.Added, result);
        var line = Assert.Single(session.Lines);
        Assert.Equal(1, line.Quantity);
    }

    [Fact]
    public void AddItem_TwiceMergesTheExistingLine()
    {
        var session = new DemoPosSession();
        var item = DemoCatalog.Items[0];

        session.AddItem(item.Id);
        var result = session.AddItem(item.Id);

        Assert.Equal(DemoCartMutationResult.Merged, result);
        Assert.Equal(2, Assert.Single(session.Lines).Quantity);
    }

    [Fact]
    public void Increase_UpdatesQuantitySubtotalAndTotal()
    {
        var session = new DemoPosSession();
        var item = DemoCatalog.Items[1];
        session.AddItem(item.Id);

        session.Increase(item.Id);

        var line = Assert.Single(session.Lines);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(item.UnitPrice * 2, line.Subtotal);
        Assert.Equal(item.UnitPrice * 2, session.Total);
    }

    [Fact]
    public void Decrease_FromOneNeverCreatesZeroOrNegativeQuantity()
    {
        var session = new DemoPosSession();
        var item = DemoCatalog.Items[0];
        session.AddItem(item.Id);

        var result = session.Decrease(item.Id);

        Assert.Equal(DemoCartMutationResult.MinimumReached, result);
        Assert.Equal(1, Assert.Single(session.Lines).Quantity);
    }

    [Fact]
    public void Remove_DeletesTheSelectedLine()
    {
        var session = new DemoPosSession();
        var item = DemoCatalog.Items[0];
        session.AddItem(item.Id);

        var result = session.Remove(item.Id);

        Assert.Equal(DemoCartMutationResult.Removed, result);
        Assert.Empty(session.Lines);
    }

    [Fact]
    public void Total_EqualsTheSumOfLineSubtotals()
    {
        var session = new DemoPosSession();
        session.AddItem(DemoCatalog.Items[0].Id);
        session.AddItem(DemoCatalog.Items[1].Id);
        session.Increase(DemoCatalog.Items[1].Id);

        Assert.Equal(session.Lines.Sum(line => line.Subtotal), session.Total);
    }

    [Fact]
    public void EmptyCart_TotalIsZero()
    {
        Assert.Equal(0m, new DemoPosSession().Total);
    }

    [Fact]
    public void Quantity_NeverExceedsSyntheticAvailability()
    {
        var session = new DemoPosSession();
        var item = DemoCatalog.Items[4];

        for (var attempt = 0; attempt < item.AvailableQuantity + 3; attempt++)
        {
            session.AddItem(item.Id);
        }

        var line = Assert.Single(session.Lines);
        Assert.Equal(item.AvailableQuantity, line.Quantity);
        Assert.Equal(DemoCartMutationResult.AvailabilityReached, session.Increase(item.Id));
    }

    [Fact]
    public void ExactKnownCode_AddsTheMatchingItem()
    {
        var session = new DemoPosSession();
        var item = DemoCatalog.Items[2];

        var result = session.TryAddByExactCode(item.Code.ToLowerInvariant());

        Assert.Equal(DemoCartMutationResult.Added, result);
        Assert.Equal(item.Id, Assert.Single(session.Lines).Item.Id);
    }

    [Fact]
    public void UnknownCode_DoesNotCreateAnything()
    {
        var session = new DemoPosSession();

        var result = session.TryAddByExactCode("DEMO-UNKNOWN");

        Assert.Equal(DemoCartMutationResult.NotFound, result);
        Assert.Empty(session.Lines);
        Assert.Equal(DemoCatalog.Items.Count, session.Catalog.Count);
    }

    [Fact]
    public void Search_IsCaseInsensitiveForNameAndCode()
    {
        var session = new DemoPosSession();

        var byName = session.Search("hYgIèNe");
        var byCode = session.Search("demo-000003");

        Assert.Contains(byName, item => item.Code == "DEMO-000006");
        Assert.Single(byCode);
        Assert.Equal("DEMO-000003", byCode[0].Code);
    }

    [Fact]
    public void CashPayment_RejectsAnInsufficientAmount()
    {
        var session = CreatePaymentSession();
        session.SelectPaymentMethod(DemoPaymentMethod.Cash);
        session.SetReceivedAmount(session.Total - 0.01m);

        Assert.False(session.CanComplete);
        Assert.False(session.TryBeginCompletion());
    }

    [Fact]
    public void CashPayment_ComputesExactChange()
    {
        var session = CreatePaymentSession();
        session.SelectPaymentMethod(DemoPaymentMethod.Cash);
        session.SetReceivedAmount(session.Total + 7.35m);

        Assert.Equal(7.35m, session.Change);
        Assert.True(session.CanComplete);
    }

    [Fact]
    public void CardPayment_RequiresExternalTerminalConfirmation()
    {
        var session = CreatePaymentSession();
        session.SelectPaymentMethod(DemoPaymentMethod.ExternalCard);

        Assert.False(session.CanComplete);

        session.ConfirmExternalTerminal(true);

        Assert.True(session.CanComplete);
    }

    [Fact]
    public void Completion_BlocksASecondValidationAttempt()
    {
        var session = CreateReadyCashSession();

        Assert.True(session.TryBeginCompletion());
        Assert.True(session.IsCompletionInProgress);
        Assert.False(session.TryBeginCompletion());
    }

    [Fact]
    public void Completion_ProducesOnlyAnInMemorySyntheticResult()
    {
        var session = CreateReadyCashSession();
        session.TryBeginCompletion();

        var result = session.CompleteSale();

        Assert.Equal("DEMO-SALE-0001", result.Reference);
        Assert.Equal(DemoPosStep.Success, session.Step);
        Assert.Same(result, session.Result);
    }

    [Fact]
    public void NewSale_ResetClearsCartPaymentAndResult()
    {
        var session = CreateReadyCashSession();
        session.TryBeginCompletion();
        session.CompleteSale();

        session.ResetForNewSale();

        Assert.Empty(session.Lines);
        Assert.Equal(0m, session.Total);
        Assert.Equal(DemoPosStep.Shopping, session.Step);
        Assert.Equal(DemoPaymentMethod.None, session.PaymentMethod);
        Assert.Null(session.ReceivedAmount);
        Assert.Null(session.Result);
        Assert.False(session.IsExternalTerminalConfirmed);
        Assert.False(session.IsCompletionInProgress);
    }

    [Fact]
    public void PosSource_DoesNotIntroducePersistenceOrNetworkAccess()
    {
        var posDirectory = DesktopPath("Components", "Pos");
        var sources = Directory
            .EnumerateFiles(posDirectory, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText)
            .ToArray();
        var prohibitedTokens = new[]
        {
            "DbContext",
            "EntityFramework",
            "SqlConnection",
            "ConnectionString",
            "HttpClient",
            "https://",
            "http://"
        };

        Assert.All(
            prohibitedTokens,
            token => Assert.DoesNotContain(
                sources,
                source => source.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void SalesRoute_IsImplementedByTheDedicatedPosPage()
    {
        var page = DesktopText("Components", "Pages", "DemoPosPage.razor");
        var placeholder = DesktopText("Components", "Pages", "ModulePlaceholder.razor");
        var navigation = DesktopText("Components", "Navigation", "AppNavigationCatalog.cs");

        Assert.Contains("@page \"/module/sales\"", page, StringComparison.Ordinal);
        Assert.Contains("Nouvelle vente", page, StringComparison.Ordinal);
        Assert.DoesNotContain("module/sales", placeholder, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"sales\",\n                \"Vente\"", navigation, StringComparison.Ordinal);
        Assert.Contains("AppNavigationAccessState.Available", navigation, StringComparison.Ordinal);
    }

    [Fact]
    public void OtherBusinessModuleRoutes_RemainAvailableAsPlaceholders()
    {
        var navigation = DesktopText("Components", "Navigation", "AppNavigationCatalog.cs");
        var expectedRoutes = new[]
        {
            "/module/catalog",
            "/module/inventory",
            "/module/purchasing",
            "/module/cash",
            "/module/customers",
            "/module/reporting",
            "/module/administration"
        };

        Assert.All(expectedRoutes, route => Assert.Contains(route, navigation, StringComparison.Ordinal));
    }

    [Fact]
    public void DiagnosticAndUiLabRoutes_RemainAccessibleInDebugSource()
    {
        var diagnostic = DesktopText("Components", "DiagnosticScreen.razor");
        var uiLab = DesktopText("Components", "Pages", "UiLab.razor");

        Assert.Contains("@page \"/diagnostic\"", diagnostic, StringComparison.Ordinal);
        Assert.Contains("@page \"/ui-lab\"", uiLab, StringComparison.Ordinal);
    }

    private static DemoPosSession CreatePaymentSession()
    {
        var session = new DemoPosSession();
        session.AddItem(DemoCatalog.Items[0].Id);
        Assert.True(session.BeginPayment());
        return session;
    }

    private static DemoPosSession CreateReadyCashSession()
    {
        var session = CreatePaymentSession();
        session.SelectPaymentMethod(DemoPaymentMethod.Cash);
        session.SetReceivedAmount(session.Total + 10m);
        return session;
    }

    private static string DesktopText(params string[] segments)
    {
        return File.ReadAllText(DesktopPath(segments));
    }

    private static string DesktopPath(params string[] segments)
    {
        return Path.Combine(
            new[] { RepositoryRoot, "src", "Pharmacy.Desktop" }.Concat(segments).ToArray());
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
