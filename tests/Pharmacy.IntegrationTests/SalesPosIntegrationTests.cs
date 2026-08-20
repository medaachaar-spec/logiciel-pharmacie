using System.Reflection;
using System.Xml.Linq;
using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Catalog.Queries;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Commands;
using Pharmacy.Application.Sales.Drafts.Queries;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Finalization.Queries;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Application.Sales.Suspensions;
using Pharmacy.Desktop.Components.Pos;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Sales.InMemory;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class SalesPosIntegrationTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InitializationCreatesOneApplicationDraftAndLoadsInfrastructureCatalog()
    {
        var fixture = Fixture.Create();

        Assert.True(await fixture.Coordinator.InitializeAsync(TestCancellation));

        Assert.NotNull(fixture.Coordinator.CurrentDraft);
        Assert.NotEqual(Guid.Empty, fixture.Coordinator.CurrentDraft.Id.Value);
        Assert.Equal(1, fixture.Coordinator.CurrentDraft.Version);
        Assert.True(fixture.Coordinator.CurrentDraft.IsEmpty);
        Assert.Equal(10, fixture.Coordinator.SearchResults.Count);
        Assert.All(fixture.Coordinator.SearchResults, product =>
            Assert.StartsWith("DEMO-", product.Reference.Value));
    }

    [Fact]
    public async Task RepeatedInitializationKeepsTheSameActiveDraft()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        var initial = fixture.Coordinator.CurrentDraft;

        Assert.True(await fixture.Coordinator.InitializeAsync(TestCancellation));

        Assert.Same(initial, fixture.Coordinator.CurrentDraft);
        Assert.Equal(1, fixture.Coordinator.CurrentDraft!.Version);
    }

    [Fact]
    public async Task SearchIsCaseInsensitiveAndAlwaysUsesApplication()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);

        var matches = await fixture.Coordinator.SearchAsync("hYgIèNe", TestCancellation);

        var product = Assert.Single(matches);
        Assert.Equal("DEMO-000006", product.Reference.Value);
        Assert.InRange(matches.Count, 0, SearchSellableProductsHandler.MaximumResults);
    }

    [Fact]
    public async Task EmptySearchRestoresTheTenItemApplicationCatalog()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        await fixture.Coordinator.SearchAsync("Produit Démo A", TestCancellation);

        var matches = await fixture.Coordinator.SearchAsync(" ", TestCancellation);

        Assert.Equal(10, matches.Count);
    }

    [Fact]
    public async Task ExactKnownCodeAddsTheApplicationProductAndAdvancesVersion()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);

        Assert.True(await fixture.Coordinator.AddExactCodeAsync("demo-000003", TestCancellation));

        var line = Assert.Single(fixture.Coordinator.CurrentDraft!.Lines);
        Assert.Equal(3, line.ProductId.Value);
        Assert.Equal(1, line.Quantity.Value);
        Assert.Equal(2, fixture.Coordinator.CurrentDraft.Version);
    }

    [Fact]
    public async Task UnknownExactCodeLeavesDraftAndCatalogUnchanged()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        var draft = fixture.Coordinator.CurrentDraft;

        Assert.False(await fixture.Coordinator.AddExactCodeAsync("DEMO-UNKNOWN", TestCancellation));

        Assert.Same(draft, fixture.Coordinator.CurrentDraft);
        Assert.True(fixture.Coordinator.CurrentDraft!.IsEmpty);
        Assert.Equal("sales.product.not_found", fixture.Coordinator.LastErrorCode);
        Assert.Equal(10, fixture.Coordinator.SearchResults.Count);
    }

    [Fact]
    public async Task AddAndMergeUseDomainTotalsFromSaleDraftView()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        var productId = ProductId.Create(1).Value;

        await fixture.Coordinator.AddProductAsync(productId, TestCancellation);
        await fixture.Coordinator.AddProductAsync(productId, TestCancellation);

        var line = Assert.Single(fixture.Coordinator.CurrentDraft!.Lines);
        Assert.Equal(2, line.Quantity.Value);
        Assert.Equal(25m, line.Subtotal.Amount);
        Assert.Equal(25m, fixture.Coordinator.Total);
        Assert.Equal(3, fixture.Coordinator.CurrentDraft.Version);
    }

    [Fact]
    public async Task InsufficientAvailabilityPreservesViewVersionAndTotal()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        var productId = ProductId.Create(5).Value;

        for (var quantity = 0; quantity < 4; quantity++)
        {
            Assert.True(await fixture.Coordinator.AddProductAsync(productId, TestCancellation));
        }

        var before = fixture.Coordinator.CurrentDraft;

        Assert.False(await fixture.Coordinator.AddProductAsync(productId, TestCancellation));

        Assert.Same(before, fixture.Coordinator.CurrentDraft);
        Assert.Equal(4, Assert.Single(fixture.Coordinator.CurrentDraft!.Lines).Quantity.Value);
        Assert.Equal("sales.stock.insufficient_availability", fixture.Coordinator.LastErrorCode);
        Assert.Equal(5, fixture.Coordinator.AvailabilityIncident!.RequestedQuantity);
        Assert.Equal(4, fixture.Coordinator.AvailabilityIncident.AvailableQuantity);
    }

    [Fact]
    public async Task QuantityZeroIsRefusedAndNeverRemovesTheLine()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        var productId = ProductId.Create(1).Value;
        await fixture.Coordinator.AddProductAsync(productId, TestCancellation);
        var version = fixture.Coordinator.CurrentDraft!.Version;

        Assert.False(await fixture.Coordinator.ChangeQuantityAsync(productId, 0, TestCancellation));

        Assert.Single(fixture.Coordinator.CurrentDraft.Lines);
        Assert.Equal(version, fixture.Coordinator.CurrentDraft.Version);
        Assert.Equal("sales.quantity.invalid", fixture.Coordinator.LastErrorCode);
    }

    [Fact]
    public async Task ChangeRemoveAndClearUseHandlersAndResetPayment()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        var first = ProductId.Create(1).Value;
        var second = ProductId.Create(2).Value;
        await fixture.Coordinator.AddProductAsync(first, TestCancellation);
        await fixture.Coordinator.AddProductAsync(second, TestCancellation);

        Assert.True(await fixture.Coordinator.ChangeQuantityAsync(first, 3, TestCancellation));
        Assert.True(await fixture.Coordinator.RemoveItemAsync(second, TestCancellation));
        Assert.True(fixture.Coordinator.BeginPayment());
        fixture.Coordinator.SelectPaymentMethod(DemoPaymentMethod.Cash);
        fixture.Coordinator.SetReceivedAmount(fixture.Coordinator.Total);
        Assert.True(await fixture.Coordinator.ClearAsync(TestCancellation));

        Assert.True(fixture.Coordinator.CurrentDraft!.IsEmpty);
        Assert.Equal(0m, fixture.Coordinator.Total);
        Assert.Equal(DemoPaymentMethod.None, fixture.Coordinator.PaymentMethod);
        Assert.Equal(DemoPosStep.Shopping, fixture.Coordinator.Step);
    }

    [Fact]
    public async Task ConcurrentDoubleMutationIsBlockedWhileFirstHandlerIsPending()
    {
        var stock = new BlockingStockReader(new InMemoryStockAvailabilityReader());
        var fixture = Fixture.Create(stock);
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        stock.BlockNextRequest();
        var productId = ProductId.Create(1).Value;

        var first = fixture.Coordinator.AddProductAsync(productId, TestCancellation);
        await stock.WaitUntilBlockedAsync(TestCancellation);
        var second = await fixture.Coordinator.AddProductAsync(productId, TestCancellation);
        stock.Release();

        Assert.True(await first);
        Assert.False(second);
        Assert.Equal(1, Assert.Single(fixture.Coordinator.CurrentDraft!.Lines).Quantity.Value);
    }

    [Fact]
    public async Task StaleVersionReloadsLatestDraftWithoutRetryingAction()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        await fixture.Coordinator.AddProductAsync(ProductId.Create(1).Value, TestCancellation);
        var staleVersion = fixture.Coordinator.CurrentDraft!.Version;
        var externalHandler = new AddProductToSaleDraftHandler(
            fixture.Store,
            fixture.Catalog,
            fixture.Stock);
        var external = await externalHandler.HandleAsync(
            new AddProductToSaleDraftCommand(
                fixture.Coordinator.CurrentDraft.Id,
                SellableProductLookup.ByProductId(ProductId.Create(2).Value),
                SaleQuantity.One,
                staleVersion),
            TestCancellation);
        Assert.True(external.IsSuccess);

        Assert.False(await fixture.Coordinator.AddProductAsync(ProductId.Create(3).Value, TestCancellation));

        Assert.Equal("sales.draft.concurrency_conflict", fixture.Coordinator.LastErrorCode);
        Assert.Equal(SalesPosCoordinator.ConcurrencyReloadMessage, fixture.Coordinator.UserMessage);
        Assert.Equal(external.Value.Version, fixture.Coordinator.CurrentDraft.Version);
        Assert.Contains(fixture.Coordinator.CurrentDraft.Lines, line => line.ProductId.Value == 2);
        Assert.DoesNotContain(fixture.Coordinator.CurrentDraft.Lines, line => line.ProductId.Value == 3);
    }

    [Fact]
    public async Task SuspensionCapturesImmutableApplicationSnapshotAndStartsNewDraft()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        await fixture.Coordinator.AddProductAsync(ProductId.Create(1).Value, TestCancellation);
        var suspendedDraftId = fixture.Coordinator.CurrentDraft!.Id;

        Assert.True(await fixture.Coordinator.SuspendCurrentDraftAsync(TestCancellation));

        var suspension = Assert.Single(fixture.Coordinator.SuspendedDrafts);
        Assert.StartsWith("HOLD-TEST-", suspension.Reference, StringComparison.Ordinal);
        Assert.Equal(12.50m, suspension.Total.Amount);
        Assert.Single(suspension.Lines);
        Assert.NotEqual(suspendedDraftId, fixture.Coordinator.CurrentDraft!.Id);
        Assert.True(fixture.Coordinator.CurrentDraft.IsEmpty);

        await fixture.Coordinator.AddProductAsync(ProductId.Create(2).Value, TestCancellation);
        Assert.Equal(1, Assert.Single(suspension.Lines).ProductId.Value);
        Assert.Equal(12.50m, suspension.Total.Amount);
    }

    [Fact]
    public async Task SeveralSuspensionsKeepDistinctReferencesAndSnapshots()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        await fixture.Coordinator.AddProductAsync(ProductId.Create(1).Value, TestCancellation);
        await fixture.Coordinator.SuspendCurrentDraftAsync(TestCancellation);
        await fixture.Coordinator.AddProductAsync(ProductId.Create(2).Value, TestCancellation);
        await fixture.Coordinator.SuspendCurrentDraftAsync(TestCancellation);

        Assert.Equal(2, fixture.Coordinator.SuspendedDrafts.Count);
        Assert.Equal(2, fixture.Coordinator.SuspendedDrafts.Select(item => item.Reference).Distinct().Count());
        Assert.NotEqual(
            fixture.Coordinator.SuspendedDrafts[0].Total,
            fixture.Coordinator.SuspendedDrafts[1].Total);
    }

    [Fact]
    public async Task ResumeRebuildsThroughHandlersAndRemovesSnapshotOnlyAfterSuccess()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        await fixture.Coordinator.AddProductAsync(ProductId.Create(1).Value, TestCancellation);
        await fixture.Coordinator.ChangeQuantityAsync(ProductId.Create(1).Value, 2, TestCancellation);
        await fixture.Coordinator.AddProductAsync(ProductId.Create(2).Value, TestCancellation);
        var total = fixture.Coordinator.Total;
        await fixture.Coordinator.SuspendCurrentDraftAsync(TestCancellation);
        var reference = Assert.Single(fixture.Coordinator.SuspendedDrafts).Reference;
        var emptyDraftId = fixture.Coordinator.CurrentDraft!.Id;

        Assert.True(await fixture.Coordinator.ResumeSuspendedDraftAsync(reference, TestCancellation));

        Assert.Empty(fixture.Coordinator.SuspendedDrafts);
        Assert.NotEqual(emptyDraftId, fixture.Coordinator.CurrentDraft!.Id);
        Assert.Equal(2, fixture.Coordinator.CurrentDraft.LineCount);
        Assert.Equal(total, fixture.Coordinator.Total);
    }

    [Fact]
    public async Task ResumeRestoresSnapshotEvenWhenCurrentStockBecameUnavailable()
    {
        var stock = new SwitchableStockReader(new InMemoryStockAvailabilityReader());
        var fixture = Fixture.Create(stock);
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        await fixture.Coordinator.AddProductAsync(ProductId.Create(1).Value, TestCancellation);
        await fixture.Coordinator.SuspendCurrentDraftAsync(TestCancellation);
        var reference = Assert.Single(fixture.Coordinator.SuspendedDrafts).Reference;
        stock.DeniedProductId = ProductId.Create(1).Value;

        Assert.True(await fixture.Coordinator.ResumeSuspendedDraftAsync(reference, TestCancellation));

        Assert.Empty(fixture.Coordinator.SuspendedDrafts);
        Assert.Equal(1, Assert.Single(fixture.Coordinator.CurrentDraft!.Lines).Quantity.Value);
    }

    [Fact]
    public async Task ResumeNeverOverwritesANonEmptyCurrentDraft()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        await fixture.Coordinator.AddProductAsync(ProductId.Create(1).Value, TestCancellation);
        await fixture.Coordinator.SuspendCurrentDraftAsync(TestCancellation);
        var reference = Assert.Single(fixture.Coordinator.SuspendedDrafts).Reference;
        await fixture.Coordinator.AddProductAsync(ProductId.Create(2).Value, TestCancellation);
        var current = fixture.Coordinator.CurrentDraft;

        Assert.False(await fixture.Coordinator.ResumeSuspendedDraftAsync(reference, TestCancellation));

        Assert.Same(current, fixture.Coordinator.CurrentDraft);
        Assert.Single(fixture.Coordinator.SuspendedDrafts);
    }

    [Fact]
    public async Task DoubleResumeIsBlockedUntilFirstRestorationFinishes()
    {
        var suspendedStore = new BlockingSuspendedSaleStore(new StubSuspendedSaleStore());
        var fixture = Fixture.Create(suspendedStore: suspendedStore);
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        await fixture.Coordinator.AddProductAsync(ProductId.Create(1).Value, TestCancellation);
        await fixture.Coordinator.SuspendCurrentDraftAsync(TestCancellation);
        var reference = Assert.Single(fixture.Coordinator.SuspendedDrafts).Reference;
        suspendedStore.BlockNextGetOpen();

        var first = fixture.Coordinator.ResumeSuspendedDraftAsync(reference, TestCancellation);
        await suspendedStore.WaitUntilBlockedAsync(TestCancellation);
        var second = await fixture.Coordinator.ResumeSuspendedDraftAsync(reference, TestCancellation);
        suspendedStore.Release();

        Assert.True(await first);
        Assert.False(second);
        Assert.Empty(fixture.Coordinator.SuspendedDrafts);
    }

    [Fact]
    public async Task PaymentAndCompletedResultReadApplicationSnapshotTotalsAndLines()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        await fixture.Coordinator.AddProductAsync(ProductId.Create(1).Value, TestCancellation);
        await fixture.Coordinator.AddProductAsync(ProductId.Create(2).Value, TestCancellation);
        var view = fixture.Coordinator.CurrentDraft;

        Assert.True(fixture.Coordinator.BeginPayment());
        fixture.Coordinator.SelectPaymentMethod(DemoPaymentMethod.Cash);
        fixture.Coordinator.SetReceivedAmount(fixture.Coordinator.Total + 5m);
        Assert.True(fixture.Coordinator.TryBeginCompletion());
        Assert.True(await fixture.Coordinator.FinalizeSaleAsync(TestCancellation));
        var completed = Assert.IsType<SalesPosCompletedSale>(fixture.Coordinator.Result);

        Assert.Equal(view!.Total, completed.Total);
        Assert.Equal(view.TotalQuantity, completed.ItemCount);
        Assert.Equal(view.LineCount, completed.LineCount);
        Assert.Equal(view.Lines.Select(line => line.ProductId), completed.Lines.Select(line => line.ProductId));
        Assert.Equal(5m, completed.Change);
        Assert.StartsWith("VTE-", completed.Reference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrintingRemainsIndependentSimulationAndNewSaleResetsEverything()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        await fixture.Coordinator.AddProductAsync(ProductId.Create(1).Value, TestCancellation);
        fixture.Coordinator.BeginPayment();
        fixture.Coordinator.SelectPaymentMethod(DemoPaymentMethod.Cash);
        fixture.Coordinator.SetReceivedAmount(fixture.Coordinator.Total);
        var oldDraftId = fixture.Coordinator.CurrentDraft!.Id;
        fixture.Coordinator.TryBeginCompletion();
        Assert.True(await fixture.Coordinator.FinalizeSaleAsync(TestCancellation));
        var preparedDraftId = fixture.Coordinator.CurrentDraft!.Id;
        Assert.NotEqual(oldDraftId, preparedDraftId);
        Assert.True(fixture.Coordinator.TryBeginTicketPrint());
        Assert.True(fixture.Coordinator.CompleteTicketPrint(DemoPrintSimulationOutcome.Failure));

        Assert.True(await fixture.Coordinator.StartNewDraftAsync(TestCancellation));

        Assert.Equal(preparedDraftId, fixture.Coordinator.CurrentDraft!.Id);
        Assert.True(fixture.Coordinator.CurrentDraft.IsEmpty);
        Assert.Null(fixture.Coordinator.Result);
        Assert.Equal(DemoPaymentMethod.None, fixture.Coordinator.PaymentMethod);
        Assert.Equal(DemoPrintStatus.NotRequested, fixture.Coordinator.PrintStatus);
    }

    [Fact]
    public void CoordinatorUsesApplicationFinalizationHandlerWithoutInfrastructureContractAccess()
    {
        var dependencies = typeof(SalesPosCoordinator)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.DoesNotContain(typeof(ISaleFinalizer), dependencies);
        Assert.DoesNotContain(typeof(Pharmacy.Application.Sales.Finalization.Commands.PrepareSaleFinalizationHandler), dependencies);
        Assert.Contains(typeof(FinalizeSaleHandler), dependencies);
    }

    [Fact]
    public void CompositionRootRegistersSqlCatalogMemoryDraftsAndAllHandlers()
    {
        var source = DesktopText("SalesServiceCollectionExtensions.cs");

        Assert.Contains("AddSqlCatalogAdapters(databaseEndpoint)", source, StringComparison.Ordinal);
        Assert.Contains("ISaleDraftStore, InMemorySaleDraftStore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InMemoryProductCatalogReader", source, StringComparison.Ordinal);
        Assert.Contains("AddTransient<SearchSellableProductsHandler>", source, StringComparison.Ordinal);
        Assert.Contains("AddTransient<SearchCatalogProductsHandler>", source, StringComparison.Ordinal);
        Assert.Contains("AddTransient<PrepareSaleFinalizationHandler>", source, StringComparison.Ordinal);
        Assert.Contains("AddSqlSalesAdapters", source, StringComparison.Ordinal);
        Assert.Contains("AddTransient<FinalizeSaleHandler>", source, StringComparison.Ordinal);
        Assert.Contains("AddTransient<GetRecentSalesHandler>", source, StringComparison.Ordinal);
        Assert.Contains("AddScoped<SalesPosCoordinator>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton<ISaleFinalizer", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RazorUsesCoordinatorAndNeverConstructsInfrastructureAdapters()
    {
        var razorSources = Directory
            .EnumerateFiles(DesktopPath("Components"), "*.razor", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();
        var page = DesktopText("Components", "Pages", "DemoPosPage.razor");

        Assert.Contains("@inject SalesPosCoordinator", page, StringComparison.Ordinal);
        Assert.Contains("Nouvelle vente", page, StringComparison.Ordinal);
        Assert.Contains("Suspendre", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Sprint", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OnAbandon=\"RequestAbandonSuspendedAsync\"", page, StringComparison.Ordinal);
        Assert.Contains("SuspendedSaleWorkflow", File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "Pharmacy.Desktop",
            "Components",
            "Pos",
            "SalesPosCoordinator.cs")), StringComparison.Ordinal);
        Assert.All(razorSources, source =>
        {
            Assert.DoesNotContain("new InMemory", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Pharmacy.Infrastructure.Sales", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DemoCatalog.Items", source, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void PosIntegrationKeepsAsyncKeyboardAndResponsiveAccessibilityGuards()
    {
        var page = DesktopText("Components", "Pages", "DemoPosPage.razor");
        var cart = DesktopText("Components", "Pos", "DemoCartPanel.razor");
        var css = DesktopText("wwwroot", "css", "pos.css");

        Assert.Contains("@onkeydown=\"HandleSearchKeyDownAsync\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-busy", page, StringComparison.Ordinal);
        Assert.Contains("role=\"alertdialog\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Diminuer la quantité", cart, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 72rem)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 48rem)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 36rem)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void IntegrationAddsNoSqlPackageProjectReferenceOrBlockingAsyncCall()
    {
        var desktopSources = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(DesktopPath(), "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));
        var prohibited = new[]
        {
            "DbContext", "EntityFramework", "SqlConnection", "ConnectionString",
            "GetAwaiter().GetResult", ".Wait(", "Task.Run(", "ISaleFinalizer."
        };

        Assert.All(prohibited, token =>
            Assert.DoesNotContain(token, desktopSources, StringComparison.OrdinalIgnoreCase));

        var desktopProject = XDocument.Load(DesktopPath("Pharmacy.Desktop.csproj"));
        Assert.Single(desktopProject.Descendants("PackageReference"));
        Assert.Equal(2, desktopProject.Descendants("ProjectReference").Count());
    }

    [Fact]
    public void DiagnosticUiLabAndOtherBusinessModulesRemainAccessible()
    {
        Assert.Contains("@page \"/diagnostic\"", DesktopText("Components", "DiagnosticScreen.razor"), StringComparison.Ordinal);
        Assert.Contains("@page \"/ui-lab\"", DesktopText("Components", "Pages", "UiLab.razor"), StringComparison.Ordinal);
        var navigation = DesktopText("Components", "Navigation", "AppNavigationCatalog.cs");
        var routes = new[]
        {
            "/module/catalog", "/module/inventory", "/module/purchasing", "/module/cash",
            "/module/customers", "/module/reporting", "/module/administration"
        };
        Assert.All(routes, route => Assert.Contains(route, navigation, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RET001_03_NewCoordinatorAndServicesReloadTheSqlProjection()
    {
        var store = new StubSuspendedSaleStore();
        var firstProcess = Fixture.Create(suspendedStore: store);
        await firstProcess.Coordinator.InitializeAsync(TestCancellation);
        await firstProcess.Coordinator.AddProductAsync(ProductId.Create(1).Value, TestCancellation);
        Assert.True(await firstProcess.Coordinator.SuspendCurrentDraftAsync(TestCancellation));
        var reference = Assert.Single(firstProcess.Coordinator.SuspendedDrafts).Reference;

        var restartedProcess = Fixture.Create(suspendedStore: store);
        Assert.True(await restartedProcess.Coordinator.InitializeAsync(TestCancellation));

        Assert.Equal(reference, Assert.Single(restartedProcess.Coordinator.SuspendedDrafts).Reference);
    }

    [Fact]
    public async Task RET001_07_08_FinalizingResumedDraftClosesSuspensionAndCreatesOneLogicalSale()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        await fixture.Coordinator.AddProductAsync(ProductId.Create(1).Value, TestCancellation);
        await fixture.Coordinator.SuspendCurrentDraftAsync(TestCancellation);
        var reference = Assert.Single(fixture.Coordinator.SuspendedDrafts).Reference;
        Assert.True(await fixture.Coordinator.ResumeSuspendedDraftAsync(reference, TestCancellation));
        Assert.True(fixture.Coordinator.BeginPayment());
        fixture.Coordinator.SelectPaymentMethod(DemoPaymentMethod.Cash);
        Assert.True(fixture.Coordinator.SetReceivedAmount(fixture.Coordinator.Total));
        Assert.True(fixture.Coordinator.TryBeginCompletion());

        Assert.True(await fixture.Coordinator.FinalizeSaleAsync(TestCancellation));
        Assert.True(await fixture.Coordinator.LoadSuspendedSalesAsync(TestCancellation));

        Assert.Empty(fixture.Coordinator.SuspendedDrafts);
        Assert.Single(fixture.Coordinator.RecentSales);
    }

    [Fact]
    public async Task RET001_ResuspendingAnActiveSuspensionCannotCreateADuplicate()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        await fixture.Coordinator.AddProductAsync(ProductId.Create(1).Value, TestCancellation);
        await fixture.Coordinator.SuspendCurrentDraftAsync(TestCancellation);
        var reference = Assert.Single(fixture.Coordinator.SuspendedDrafts).Reference;
        Assert.True(await fixture.Coordinator.ResumeSuspendedDraftAsync(reference, TestCancellation));

        Assert.False(fixture.Coordinator.CanSuspend);
        Assert.False(await fixture.Coordinator.SuspendCurrentDraftAsync(TestCancellation));
        Assert.Single((await fixture.SuspendedStore.GetOpenAsync(TestCancellation)).Value);
    }

    [Fact]
    public async Task RET001_09_SameSuspendOperationCreatesOneLogicalSuspension()
    {
        var store = new StubSuspendedSaleStore();
        var request = SuspensionRequest(Guid.NewGuid(), quantity: 2, unitPrice: 12.50m);

        var first = await store.SuspendAsync(request, TestCancellation);
        var replay = await store.SuspendAsync(request, TestCancellation);

        Assert.Equal(first.Value.SuspendedSaleId, replay.Value.SuspendedSaleId);
        Assert.Single((await store.GetOpenAsync(TestCancellation)).Value);
    }

    [Fact]
    public async Task RET001_10_AbandonIsTraceableAndStaleSecondAttemptIsRejectedSafely()
    {
        var store = new StubSuspendedSaleStore();
        var suspended = await store.SuspendAsync(SuspensionRequest(Guid.NewGuid()), TestCancellation);

        Assert.True((await store.AbandonAsync(
            Guid.NewGuid(), suspended.Value.SuspendedSaleId, suspended.Value.RowVersion, TestCancellation)).IsSuccess);
        var stale = await store.AbandonAsync(
            Guid.NewGuid(), suspended.Value.SuspendedSaleId, suspended.Value.RowVersion, TestCancellation);

        Assert.Equal("sales.operation.conflict", stale.Error.Code);
        Assert.Empty((await store.GetOpenAsync(TestCancellation)).Value);
    }

    [Fact]
    public async Task RET001_12_RestoreUsesPersistedPriceSnapshotWithoutCatalogOrStockLookup()
    {
        var store = new StubSuspendedSaleStore();
        var suspended = await store.SuspendAsync(
            SuspensionRequest(Guid.NewGuid(), quantity: 3, unitPrice: 9.75m),
            TestCancellation);
        var drafts = new InMemorySaleDraftStore();

        var restored = await new RestoreSuspendedSaleDraftHandler(
            new InMemorySaleDraftIdGenerator(), drafts).HandleAsync(suspended.Value, TestCancellation);

        Assert.True(restored.IsSuccess);
        var line = Assert.Single(restored.Value.Lines);
        Assert.Equal(3, line.Quantity.Value);
        Assert.Equal(9.75m, line.UnitPrice.Amount);
    }

    [Fact]
    public async Task RET001_SqlUnavailableDoesNotCreateALocalFallbackSuspension()
    {
        var fixture = Fixture.Create(suspendedStore: new UnavailableSuspendedSaleStore());

        Assert.False(await fixture.Coordinator.InitializeAsync(TestCancellation));

        Assert.Empty(fixture.Coordinator.SuspendedDrafts);
        Assert.Equal("sales.database.unavailable", fixture.Coordinator.LastErrorCode);
    }

    [Fact]
    public async Task RET001_BackendPermissionDeniesSuspensionBeforeStoreMutation()
    {
        var store = new StubSuspendedSaleStore();
        var workflow = new SuspendedSaleWorkflow(store, new DenyPermissionAuthorizer());

        var result = await workflow.SuspendAsync(SuspensionRequest(Guid.NewGuid()), TestCancellation);

        Assert.Equal("security.authorization.denied", result.Error.Code);
        Assert.Empty((await store.GetOpenAsync(TestCancellation)).Value);
    }

    [Fact]
    public async Task RET001_SuspendAndAbandonRecordBusinessAuditEvents()
    {
        var store = new StubSuspendedSaleStore();
        var audit = new RecordingBusinessAudit();
        var workflow = new SuspendedSaleWorkflow(store, audit: audit);
        var suspended = await workflow.SuspendAsync(SuspensionRequest(Guid.NewGuid()), TestCancellation);

        Assert.True((await workflow.AbandonAsync(
            Guid.NewGuid(),
            suspended.Value.SuspendedSaleId,
            suspended.Value.RowVersion,
            TestCancellation)).IsSuccess);

        Assert.Equal(new[] { "sale.suspend", "sale.abandon" }, audit.Actions);
    }

    private static SuspendSaleRequest SuspensionRequest(
        Guid operationId,
        int quantity = 1,
        decimal unitPrice = 12.50m) =>
        new(
            operationId,
            null,
            new[]
            {
                new SuspendSaleLineRequest(
                    1,
                    "DEMO-000001",
                    "Produit Démo A",
                    "Boîte",
                    unitPrice,
                    quantity)
            });

    private static int CountOccurrences(string source, string value)
    {
        return source.Split(value, StringSplitOptions.None).Length - 1;
    }

    private static string DesktopText(params string[] segments)
    {
        return File.ReadAllText(DesktopPath(segments));
    }

    private static string DesktopPath(params string[] segments)
    {
        return Path.Combine(new[] { RepositoryRoot, "src", "Pharmacy.Desktop" }.Concat(segments).ToArray());
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

    private sealed class Fixture
    {
        private Fixture(
            InMemorySaleDraftStore store,
            IProductCatalogReader catalog,
            IStockAvailabilityReader stock,
            ISuspendedSaleStore suspendedStore,
            SalesPosCoordinator coordinator)
        {
            Store = store;
            Catalog = catalog;
            Stock = stock;
            SuspendedStore = suspendedStore;
            Coordinator = coordinator;
        }

        public InMemorySaleDraftStore Store { get; }

        public IProductCatalogReader Catalog { get; }

        public IStockAvailabilityReader Stock { get; }

        public ISuspendedSaleStore SuspendedStore { get; }

        public SalesPosCoordinator Coordinator { get; }

        public static Fixture Create(
            IStockAvailabilityReader? stock = null,
            ISuspendedSaleStore? suspendedStore = null)
        {
            var store = new InMemorySaleDraftStore();
            var catalog = new InMemoryProductCatalogReader();
            var availability = stock ?? new InMemoryStockAvailabilityReader();
            var suspensionPersistence = suspendedStore ?? new StubSuspendedSaleStore();
            var persistence = new StubSalesPersistence(suspensionPersistence);
            var preparation = new PrepareSaleFinalizationHandler(
                store,
                availability,
                new InMemorySaleFinalizationPreparationStore());
            var coordinator = new SalesPosCoordinator(
                new CreateSaleDraftHandler(new InMemorySaleDraftIdGenerator(), store),
                new SearchSellableProductsHandler(catalog, availability),
                new GetSellableProductByCodeHandler(catalog, availability),
                new AddProductToSaleDraftHandler(store, catalog, availability),
                new ChangeSaleItemQuantityHandler(store, availability),
                new RemoveSaleItemHandler(store),
                new ClearSaleDraftHandler(store),
                new GetSaleDraftHandler(store),
                new RestoreSuspendedSaleDraftHandler(new InMemorySaleDraftIdGenerator(), store),
                new FinalizeSaleHandler(preparation, persistence),
                new GetRecentSalesHandler(persistence),
                new DemoTicketPrintService(),
                new SuspendedSaleWorkflow(suspensionPersistence));
            return new Fixture(store, catalog, availability, suspensionPersistence, coordinator);
        }
    }

    private sealed class StubSalesPersistence : ISaleFinalizer, ISalesReader
    {
        private readonly Dictionary<Guid, FinalizedSaleView> salesByOperation = new();
        private readonly ISuspendedSaleStore suspendedSaleStore;
        private long nextSaleId;

        public StubSalesPersistence(ISuspendedSaleStore suspendedSaleStore)
        {
            this.suspendedSaleStore = suspendedSaleStore;
        }

        public Task<Result<FinalizedSaleView>> FinalizeAsync(
            FinalizeSaleRequest request,
            CancellationToken cancellationToken = default)
        {
            if (salesByOperation.TryGetValue(request.Preparation.OperationId, out var replay))
            {
                return Task.FromResult(Result<FinalizedSaleView>.Success(replay));
            }

            var saleId = Interlocked.Increment(ref nextSaleId);
            var completedAt = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc).AddSeconds(saleId);
            var lines = request.Preparation.Lines.Select((line, lineIndex) =>
                new FinalizedSaleLineView(
                    lineIndex + 1,
                    line.ProductId,
                    line.Reference,
                    line.DisplayName,
                    line.Presentation,
                    line.UnitPrice,
                    line.Quantity,
                    line.Subtotal,
                    new[]
                    {
                        new SaleAllocationView(
                            lineIndex + 1,
                            lineIndex + 1,
                            null,
                            null,
                            null,
                            line.Quantity.Value)
                    }));
            var payment = new SalePaymentView(
                saleId,
                request.Payment.Method,
                Money.Create(request.Payment.Amount).Value,
                request.Payment.TenderedAmount,
                request.Payment.ChangeGiven,
                request.Payment.ExternalCardConfirmed,
                completedAt);
            var sale = new FinalizedSaleView(
                saleId,
                $"VTE-TEST-{saleId:0000}",
                request.Preparation.OperationId,
                completedAt,
                request.Preparation.Total,
                request.Preparation.TotalQuantity,
                lines,
                payment);
            salesByOperation.Add(request.Preparation.OperationId, sale);
            if (request.SuspendedSale is not null && suspendedSaleStore is StubSuspendedSaleStore stub)
            {
                stub.MarkResumed(request.SuspendedSale.SuspendedSaleId, request.SuspendedSale.RowVersion);
            }
            return Task.FromResult(Result<FinalizedSaleView>.Success(sale));
        }

        public Task<Result<FinalizedSaleView>> GetByIdAsync(
            long saleId,
            CancellationToken cancellationToken = default)
        {
            var sale = salesByOperation.Values.SingleOrDefault(candidate => candidate.SaleId == saleId);
            return Task.FromResult(sale is null
                ? Result<FinalizedSaleView>.Failure(
                    new Error("sales.sale.not_found", "Vente introuvable."))
                : Result<FinalizedSaleView>.Success(sale));
        }

        public Task<Result<IReadOnlyList<FinalizedSaleSummaryView>>> GetRecentAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            var rows = salesByOperation.Values
                .OrderByDescending(sale => sale.CompletedAtUtc)
                .Take(limit)
                .Select(sale => new FinalizedSaleSummaryView(
                    sale.SaleId,
                    sale.Reference,
                    sale.CompletedAtUtc,
                    sale.LineCount,
                    sale.TotalQuantity,
                    sale.Total,
                    sale.Payment.Method,
                    sale.Status))
                .ToArray();
            return Task.FromResult(Result<IReadOnlyList<FinalizedSaleSummaryView>>.Success(
                Array.AsReadOnly(rows)));
        }
    }

    private sealed class StubSuspendedSaleStore : ISuspendedSaleStore
    {
        private readonly List<SuspendedSaleSummaryView> sales = new();
        private readonly object syncRoot = new();
        private long nextId;

        public Task<Result<SuspendedSaleSummaryView>> SuspendAsync(
            SuspendSaleRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (syncRoot)
            {
                var replay = sales.SingleOrDefault(sale => sale.OperationId == request.OperationId);
                if (replay is not null)
                {
                    return Task.FromResult(Result<SuspendedSaleSummaryView>.Success(replay));
                }

                var id = ++nextId;
                var lines = request.Lines.Select(line => new SuspendedSaleLineView(
                    ProductId.Create(line.ProductId).Value,
                    ProductReference.Create(line.ProductReference).Value,
                    line.ProductName,
                    line.Presentation,
                    Money.Create(line.UnitPrice).Value,
                    SaleQuantity.Create(line.Quantity).Value,
                    Money.Create(line.UnitPrice * line.Quantity).Value)).ToArray();
                var sale = new SuspendedSaleSummaryView(
                    id,
                    request.OperationId,
                    $"HOLD-TEST-{id:0000}",
                    request.CustomerId,
                    null,
                    Money.Create(request.Lines.Sum(line => line.UnitPrice * line.Quantity)).Value,
                    lines.Length,
                    new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc).AddMinutes(id),
                    "Open",
                    BitConverter.GetBytes(1L),
                    Array.AsReadOnly(lines));
                sales.Insert(0, sale);
                return Task.FromResult(Result<SuspendedSaleSummaryView>.Success(sale));
            }
        }

        public Task<Result<IReadOnlyList<SuspendedSaleSummaryView>>> GetOpenAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (syncRoot)
            {
                IReadOnlyList<SuspendedSaleSummaryView> open = Array.AsReadOnly(sales
                    .Where(sale => sale.Status == "Open")
                    .ToArray());
                return Task.FromResult(Result<IReadOnlyList<SuspendedSaleSummaryView>>.Success(open));
            }
        }

        public Task<Result> AbandonAsync(
            Guid operationId,
            long suspendedSaleId,
            byte[] rowVersion,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (syncRoot)
            {
                var index = sales.FindIndex(sale => sale.SuspendedSaleId == suspendedSaleId);
                if (index < 0)
                {
                    return Task.FromResult(Result.Failure(Pharmacy.Application.Sales.Errors.SalesApplicationErrors.SaleNotFound));
                }

                var sale = sales[index];
                if (sale.Status != "Open" || !sale.RowVersion.AsSpan().SequenceEqual(rowVersion))
                {
                    return Task.FromResult(Result.Failure(Pharmacy.Application.Sales.Errors.SalesApplicationErrors.OperationConflict));
                }

                sales[index] = sale with { Status = "Abandoned", RowVersion = BitConverter.GetBytes(2L) };
                return Task.FromResult(Result.Success());
            }
        }

        public void MarkResumed(long suspendedSaleId, byte[] rowVersion)
        {
            lock (syncRoot)
            {
                var index = sales.FindIndex(sale => sale.SuspendedSaleId == suspendedSaleId);
                if (index >= 0
                    && sales[index].Status == "Open"
                    && sales[index].RowVersion.AsSpan().SequenceEqual(rowVersion))
                {
                    sales[index] = sales[index] with { Status = "Resumed", RowVersion = BitConverter.GetBytes(2L) };
                }
            }
        }
    }

    private sealed class BlockingSuspendedSaleStore : ISuspendedSaleStore
    {
        private readonly ISuspendedSaleStore inner;
        private readonly TaskCompletionSource blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int blockNext;

        public BlockingSuspendedSaleStore(ISuspendedSaleStore inner) => this.inner = inner;

        public void BlockNextGetOpen() => Volatile.Write(ref blockNext, 1);

        public Task WaitUntilBlockedAsync(CancellationToken cancellationToken) =>
            blocked.Task.WaitAsync(cancellationToken);

        public void Release() => released.TrySetResult();

        public Task<Result<SuspendedSaleSummaryView>> SuspendAsync(
            SuspendSaleRequest request,
            CancellationToken cancellationToken = default) => inner.SuspendAsync(request, cancellationToken);

        public async Task<Result<IReadOnlyList<SuspendedSaleSummaryView>>> GetOpenAsync(
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref blockNext, 0) == 1)
            {
                blocked.TrySetResult();
                await released.Task.WaitAsync(cancellationToken);
            }

            return await inner.GetOpenAsync(cancellationToken);
        }

        public Task<Result> AbandonAsync(
            Guid operationId,
            long suspendedSaleId,
            byte[] rowVersion,
            CancellationToken cancellationToken = default) =>
            inner.AbandonAsync(operationId, suspendedSaleId, rowVersion, cancellationToken);
    }

    private sealed class UnavailableSuspendedSaleStore : ISuspendedSaleStore
    {
        public Task<Result<SuspendedSaleSummaryView>> SuspendAsync(
            SuspendSaleRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<SuspendedSaleSummaryView>.Failure(
                Pharmacy.Application.Sales.Errors.SalesApplicationErrors.DatabaseUnavailable));

        public Task<Result<IReadOnlyList<SuspendedSaleSummaryView>>> GetOpenAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<IReadOnlyList<SuspendedSaleSummaryView>>.Failure(
                Pharmacy.Application.Sales.Errors.SalesApplicationErrors.DatabaseUnavailable));

        public Task<Result> AbandonAsync(
            Guid operationId,
            long suspendedSaleId,
            byte[] rowVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure(
                Pharmacy.Application.Sales.Errors.SalesApplicationErrors.DatabaseUnavailable));
    }

    private sealed class DenyPermissionAuthorizer : Pharmacy.Application.Security.Contracts.IPermissionAuthorizer
    {
        public Result Authorize(string permission, bool requireRecentReauthentication = false) =>
            Result.Failure(Pharmacy.Application.Security.Errors.SecurityErrors.PermissionDenied);
    }

    private sealed class RecordingBusinessAudit : Pharmacy.Application.Security.Contracts.IBusinessAuditRecorder
    {
        public List<string> Actions { get; } = new();

        public Task RecordSuccessAsync(
            string module,
            string entityType,
            string? entityId,
            string action,
            Guid? operationId,
            CancellationToken cancellationToken = default)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingStockReader : IStockAvailabilityReader
    {
        private readonly IStockAvailabilityReader inner;
        private TaskCompletionSource<bool>? entered;
        private TaskCompletionSource<bool>? release;
        private int shouldBlock;

        public BlockingStockReader(IStockAvailabilityReader inner)
        {
            this.inner = inner;
        }

        public void BlockNextRequest()
        {
            entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref shouldBlock, 1);
        }

        public Task WaitUntilBlockedAsync(CancellationToken cancellationToken)
        {
            return (entered ?? throw new InvalidOperationException("Blocking was not configured."))
                .Task.WaitAsync(cancellationToken);
        }

        public void Release()
        {
            release?.TrySetResult(true);
        }

        public async Task<Result<StockAvailability>> GetAvailabilityAsync(
            ProductId productId,
            SaleQuantity requestedQuantity,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref shouldBlock, 0) == 1)
            {
                entered!.TrySetResult(true);
                await release!.Task.WaitAsync(cancellationToken);
            }

            return await inner.GetAvailabilityAsync(productId, requestedQuantity, cancellationToken);
        }
    }

    private sealed class SwitchableStockReader : IStockAvailabilityReader
    {
        private readonly IStockAvailabilityReader inner;

        public SwitchableStockReader(IStockAvailabilityReader inner)
        {
            this.inner = inner;
        }

        public ProductId? DeniedProductId { get; set; }

        public Task<Result<StockAvailability>> GetAvailabilityAsync(
            ProductId productId,
            SaleQuantity requestedQuantity,
            CancellationToken cancellationToken = default)
        {
            if (productId == DeniedProductId)
            {
                return Task.FromResult(
                    Result<StockAvailability>.Success(
                        new StockAvailability(productId, 0, false)));
            }

            return inner.GetAvailabilityAsync(productId, requestedQuantity, cancellationToken);
        }
    }
}
