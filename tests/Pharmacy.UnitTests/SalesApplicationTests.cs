using System.Reflection;
using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Catalog.Queries;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Commands;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Finalization.Models;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class SalesApplicationTests
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void ProductLookupByIdCannotAlsoContainACode()
    {
        var lookup = SellableProductLookup.ByProductId(ProductId.Create(1).Value);

        Assert.Equal(SellableProductLookupKind.ProductId, lookup.Kind);
        Assert.NotNull(lookup.ProductId);
        Assert.Null(lookup.ExactCode);
    }

    [Fact]
    public void ProductLookupByCodeIsTrimmedAndExclusive()
    {
        var result = SellableProductLookup.ByExactCode("  DEMO-000001  ");

        Assert.True(result.IsSuccess);
        Assert.Equal(SellableProductLookupKind.ExactCode, result.Value.Kind);
        Assert.Equal("DEMO-000001", result.Value.ExactCode);
        Assert.Null(result.Value.ProductId);
    }

    [Fact]
    public void EmptyProductLookupCodeReturnsAnExplicitError()
    {
        var result = SellableProductLookup.ByExactCode(" ");

        Assert.True(result.IsFailure);
        Assert.Equal("sales.product_code.invalid", result.Error.Code);
    }

    [Fact]
    public void DraftViewCopiesItsInputLines()
    {
        var mutableLines = new List<SaleDraftLineView> { LineView(1) };
        var view = new SaleDraftView(
            SaleDraftId.New(),
            1,
            mutableLines,
            1,
            Money.Create(10m).Value,
            false);

        mutableLines.Clear();

        Assert.Single(view.Lines);
        Assert.Throws<NotSupportedException>(() =>
            Assert.IsAssignableFrom<IList<SaleDraftLineView>>(view.Lines).Clear());
    }

    [Fact]
    public void PreparationCopiesLinesAndAlwaysHasPreparedStatus()
    {
        var mutableLines = new List<SaleDraftLineView> { LineView(1) };
        var preparation = new SaleFinalizationPreparation(
            Guid.NewGuid(),
            SaleDraftId.New(),
            2,
            mutableLines,
            1,
            Money.Create(10m).Value,
            new SaleFinalizationFingerprint("fingerprint"));

        mutableLines.Clear();

        Assert.Single(preparation.Lines);
        Assert.Equal(SaleFinalizationPreparationStatus.Prepared, preparation.Status);
        Assert.Throws<NotSupportedException>(() =>
            Assert.IsAssignableFrom<IList<SaleDraftLineView>>(preparation.Lines).Clear());
    }

    [Fact]
    public async Task EmptySearchDoesNotCallAnyAdapter()
    {
        var catalog = new StubCatalog([]);
        var stock = new StubStock();
        var handler = new SearchSellableProductsHandler(catalog, stock);

        var result = await handler.HandleAsync(
            new ProductSearchQuery("  "),
            TestCancellation);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Equal(0, catalog.SearchCalls);
        Assert.Equal(0, stock.Calls);
    }

    [Fact]
    public async Task SearchReturnsAtMostTwentyFiveResults()
    {
        var catalog = new StubCatalog(
            Enumerable.Range(1, 30).Select(Product).ToArray());
        var stock = new StubStock();
        var handler = new SearchSellableProductsHandler(catalog, stock);

        var result = await handler.HandleAsync(
            new ProductSearchQuery("DEMO"),
            TestCancellation);

        Assert.Equal(SearchSellableProductsHandler.MaximumResults, result.Value.Count);
        Assert.Equal(SearchSellableProductsHandler.MaximumResults, stock.Calls);
    }

    [Fact]
    public async Task SearchExcludesProductsMarkedNotSellable()
    {
        var products = new[]
        {
            Product(1),
            Product(2) with { IsSellable = false }
        };
        var handler = new SearchSellableProductsHandler(
            new StubCatalog(products),
            new StubStock());

        var result = await handler.HandleAsync(
            new ProductSearchQuery("DEMO"),
            TestCancellation);

        Assert.Equal(1, Assert.Single(result.Value).ProductId.Value);
    }

    [Fact]
    public async Task NonSellableProductIsRejectedWithoutSavingDraft()
    {
        var draft = SaleDraft.Create(SaleDraftId.New());
        var store = new StubDraftStore(new VersionedSaleDraft(draft, 1));
        var catalog = new StubCatalog([Product(1) with { IsSellable = false }]);
        var handler = new AddProductToSaleDraftHandler(store, catalog, new StubStock());

        var result = await handler.HandleAsync(
            new AddProductToSaleDraftCommand(
                draft.Id,
                SellableProductLookup.ByProductId(ProductId.Create(1).Value),
                SaleQuantity.One,
                1),
            TestCancellation);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.product.not_sellable", result.Error.Code);
        Assert.Equal(0, store.SaveCalls);
        Assert.True(draft.IsEmpty);
    }

    [Fact]
    public async Task VersionConflictIsDetectedBeforeCatalogLookup()
    {
        var draft = SaleDraft.Create(SaleDraftId.New());
        var store = new StubDraftStore(new VersionedSaleDraft(draft, 2));
        var catalog = new StubCatalog([Product(1)]);
        var handler = new AddProductToSaleDraftHandler(store, catalog, new StubStock());

        var result = await handler.HandleAsync(
            new AddProductToSaleDraftCommand(
                draft.Id,
                SellableProductLookup.ByProductId(ProductId.Create(1).Value),
                SaleQuantity.One,
                1),
            TestCancellation);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.draft.concurrency_conflict", result.Error.Code);
        Assert.Equal(0, catalog.FindCalls);
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public async Task EmptyOperationIdIsRejectedBeforeDraftRead()
    {
        var draft = SaleDraft.Create(SaleDraftId.New());
        var store = new StubDraftStore(new VersionedSaleDraft(draft, 1));
        var handler = new PrepareSaleFinalizationHandler(
            store,
            new StubStock(),
            new StubPreparationStore());

        var result = await handler.HandleAsync(
            new PrepareSaleFinalizationCommand(Guid.Empty, draft.Id, 1),
            TestCancellation);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.operation.invalid", result.Error.Code);
        Assert.Equal(0, store.GetCalls);
    }

    [Fact]
    public async Task PreparationStopsWhenAnyLineIsUnavailable()
    {
        var draft = SaleDraft.Create(SaleDraftId.New());
        var product = Product(1);
        var snapshot = SaleProductSnapshot.Create(
            product.ProductId,
            product.Reference,
            product.DisplayName,
            product.Presentation,
            product.UnitSalePrice).Value;
        draft.AddItem(snapshot, SaleQuantity.One);
        var store = new StubDraftStore(new VersionedSaleDraft(draft, 2));
        var preparations = new StubPreparationStore();
        var handler = new PrepareSaleFinalizationHandler(
            store,
            new StubStock(isAvailable: false, availableQuantity: 0),
            preparations);

        var result = await handler.HandleAsync(
            new PrepareSaleFinalizationCommand(Guid.NewGuid(), draft.Id, 2),
            TestCancellation);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.stock.insufficient", result.Error.Code);
        Assert.Equal(0, preparations.Calls);
        Assert.Equal(2, store.VersionedDraft.Version);
    }

    [Fact]
    public void PreparationHandlerDoesNotDependOnSaleFinalizer()
    {
        var constructorTypes = typeof(PrepareSaleFinalizationHandler)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.DoesNotContain(typeof(ISaleFinalizer), constructorTypes);
        Assert.Contains(typeof(ISaleFinalizationPreparationStore), constructorTypes);
    }

    [Theory]
    [InlineData(SalePaymentMethod.Cash, 5, false, "sales.payment.invalid")]
    [InlineData(SalePaymentMethod.ExternalCard, 0, false, "sales.payment.card_unconfirmed")]
    public async Task InvalidPaymentIsRejectedBeforePersistentFinalizer(
        SalePaymentMethod paymentMethod,
        decimal tenderedAmount,
        bool cardConfirmed,
        string expectedError)
    {
        var draft = SaleDraft.Create(SaleDraftId.New());
        var product = Product(1);
        draft.AddItem(
            SaleProductSnapshot.Create(
                product.ProductId,
                product.Reference,
                product.DisplayName,
                product.Presentation,
                product.UnitSalePrice).Value,
            SaleQuantity.One);
        var store = new StubDraftStore(new VersionedSaleDraft(draft, 2));
        var finalizer = new RecordingFinalizer();
        var handler = new FinalizeSaleHandler(
            new PrepareSaleFinalizationHandler(
                store,
                new StubStock(),
                new StubPreparationStore()),
            finalizer);

        var result = await handler.HandleAsync(
            new FinalizeSaleCommand(
                Guid.NewGuid(),
                draft.Id,
                2,
                paymentMethod,
                paymentMethod == SalePaymentMethod.Cash ? tenderedAmount : null,
                cardConfirmed),
            TestCancellation);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedError, result.Error.Code);
        Assert.Equal(0, finalizer.Calls);
    }

    [Fact]
    public void ApplicationErrorsExposeStableExpectedCodes()
    {
        Assert.Equal("sales.draft.version.invalid", SalesApplicationErrors.InvalidExpectedVersion.Code);
        Assert.Equal("sales.product.not_found", SalesApplicationErrors.ProductNotFound.Code);
        Assert.Equal("sales.product.not_sellable", SalesApplicationErrors.ProductNotSellable.Code);
        Assert.Equal("sales.finalization.empty_draft", SalesApplicationErrors.DraftEmpty.Code);
        Assert.Equal("sales.operation.conflict", SalesApplicationErrors.OperationConflict.Code);
    }

    private static SellableProductSnapshot Product(int number)
    {
        return new SellableProductSnapshot(
            ProductId.Create(number).Value,
            ProductReference.Create($"DEMO-{number:000000}").Value,
            $"Produit Démo {number}",
            "Présentation synthétique",
            Money.Create(10m).Value,
            true);
    }

    private static SaleDraftLineView LineView(long productId)
    {
        var quantity = SaleQuantity.One;
        var price = Money.Create(10m).Value;
        return new SaleDraftLineView(
            ProductId.Create(productId).Value,
            ProductReference.Create($"DEMO-{productId:000000}").Value,
            "Produit synthétique",
            null,
            price,
            quantity,
            price.Multiply(quantity));
    }

    private sealed class StubCatalog : IProductCatalogReader
    {
        private readonly IReadOnlyList<SellableProductSnapshot> products;

        public StubCatalog(IReadOnlyList<SellableProductSnapshot> products)
        {
            this.products = products;
        }

        public int FindCalls { get; private set; }

        public int SearchCalls { get; private set; }

        public Task<Result<SellableProductSnapshot>> FindByIdAsync(
            ProductId productId,
            CancellationToken cancellationToken = default)
        {
            FindCalls++;
            var product = products.FirstOrDefault(candidate => candidate.ProductId == productId);
            return Task.FromResult(product is null
                ? Result<SellableProductSnapshot>.Failure(SalesApplicationErrors.ProductNotFound)
                : Result<SellableProductSnapshot>.Success(product));
        }

        public Task<Result<SellableProductSnapshot>> FindByCodeAsync(
            string code,
            CancellationToken cancellationToken = default)
        {
            FindCalls++;
            var product = products.FirstOrDefault(candidate =>
                string.Equals(candidate.Reference.Value, code, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(product is null
                ? Result<SellableProductSnapshot>.Failure(SalesApplicationErrors.ProductNotFound)
                : Result<SellableProductSnapshot>.Success(product));
        }

        public Task<Result<IReadOnlyList<SellableProductSnapshot>>> SearchAsync(
            ProductSearchQuery query,
            CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            return Task.FromResult(
                Result<IReadOnlyList<SellableProductSnapshot>>.Success(products));
        }
    }

    private sealed class StubStock : IStockAvailabilityReader
    {
        private readonly bool isAvailable;
        private readonly int availableQuantity;

        public StubStock(bool isAvailable = true, int availableQuantity = 100)
        {
            this.isAvailable = isAvailable;
            this.availableQuantity = availableQuantity;
        }

        public int Calls { get; private set; }

        public Task<Result<StockAvailability>> GetAvailabilityAsync(
            ProductId productId,
            SaleQuantity requestedQuantity,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(
                Result<StockAvailability>.Success(
                    new StockAvailability(productId, availableQuantity, isAvailable)));
        }
    }

    private sealed class StubDraftStore : ISaleDraftStore
    {
        private readonly VersionedSaleDraft versionedDraft;

        public StubDraftStore(VersionedSaleDraft versionedDraft)
        {
            this.versionedDraft = versionedDraft;
        }

        public int GetCalls { get; private set; }

        public int SaveCalls { get; private set; }

        public VersionedSaleDraft VersionedDraft => versionedDraft;

        public Task<Result<VersionedSaleDraft>> CreateAsync(
            SaleDraft draft,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<VersionedSaleDraft>.Success(versionedDraft));
        }

        public Task<Result<VersionedSaleDraft>> GetAsync(
            SaleDraftId draftId,
            CancellationToken cancellationToken = default)
        {
            GetCalls++;
            return Task.FromResult(Result<VersionedSaleDraft>.Success(versionedDraft));
        }

        public Task<Result<VersionedSaleDraft>> SaveAsync(
            SaleDraft draft,
            long expectedVersion,
            CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.FromResult(
                Result<VersionedSaleDraft>.Success(
                    new VersionedSaleDraft(draft, expectedVersion + 1)));
        }
    }

    private sealed class StubPreparationStore : ISaleFinalizationPreparationStore
    {
        public int Calls { get; private set; }

        public Task<Result<SaleFinalizationPreparation>> GetOrAddAsync(
            SaleFinalizationPreparation preparation,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Result<SaleFinalizationPreparation>.Success(preparation));
        }
    }

    private sealed class RecordingFinalizer : ISaleFinalizer
    {
        public int Calls { get; private set; }

        public Task<Result<FinalizedSaleView>> FinalizeAsync(
            FinalizeSaleRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Result<FinalizedSaleView>.Failure(
                SalesApplicationErrors.OperationConflict));
        }
    }
}
