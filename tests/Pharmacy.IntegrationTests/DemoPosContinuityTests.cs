using System.Text.RegularExpressions;
using System.Xml.Linq;
using Pharmacy.Desktop.Components.Pos;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class DemoPosContinuityTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void UnknownCode_DoesNotCreateAnItemOrLine()
    {
        var session = new DemoPosSession();

        var result = session.TryAddByExactCode("DEMO-CODE-INCONNU");

        Assert.Equal(DemoCartMutationResult.NotFound, result);
        Assert.Empty(session.Lines);
        Assert.Equal(10, session.Catalog.Count);
    }

    [Fact]
    public void UnknownCode_LeavesAnExistingCartUnchanged()
    {
        var session = new DemoPosSession();
        session.AddItem(DemoCatalog.Items[0].Id);
        var total = session.Total;

        session.TryAddByExactCode("DEMO-CODE-INCONNU");

        Assert.Single(session.Lines);
        Assert.Equal(1, session.Lines[0].Quantity);
        Assert.Equal(total, session.Total);
    }

    [Fact]
    public void Availability_RefusesAQuantityAboveTheDisplayedMaximum()
    {
        var session = FillToAvailability(DemoCatalog.Items[4]);

        var result = session.AddItem(DemoCatalog.Items[4].Id);

        Assert.Equal(DemoCartMutationResult.AvailabilityReached, result);
    }

    [Fact]
    public void AvailabilityRefusal_PreservesThePreviousQuantity()
    {
        var item = DemoCatalog.Items[4];
        var session = FillToAvailability(item);

        session.Increase(item.Id);

        Assert.Equal(item.AvailableQuantity, Assert.Single(session.Lines).Quantity);
    }

    [Fact]
    public void AvailabilityRefusal_PreservesAllCartTotals()
    {
        var limitedItem = DemoCatalog.Items[4];
        var session = FillToAvailability(limitedItem);
        session.AddItem(DemoCatalog.Items[0].Id);
        var total = session.Total;
        var quantity = session.TotalQuantity;

        session.AddItem(limitedItem.Id);

        Assert.Equal(total, session.Total);
        Assert.Equal(quantity, session.TotalQuantity);
    }

    [Fact]
    public void Suspension_RejectsAnEmptyCart()
    {
        var session = new DemoPosSession();

        Assert.Null(session.SuspendCurrent());
        Assert.Empty(session.SuspendedSales);
    }

    [Fact]
    public void Suspension_CreatesUniqueInMemoryReferences()
    {
        var session = new DemoPosSession();
        session.AddItem(DemoCatalog.Items[0].Id);
        var first = session.SuspendCurrent();
        session.AddItem(DemoCatalog.Items[1].Id);
        var second = session.SuspendCurrent();

        Assert.Equal("DEMO-HOLD-0001", first?.Reference);
        Assert.Equal("DEMO-HOLD-0002", second?.Reference);
        Assert.NotEqual(first?.Reference, second?.Reference);
    }

    [Fact]
    public void Suspension_PreservesLinesAndQuantitiesAsSnapshots()
    {
        var session = new DemoPosSession();
        var item = DemoCatalog.Items[1];
        session.AddItem(item.Id);
        session.Increase(item.Id);

        var suspended = Assert.IsType<DemoSuspendedSale>(session.SuspendCurrent());

        var line = Assert.Single(suspended.Lines);
        Assert.Equal(item, line.Item);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public void Suspension_PreservesTheCartTotal()
    {
        var session = new DemoPosSession();
        session.AddItem(DemoCatalog.Items[0].Id);
        session.AddItem(DemoCatalog.Items[1].Id);
        var total = session.Total;

        var suspended = Assert.IsType<DemoSuspendedSale>(session.SuspendCurrent());

        Assert.Equal(total, suspended.Total);
    }

    [Fact]
    public void Suspension_ResetsTheCurrentPos()
    {
        var session = CreatePaymentSession();
        session.SelectPaymentMethod(DemoPaymentMethod.Cash);
        session.SetReceivedAmount(session.Total);

        session.SuspendCurrent();

        Assert.Empty(session.Lines);
        Assert.Equal(0m, session.Total);
        Assert.Equal(DemoPosStep.Shopping, session.Step);
        Assert.Equal(DemoPaymentMethod.None, session.PaymentMethod);
        Assert.Null(session.ReceivedAmount);
    }

    [Fact]
    public void Suspension_DoesNotRetainAConfirmedPayment()
    {
        var session = CreatePaymentSession();
        session.SelectPaymentMethod(DemoPaymentMethod.ExternalCard);
        session.ConfirmExternalTerminal(true);

        var suspended = Assert.IsType<DemoSuspendedSale>(session.SuspendCurrent());

        Assert.Null(typeof(DemoSuspendedSale).GetProperty("PaymentMethod"));
        Assert.NotEmpty(suspended.Lines);
        Assert.Equal(DemoPaymentMethod.None, session.PaymentMethod);
        Assert.Equal(DemoCardPaymentStatus.NotStarted, session.CardPaymentStatus);
    }

    [Fact]
    public void Suspension_KeepsSeveralSuspensionsDistinct()
    {
        var session = CreateTwoSuspensions();

        Assert.Equal(2, session.SuspendedSales.Count);
        Assert.Equal(2, session.SuspendedSales.Select(sale => sale.Reference).Distinct().Count());
        Assert.NotEqual(session.SuspendedSales[0].Total, session.SuspendedSales[1].Total);
    }

    [Fact]
    public void Resume_RestoresLinesAndQuantities()
    {
        var session = new DemoPosSession();
        var item = DemoCatalog.Items[2];
        session.AddItem(item.Id);
        session.Increase(item.Id);
        var reference = session.SuspendCurrent()!.Reference;

        var result = session.ResumeSuspended(reference);

        Assert.Equal(DemoResumeResult.Resumed, result);
        var line = Assert.Single(session.Lines);
        Assert.Equal(item.Id, line.Item.Id);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public void Resume_RemovesTheRestoredEntryFromTheList()
    {
        var session = new DemoPosSession();
        session.AddItem(DemoCatalog.Items[0].Id);
        var reference = session.SuspendCurrent()!.Reference;

        session.ResumeSuspended(reference);

        Assert.Empty(session.SuspendedSales);
    }

    [Fact]
    public void Resume_NeverOverwritesANonEmptyCurrentCart()
    {
        var session = new DemoPosSession();
        var suspendedItem = DemoCatalog.Items[0];
        var currentItem = DemoCatalog.Items[1];
        session.AddItem(suspendedItem.Id);
        var reference = session.SuspendCurrent()!.Reference;
        session.AddItem(currentItem.Id);

        var result = session.ResumeSuspended(reference);

        Assert.Equal(DemoResumeResult.CurrentCartNotEmpty, result);
        Assert.Equal(currentItem.Id, Assert.Single(session.Lines).Item.Id);
        Assert.Single(session.SuspendedSales);
    }

    [Fact]
    public void Resume_AfterSuspendingCurrentCart_PreservesBothStates()
    {
        var session = new DemoPosSession();
        var restoredItem = DemoCatalog.Items[0];
        var newlySuspendedItem = DemoCatalog.Items[1];
        session.AddItem(restoredItem.Id);
        var reference = session.SuspendCurrent()!.Reference;
        session.AddItem(newlySuspendedItem.Id);

        var result = session.SuspendCurrentThenResume(reference);

        Assert.Equal(DemoResumeResult.Resumed, result);
        Assert.Equal(restoredItem.Id, Assert.Single(session.Lines).Item.Id);
        var remaining = Assert.Single(session.SuspendedSales);
        Assert.Equal(newlySuspendedItem.Id, Assert.Single(remaining.Lines).Item.Id);
    }

    [Fact]
    public void DeleteSuspended_DoesNotAffectOtherSuspensions()
    {
        var session = CreateTwoSuspensions();
        var deletedReference = session.SuspendedSales[0].Reference;
        var retainedReference = session.SuspendedSales[1].Reference;

        Assert.True(session.DeleteSuspended(deletedReference));

        var remaining = Assert.Single(session.SuspendedSales);
        Assert.Equal(retainedReference, remaining.Reference);
    }

    [Fact]
    public void Abandon_ResetsTheDraftToZero()
    {
        var session = CreatePaymentSession();

        Assert.True(session.AbandonDraft());

        Assert.Empty(session.Lines);
        Assert.Equal(0m, session.Total);
        Assert.Equal(DemoPosStep.Shopping, session.Step);
        Assert.Equal(DemoPaymentMethod.None, session.PaymentMethod);
    }

    [Fact]
    public void Abandon_DoesNotProduceACompletedSale()
    {
        var session = CreatePaymentSession();

        session.AbandonDraft();

        Assert.Null(session.Result);
        Assert.NotEqual(DemoPosStep.Success, session.Step);
    }

    [Fact]
    public void Abandon_DoesNotRetainASuccessResult()
    {
        var session = CreatePaymentSession();
        session.SelectPaymentMethod(DemoPaymentMethod.Cash);
        session.SetReceivedAmount(session.Total);

        session.AbandonDraft();

        Assert.Null(session.Result);
        Assert.False(session.IsCompletionInProgress);
    }

    [Fact]
    public void AmbiguousCardPayment_BlocksSaleValidation()
    {
        var session = CreateAmbiguousCardSession();

        Assert.False(session.CanComplete);
        Assert.False(session.TryBeginCompletion());
    }

    [Fact]
    public void AmbiguousCardPayment_ExplicitConfirmedResolutionAllowsValidation()
    {
        var session = CreateAmbiguousCardSession();

        Assert.True(session.ResolveAmbiguousCardAsConfirmed());

        Assert.Equal(DemoCardPaymentStatus.Confirmed, session.CardPaymentStatus);
        Assert.True(session.CanComplete);
    }

    [Fact]
    public void NotCompletedCardPayment_DoesNotCompleteTheSale()
    {
        var session = CreatePaymentSession();
        session.SelectPaymentMethod(DemoPaymentMethod.ExternalCard);
        session.StartExternalCardVerification();

        Assert.True(session.ReportExternalCardNotCompleted());

        Assert.False(session.CanComplete);
        Assert.False(session.TryBeginCompletion());
        Assert.Null(session.Result);
    }

    [Fact]
    public void PosModels_DoNotContainBankCardDataFields()
    {
        var sources = PosSources();
        var prohibited = new[]
        {
            "CardNumber", "NumeroCarte", "NuméroCarte", "CVV", "CardHolder",
            "TitulaireCarte", "ExpirationDate", "BankAccount"
        };

        Assert.All(prohibited, token =>
            Assert.DoesNotContain(sources, source => source.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void AmbiguousCardPayment_DoesNotLaunchASecondAutomaticAttempt()
    {
        var session = CreateAmbiguousCardSession();

        Assert.False(session.StartExternalCardVerification());

        Assert.Equal(1, session.CardVerificationCount);
        Assert.Equal(DemoCardPaymentStatus.Ambiguous, session.CardPaymentStatus);
    }

    [Fact]
    public void PrintFailure_DoesNotRemoveTheCompletedSale()
    {
        var session = CreateCompletedSession();

        FailPrint(session);

        Assert.Equal(DemoPosStep.Success, session.Step);
        Assert.NotNull(session.Result);
        Assert.Equal(DemoPrintStatus.Failed, session.PrintStatus);
    }

    [Fact]
    public void PrintFailure_PreservesSaleReferenceAndTotal()
    {
        var session = CreateCompletedSession();
        var reference = session.Result!.Reference;
        var total = session.Result.Total;

        FailPrint(session);

        Assert.Equal(reference, session.Result.Reference);
        Assert.Equal(total, session.Result.Total);
    }

    [Fact]
    public void Reprint_DoesNotRecreateTheCompletedSale()
    {
        var session = CreateCompletedSession();
        var result = session.Result;
        FailPrint(session);

        Assert.True(session.TryBeginTicketPrint());
        Assert.True(session.CompleteTicketPrint(DemoPrintSimulationOutcome.Success));

        Assert.Same(result, session.Result);
        Assert.Equal("DEMO-SALE-0001", session.Result!.Reference);
    }

    [Fact]
    public void Reprint_IncrementsTheAttemptCounter()
    {
        var session = CreateCompletedSession();
        FailPrint(session);

        session.TryBeginTicketPrint();
        session.CompleteTicketPrint(DemoPrintSimulationOutcome.Failure);

        Assert.Equal(2, session.PrintAttemptCount);
        Assert.NotNull(session.LastPrintAttemptAtLocal);
    }

    [Fact]
    public void Print_BlocksASecondConcurrentStart()
    {
        var session = CreateCompletedSession();

        Assert.True(session.TryBeginTicketPrint());
        Assert.False(session.TryBeginTicketPrint());
        Assert.Equal(1, session.PrintAttemptCount);
    }

    [Fact]
    public void NewSale_ResetsEveryPrintState()
    {
        var session = CreateCompletedSession();
        FailPrint(session);

        session.ResetForNewSale();

        Assert.Equal(DemoPrintStatus.NotRequested, session.PrintStatus);
        Assert.Equal(0, session.PrintAttemptCount);
        Assert.Null(session.LastPrintAttemptAtLocal);
        Assert.False(session.ContinuedWithoutTicket);
    }

    [Fact]
    public void PosContinuitySource_HasNoSqlOrEfCoreReference()
    {
        var source = string.Join(Environment.NewLine, PosSources());

        Assert.DoesNotContain("SqlConnection", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DbContext", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EntityFramework", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PosContinuitySource_HasNoConnectionString()
    {
        Assert.DoesNotContain(
            PosSources(),
            source => source.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TicketSimulation_HasNoFileGenerationApi()
    {
        var source = DesktopText("Components", "Pos", "DemoTicketPrintService.cs");
        var prohibited = new[] { "System.IO", "File.", "FileStream", "StreamWriter", "Pdf", "Printer" };

        Assert.All(prohibited, token => Assert.DoesNotContain(token, source, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PosContinuitySource_HasNoExternalUrl()
    {
        var source = string.Join(Environment.NewLine, PosSources());

        Assert.DoesNotContain("http://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Solution_HasOnlyTheThreeBaselinePackageReferences()
    {
        var packageReferences = Directory
            .EnumerateFiles(RepositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .SelectMany(path => XDocument.Load(path).Descendants("PackageReference"))
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .ToArray();

        Assert.Equal(3, packageReferences.Length);
        Assert.Equal(2, packageReferences.Count(name => name == "xunit.v3.mtp-v2"));
        Assert.Single(packageReferences, name => name == "Microsoft.AspNetCore.Components.WebView.Wpf");
    }

    [Fact]
    public void ProjectReferences_RemainInsideTheAllowedGraph()
    {
        var desktop = XDocument.Load(DesktopPath("Pharmacy.Desktop.csproj"));
        var integration = XDocument.Load(TestPath("Pharmacy.IntegrationTests", "Pharmacy.IntegrationTests.csproj"));
        var desktopReferences = ProjectReferences(desktop);
        var integrationReferences = ProjectReferences(integration);

        Assert.Equal(new[] { "Pharmacy.Application", "Pharmacy.Infrastructure" }, desktopReferences);
        Assert.Equal(
            new[] { "Pharmacy.Application", "Pharmacy.Domain", "Pharmacy.Infrastructure" },
            integrationReferences);
    }

    [Fact]
    public void DiagnosticAndUiLab_RoutesRemainAccessible()
    {
        Assert.Contains("@page \"/diagnostic\"", DesktopText("Components", "DiagnosticScreen.razor"), StringComparison.Ordinal);
        Assert.Contains("@page \"/ui-lab\"", DesktopText("Components", "Pages", "UiLab.razor"), StringComparison.Ordinal);
    }

    [Fact]
    public void OtherBusinessModules_RemainConfiguredAsBefore()
    {
        var navigation = DesktopText("Components", "Navigation", "AppNavigationCatalog.cs");
        var routes = new[]
        {
            "/module/catalog", "/module/inventory", "/module/purchasing", "/module/cash",
            "/module/customers", "/module/reporting", "/module/administration"
        };

        Assert.All(routes, route => Assert.Contains($"\"{route}\"", navigation, StringComparison.Ordinal));
    }

    [Fact]
    public void SalesRoute_IsDeclaredExactlyOnce()
    {
        var declarationCount = Directory
            .EnumerateFiles(DesktopPath("Components"), "*.razor", SearchOption.AllDirectories)
            .Sum(path => Regex.Matches(
                File.ReadAllText(path),
                "@page\\s+\\\"/module/sales\\\"",
                RegexOptions.CultureInvariant).Count);

        Assert.Equal(1, declarationCount);
    }

    private static DemoPosSession FillToAvailability(DemoCatalogItem item)
    {
        var session = new DemoPosSession();

        for (var quantity = 0; quantity < item.AvailableQuantity; quantity++)
        {
            session.AddItem(item.Id);
        }

        return session;
    }

    private static DemoPosSession CreatePaymentSession()
    {
        var session = new DemoPosSession();
        session.AddItem(DemoCatalog.Items[0].Id);
        Assert.True(session.BeginPayment());
        return session;
    }

    private static DemoPosSession CreateAmbiguousCardSession()
    {
        var session = CreatePaymentSession();
        session.SelectPaymentMethod(DemoPaymentMethod.ExternalCard);
        Assert.True(session.StartExternalCardVerification());
        Assert.True(session.ReportExternalCardAmbiguous());
        return session;
    }

    private static DemoPosSession CreateCompletedSession()
    {
        var session = CreatePaymentSession();
        session.SelectPaymentMethod(DemoPaymentMethod.Cash);
        session.SetReceivedAmount(session.Total);
        Assert.True(session.TryBeginCompletion());
        session.CompleteSale();
        return session;
    }

    private static DemoPosSession CreateTwoSuspensions()
    {
        var session = new DemoPosSession();
        session.AddItem(DemoCatalog.Items[0].Id);
        session.SuspendCurrent();
        session.AddItem(DemoCatalog.Items[1].Id);
        session.SuspendCurrent();
        return session;
    }

    private static void FailPrint(DemoPosSession session)
    {
        Assert.True(session.TryBeginTicketPrint());
        Assert.True(session.CompleteTicketPrint(DemoPrintSimulationOutcome.Failure));
    }

    private static string[] PosSources()
    {
        return Directory
            .EnumerateFiles(DesktopPath("Components", "Pos"), "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText)
            .ToArray();
    }

    private static string[] ProjectReferences(XDocument document)
    {
        return document
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Select(value => Path.GetFileNameWithoutExtension(value!))
            .Order(StringComparer.Ordinal)
            .ToArray();
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

    private static string TestPath(params string[] segments)
    {
        return Path.Combine(new[] { RepositoryRoot, "tests" }.Concat(segments).ToArray());
    }

    private static bool IsGeneratedPath(string path)
    {
        var relativePath = Path.GetRelativePath(RepositoryRoot, path);
        return relativePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("obj", StringComparison.OrdinalIgnoreCase));
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
