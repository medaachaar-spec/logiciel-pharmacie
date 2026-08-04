using System.Reflection;
using System.Xml.Linq;
using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Catalog.Queries;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Commands;
using Pharmacy.Application.Sales.Drafts.Queries;
using Pharmacy.Application.Sales.Models;
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
        Assert.Equal("DEMO-HOLD-0001", suspension.Reference);
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
    public async Task FailedResumeKeepsSuspensionAndDisplayedEmptyDraft()
    {
        var stock = new SwitchableStockReader(new InMemoryStockAvailabilityReader());
        var fixture = Fixture.Create(stock);
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        await fixture.Coordinator.AddProductAsync(ProductId.Create(1).Value, TestCancellation);
        await fixture.Coordinator.SuspendCurrentDraftAsync(TestCancellation);
        var reference = Assert.Single(fixture.Coordinator.SuspendedDrafts).Reference;
        var displayedDraft = fixture.Coordinator.CurrentDraft;
        stock.DeniedProductId = ProductId.Create(1).Value;

        Assert.False(await fixture.Coordinator.ResumeSuspendedDraftAsync(reference, TestCancellation));

        Assert.Single(fixture.Coordinator.SuspendedDrafts);
        Assert.Same(displayedDraft, fixture.Coordinator.CurrentDraft);
        Assert.True(fixture.Coordinator.CurrentDraft!.IsEmpty);
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
        var stock = new BlockingStockReader(new InMemoryStockAvailabilityReader());
        var fixture = Fixture.Create(stock);
        await fixture.Coordinator.InitializeAsync(TestCancellation);
        await fixture.Coordinator.AddProductAsync(ProductId.Create(1).Value, TestCancellation);
        await fixture.Coordinator.SuspendCurrentDraftAsync(TestCancellation);
        var reference = Assert.Single(fixture.Coordinator.SuspendedDrafts).Reference;
        stock.BlockNextRequest();

        var first = fixture.Coordinator.ResumeSuspendedDraftAsync(reference, TestCancellation);
        await stock.WaitUntilBlockedAsync(TestCancellation);
        var second = await fixture.Coordinator.ResumeSuspendedDraftAsync(reference, TestCancellation);
        stock.Release();

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
        var completed = fixture.Coordinator.CompleteSale();

        Assert.Equal(view!.Total, completed.Total);
        Assert.Equal(view.TotalQuantity, completed.ItemCount);
        Assert.Equal(view.LineCount, completed.LineCount);
        Assert.Equal(view.Lines, completed.Lines);
        Assert.Equal(5m, completed.Change);
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
        fixture.Coordinator.TryBeginCompletion();
        fixture.Coordinator.CompleteSale();
        var oldDraftId = fixture.Coordinator.CurrentDraft!.Id;
        Assert.True(fixture.Coordinator.TryBeginTicketPrint());
        Assert.True(fixture.Coordinator.CompleteTicketPrint(DemoPrintSimulationOutcome.Failure));

        Assert.True(await fixture.Coordinator.StartNewDraftAsync(TestCancellation));

        Assert.NotEqual(oldDraftId, fixture.Coordinator.CurrentDraft!.Id);
        Assert.True(fixture.Coordinator.CurrentDraft.IsEmpty);
        Assert.Null(fixture.Coordinator.Result);
        Assert.Equal(DemoPaymentMethod.None, fixture.Coordinator.PaymentMethod);
        Assert.Equal(DemoPrintStatus.NotRequested, fixture.Coordinator.PrintStatus);
    }

    [Fact]
    public void CoordinatorHasNoSaleFinalizerOrPreparationDependency()
    {
        var dependencies = typeof(SalesPosCoordinator)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.DoesNotContain(typeof(ISaleFinalizer), dependencies);
        Assert.DoesNotContain(typeof(Pharmacy.Application.Sales.Finalization.Commands.PrepareSaleFinalizationHandler), dependencies);
    }

    [Fact]
    public void CompositionRootRegistersSqlCatalogMemoryDraftsAndAllHandlers()
    {
        var source = DesktopText("SalesServiceCollectionExtensions.cs");

        Assert.Contains("AddSqlCatalogAdaptersFromEnvironment", source, StringComparison.Ordinal);
        Assert.Contains("ISaleDraftStore, InMemorySaleDraftStore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InMemoryProductCatalogReader", source, StringComparison.Ordinal);
        Assert.Contains("AddTransient<SearchSellableProductsHandler>", source, StringComparison.Ordinal);
        Assert.Contains("AddTransient<SearchCatalogProductsHandler>", source, StringComparison.Ordinal);
        Assert.Contains("AddTransient<PrepareSaleFinalizationHandler>", source, StringComparison.Ordinal);
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
        Assert.Contains("Mode démonstration — Catalogue et Stock SQL", page, StringComparison.Ordinal);
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
            SalesPosCoordinator coordinator)
        {
            Store = store;
            Catalog = catalog;
            Stock = stock;
            Coordinator = coordinator;
        }

        public InMemorySaleDraftStore Store { get; }

        public IProductCatalogReader Catalog { get; }

        public IStockAvailabilityReader Stock { get; }

        public SalesPosCoordinator Coordinator { get; }

        public static Fixture Create(IStockAvailabilityReader? stock = null)
        {
            var store = new InMemorySaleDraftStore();
            var catalog = new InMemoryProductCatalogReader();
            var availability = stock ?? new InMemoryStockAvailabilityReader();
            var coordinator = new SalesPosCoordinator(
                new CreateSaleDraftHandler(new InMemorySaleDraftIdGenerator(), store),
                new SearchSellableProductsHandler(catalog, availability),
                new GetSellableProductByCodeHandler(catalog, availability),
                new AddProductToSaleDraftHandler(store, catalog, availability),
                new ChangeSaleItemQuantityHandler(store, availability),
                new RemoveSaleItemHandler(store),
                new ClearSaleDraftHandler(store),
                new GetSaleDraftHandler(store),
                new DemoTicketPrintService());
            return new Fixture(store, catalog, availability, coordinator);
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
