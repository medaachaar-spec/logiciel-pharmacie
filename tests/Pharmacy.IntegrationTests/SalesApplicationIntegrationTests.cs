using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Catalog.Queries;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Commands;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Drafts.Queries;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Sales.InMemory;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class SalesApplicationIntegrationTests
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CatalogContainsTenDeterministicSyntheticProducts()
    {
        var context = new SalesContext();

        var result = await context.Search.HandleAsync(
            new ProductSearchQuery("DEMO-"),
            TestCancellation);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value.Count);
        Assert.Equal(
            Enumerable.Range(1, 10).Select(number => $"DEMO-{number:000000}"),
            result.Value.Select(product => product.Reference.Value));
        Assert.All(result.Value, product => Assert.True(product.IsSellable));
    }

    [Fact]
    public async Task CatalogSearchIsCaseInsensitiveAcrossNamePresentationAndReference()
    {
        var context = new SalesContext();

        var byName = await context.Search.HandleAsync(
            new ProductSearchQuery("hYgIèNe"),
            TestCancellation);
        var byPresentation = await context.Search.HandleAsync(
            new ProductSearchQuery("tube 75"),
            TestCancellation);
        var byReference = await context.Search.HandleAsync(
            new ProductSearchQuery("demo-000003"),
            TestCancellation);

        Assert.Equal("DEMO-000006", Assert.Single(byName.Value).Reference.Value);
        Assert.Equal("DEMO-000007", Assert.Single(byPresentation.Value).Reference.Value);
        Assert.Equal("DEMO-000003", Assert.Single(byReference.Value).Reference.Value);
    }

    [Fact]
    public async Task EmptyCatalogSearchReturnsAnEmptyReadOnlyList()
    {
        var context = new SalesContext();

        var result = await context.Search.HandleAsync(
            new ProductSearchQuery("  "),
            TestCancellation);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Throws<NotSupportedException>(() =>
            Assert.IsAssignableFrom<IList<SellableProductView>>(result.Value).Add(result.Value.FirstOrDefault()!));
    }

    [Fact]
    public async Task ProductCanBeReadByIdWithDescriptiveAvailability()
    {
        var context = new SalesContext();
        var handler = new GetSellableProductByIdHandler(context.Catalog, context.Stock);

        var result = await handler.HandleAsync(
            new GetSellableProductByIdQuery(ProductId.Create(1).Value),
            TestCancellation);

        Assert.True(result.IsSuccess);
        Assert.Equal("DEMO-000001", result.Value.Reference.Value);
        Assert.Equal(8, result.Value.AvailableQuantity);
        Assert.True(result.Value.IsAvailable);
    }

    [Fact]
    public async Task ProductCanBeReadByTrimmedCaseInsensitiveExactCode()
    {
        var context = new SalesContext();
        var handler = new GetSellableProductByCodeHandler(context.Catalog, context.Stock);

        var result = await handler.HandleAsync(
            new GetSellableProductByCodeQuery("  demo-000010  "),
            TestCancellation);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value.ProductId.Value);
    }

    [Fact]
    public async Task UnknownProductReturnsAnExplicitError()
    {
        var context = new SalesContext();
        var handler = new GetSellableProductByCodeHandler(context.Catalog, context.Stock);

        var result = await handler.HandleAsync(
            new GetSellableProductByCodeQuery("DEMO-UNKNOWN"),
            TestCancellation);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.product.not_found", result.Error.Code);
    }

    [Fact]
    public async Task DraftCreationUsesTheGeneratorAndStartsAtVersionOne()
    {
        var expectedId = SaleDraftId.Create(Guid.Parse("d9cdf63f-c4ab-4436-a925-e924a6b9e3cb")).Value;
        var store = new InMemorySaleDraftStore();
        var handler = new CreateSaleDraftHandler(new FixedDraftIdGenerator(expectedId), store);

        var result = await handler.HandleAsync(TestCancellation);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedId, result.Value.Id);
        Assert.Equal(1, result.Value.Version);
        Assert.True(result.Value.IsEmpty);
        Assert.Equal(Money.Zero, result.Value.Total);
    }

    [Fact]
    public async Task MissingDraftReturnsAnExplicitError()
    {
        var context = new SalesContext();
        var handler = new GetSaleDraftHandler(context.Drafts);

        var result = await handler.HandleAsync(
            new GetSaleDraftQuery(SaleDraftId.New()),
            TestCancellation);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.draft.not_found", result.Error.Code);
    }

    [Fact]
    public async Task AddingAProductByIdIncrementsTheVersion()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();

        var result = await context.Add.HandleAsync(
            new AddProductToSaleDraftCommand(
                draft.Id,
                SellableProductLookup.ByProductId(ProductId.Create(1).Value),
                SaleQuantity.Create(2).Value,
                draft.Version),
            TestCancellation);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Version);
        var line = Assert.Single(result.Value.Lines);
        Assert.Equal(2, line.Quantity.Value);
        Assert.Equal(25.00m, line.Subtotal.Amount);
    }

    [Fact]
    public async Task AddingAProductByExactCodeIsSupported()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();
        var lookup = SellableProductLookup.ByExactCode("demo-000002").Value;

        var result = await context.Add.HandleAsync(
            new AddProductToSaleDraftCommand(
                draft.Id,
                lookup,
                SaleQuantity.One,
                draft.Version),
            TestCancellation);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, Assert.Single(result.Value.Lines).ProductId.Value);
    }

    [Fact]
    public async Task AddingTheSameProductMergesAgainstTheTargetQuantity()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();
        var first = await context.AddByIdAsync(draft, 1, 2);

        var second = await context.AddByIdAsync(first.Value, 1, 3);

        Assert.True(second.IsSuccess);
        Assert.Equal(3, second.Value.Version);
        Assert.Equal(5, Assert.Single(second.Value.Lines).Quantity.Value);
    }

    [Fact]
    public async Task MergeAvailabilityUsesExistingPlusRequestedQuantity()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();
        var first = await context.AddByIdAsync(draft, 1, 5);

        var rejected = await context.AddByIdAsync(first.Value, 1, 4);
        var stored = await context.GetDraftAsync(draft.Id);

        Assert.True(rejected.IsFailure);
        Assert.Equal("sales.stock.insufficient_availability", rejected.Error.Code);
        Assert.Equal(5, Assert.Single(stored.Value.Lines).Quantity.Value);
        Assert.Equal(first.Value.Version, stored.Value.Version);
    }

    [Fact]
    public async Task InsufficientAvailabilityLeavesTheStoredDraftIntact()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();

        var result = await context.AddByIdAsync(draft, 5, 5);
        var stored = await context.GetDraftAsync(draft.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.stock.insufficient_availability", result.Error.Code);
        Assert.Equal(1, stored.Value.Version);
        Assert.True(stored.Value.IsEmpty);
    }

    [Fact]
    public async Task UnknownProductLeavesTheStoredDraftIntact()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();

        var result = await context.Add.HandleAsync(
            new AddProductToSaleDraftCommand(
                draft.Id,
                SellableProductLookup.ByProductId(ProductId.Create(99).Value),
                SaleQuantity.One,
                draft.Version),
            TestCancellation);
        var stored = await context.GetDraftAsync(draft.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.product.not_found", result.Error.Code);
        Assert.True(stored.Value.IsEmpty);
    }

    [Fact]
    public async Task StaleVersionIsRejectedBeforeMutation()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();
        var updated = await context.AddByIdAsync(draft, 1, 1);

        var stale = await context.AddByIdAsync(draft, 2, 1);
        var stored = await context.GetDraftAsync(draft.Id);

        Assert.True(stale.IsFailure);
        Assert.Equal("sales.draft.concurrency_conflict", stale.Error.Code);
        Assert.Equal(updated.Value.Version, stored.Value.Version);
        Assert.Single(stored.Value.Lines);
    }

    [Fact]
    public async Task QuantityChangeChecksAvailabilityAndIncrementsVersion()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();
        var added = await context.AddByIdAsync(draft, 1, 1);

        var result = await context.Change.HandleAsync(
            new ChangeSaleItemQuantityCommand(
                draft.Id,
                ProductId.Create(1).Value,
                SaleQuantity.Create(7).Value,
                added.Value.Version),
            TestCancellation);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Version);
        Assert.Equal(7, Assert.Single(result.Value.Lines).Quantity.Value);
    }

    [Fact]
    public async Task RejectedQuantityChangeDoesNotModifyTheStoredLine()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();
        var added = await context.AddByIdAsync(draft, 1, 2);

        var result = await context.Change.HandleAsync(
            new ChangeSaleItemQuantityCommand(
                draft.Id,
                ProductId.Create(1).Value,
                SaleQuantity.Create(9).Value,
                added.Value.Version),
            TestCancellation);
        var stored = await context.GetDraftAsync(draft.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(2, Assert.Single(stored.Value.Lines).Quantity.Value);
        Assert.Equal(added.Value.Version, stored.Value.Version);
    }

    [Fact]
    public async Task MissingLineChangeReturnsTheDomainError()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();

        var result = await context.Change.HandleAsync(
            new ChangeSaleItemQuantityCommand(
                draft.Id,
                ProductId.Create(1).Value,
                SaleQuantity.One,
                draft.Version),
            TestCancellation);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.line.not_found", result.Error.Code);
    }

    [Fact]
    public async Task RemovingALineIncrementsVersionAndNeedsNoStockRead()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();
        var added = await context.AddByIdAsync(draft, 1, 1);

        var result = await context.Remove.HandleAsync(
            new RemoveSaleItemCommand(
                draft.Id,
                ProductId.Create(1).Value,
                added.Value.Version),
            TestCancellation);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Version);
        Assert.True(result.Value.IsEmpty);
    }

    [Fact]
    public async Task RemovingAMissingLineDoesNotIncrementVersion()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();

        var result = await context.Remove.HandleAsync(
            new RemoveSaleItemCommand(
                draft.Id,
                ProductId.Create(1).Value,
                draft.Version),
            TestCancellation);
        var stored = await context.GetDraftAsync(draft.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.line.not_found", result.Error.Code);
        Assert.Equal(1, stored.Value.Version);
    }

    [Fact]
    public async Task ClearingANonEmptyDraftIncrementsVersion()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();
        var added = await context.AddByIdAsync(draft, 1, 1);

        var result = await context.Clear.HandleAsync(
            new ClearSaleDraftCommand(draft.Id, added.Value.Version),
            TestCancellation);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsEmpty);
        Assert.Equal(3, result.Value.Version);
    }

    [Fact]
    public async Task ClearingAnEmptyDraftIsIdempotentWithoutVersionIncrement()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();

        var first = await context.Clear.HandleAsync(
            new ClearSaleDraftCommand(draft.Id, draft.Version),
            TestCancellation);
        var second = await context.Clear.HandleAsync(
            new ClearSaleDraftCommand(draft.Id, first.Value.Version),
            TestCancellation);

        Assert.Equal(1, first.Value.Version);
        Assert.Equal(1, second.Value.Version);
    }

    [Fact]
    public async Task LoadedAggregateIsDetachedFromTheStoredSnapshot()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();
        var detached = await context.Drafts.GetAsync(draft.Id, TestCancellation);
        var product = (await context.Catalog.FindByIdAsync(
            ProductId.Create(1).Value,
            TestCancellation)).Value;
        var snapshot = SaleProductSnapshot.Create(
            product.ProductId,
            product.Reference,
            product.DisplayName,
            product.Presentation,
            product.UnitSalePrice).Value;

        detached.Value.Draft.AddItem(snapshot, SaleQuantity.One);
        var stored = await context.GetDraftAsync(draft.Id);

        Assert.True(stored.Value.IsEmpty);
        Assert.Equal(1, stored.Value.Version);
    }

    [Fact]
    public async Task ConcurrentSavesAllowOnlyOneVersionWinner()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();
        var first = await context.Drafts.GetAsync(draft.Id, TestCancellation);
        var second = await context.Drafts.GetAsync(draft.Id, TestCancellation);
        await AddDirectAsync(context.Catalog, first.Value.Draft, 1);
        await AddDirectAsync(context.Catalog, second.Value.Draft, 2);

        var results = await Task.WhenAll(
            context.Drafts.SaveAsync(first.Value.Draft, 1, TestCancellation),
            context.Drafts.SaveAsync(second.Value.Draft, 1, TestCancellation));
        var stored = await context.GetDraftAsync(draft.Id);

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.IsFailure
            && result.Error.Code == "sales.draft.concurrency_conflict");
        Assert.Equal(2, stored.Value.Version);
        Assert.Single(stored.Value.Lines);
    }

    [Fact]
    public async Task PreparationCreatesAnImmutablePreparedSnapshotWithoutChangingDraft()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();
        var added = await context.AddByIdAsync(draft, 1, 2);
        var operationId = Guid.Parse("7ae2f60a-d37b-457e-8108-f9ed9f3474ec");

        var result = await context.Prepare.HandleAsync(
            new PrepareSaleFinalizationCommand(
                operationId,
                draft.Id,
                added.Value.Version),
            TestCancellation);
        var stored = await context.GetDraftAsync(draft.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(operationId, result.Value.OperationId);
        Assert.Equal(added.Value.Version, result.Value.PreparedVersion);
        Assert.Equal("Prepared", result.Value.Status.ToString());
        Assert.Equal(2, result.Value.TotalQuantity);
        Assert.Equal(25.00m, result.Value.Total.Amount);
        Assert.Equal(added.Value.Version, stored.Value.Version);
        Assert.Equal(2, Assert.Single(stored.Value.Lines).Quantity.Value);
    }

    [Fact]
    public async Task EmptyDraftCannotBePrepared()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();

        var result = await context.Prepare.HandleAsync(
            new PrepareSaleFinalizationCommand(Guid.NewGuid(), draft.Id, draft.Version),
            TestCancellation);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.draft.empty", result.Error.Code);
    }

    [Fact]
    public async Task EmptyOperationIdIsRejectedBeforePreparation()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();

        var result = await context.Prepare.HandleAsync(
            new PrepareSaleFinalizationCommand(Guid.Empty, draft.Id, draft.Version),
            TestCancellation);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.finalization.operation_id.invalid", result.Error.Code);
    }

    [Fact]
    public async Task EquivalentOperationReuseReturnsTheSamePreparedResult()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();
        var added = await context.AddByIdAsync(draft, 1, 1);
        var command = new PrepareSaleFinalizationCommand(
            Guid.Parse("17c781a5-8a38-490a-aacb-8b6bdb69f294"),
            draft.Id,
            added.Value.Version);

        var first = await context.Prepare.HandleAsync(command, TestCancellation);
        var second = await context.Prepare.HandleAsync(command, TestCancellation);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Same(first.Value, second.Value);
        Assert.Equal(first.Value.Fingerprint, second.Value.Fingerprint);
    }

    [Fact]
    public async Task OperationReuseAfterDraftChangeReturnsAConflict()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();
        var firstLine = await context.AddByIdAsync(draft, 1, 1);
        var operationId = Guid.Parse("37c6bc67-7464-447d-a1d9-0717aa6f5beb");
        var first = await context.Prepare.HandleAsync(
            new PrepareSaleFinalizationCommand(
                operationId,
                draft.Id,
                firstLine.Value.Version),
            TestCancellation);
        var secondLine = await context.AddByIdAsync(firstLine.Value, 2, 1);

        var reused = await context.Prepare.HandleAsync(
            new PrepareSaleFinalizationCommand(
                operationId,
                draft.Id,
                secondLine.Value.Version),
            TestCancellation);

        Assert.True(first.IsSuccess);
        Assert.True(reused.IsFailure);
        Assert.Equal("sales.finalization.operation_conflict", reused.Error.Code);
    }

    [Fact]
    public async Task SameOperationIdForAnotherDraftReturnsAConflict()
    {
        var context = new SalesContext();
        var firstDraft = await context.CreateDraftAsync();
        var firstAdded = await context.AddByIdAsync(firstDraft, 1, 1);
        var secondDraft = await context.CreateDraftAsync();
        var secondAdded = await context.AddByIdAsync(secondDraft, 1, 1);
        var operationId = Guid.Parse("6887de65-82a4-4221-8380-d44123e27862");
        await context.Prepare.HandleAsync(
            new PrepareSaleFinalizationCommand(
                operationId,
                firstDraft.Id,
                firstAdded.Value.Version),
            TestCancellation);

        var result = await context.Prepare.HandleAsync(
            new PrepareSaleFinalizationCommand(
                operationId,
                secondDraft.Id,
                secondAdded.Value.Version),
            TestCancellation);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.finalization.operation_conflict", result.Error.Code);
    }

    [Fact]
    public async Task DifferentOperationIdsCanPrepareTheSameDraft()
    {
        var context = new SalesContext();
        var draft = await context.CreateDraftAsync();
        var added = await context.AddByIdAsync(draft, 1, 1);

        var first = await context.Prepare.HandleAsync(
            new PrepareSaleFinalizationCommand(Guid.NewGuid(), draft.Id, added.Value.Version),
            TestCancellation);
        var second = await context.Prepare.HandleAsync(
            new PrepareSaleFinalizationCommand(Guid.NewGuid(), draft.Id, added.Value.Version),
            TestCancellation);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Value.OperationId, second.Value.OperationId);
        Assert.Equal(first.Value.Fingerprint, second.Value.Fingerprint);
    }

    [Fact]
    public async Task DuplicateDraftCreationIsRejected()
    {
        var store = new InMemorySaleDraftStore();
        var draft = SaleDraft.Create(
            SaleDraftId.Create(Guid.Parse("214d17a4-9938-428b-b1da-14587ec8d441")).Value);

        var first = await store.CreateAsync(draft, TestCancellation);
        var duplicate = await store.CreateAsync(draft, TestCancellation);

        Assert.True(first.IsSuccess);
        Assert.True(duplicate.IsFailure);
        Assert.Equal("sales.draft.already_exists", duplicate.Error.Code);
    }

    [Fact]
    public async Task CancellationIsPropagatedWithoutTaskRun()
    {
        var catalog = new InMemoryProductCatalogReader();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            catalog.FindByIdAsync(ProductId.Create(1).Value, cancellation.Token));
    }

    private static async Task AddDirectAsync(
        IProductCatalogReader catalog,
        SaleDraft draft,
        long productId)
    {
        var product = (await catalog.FindByIdAsync(
            ProductId.Create(productId).Value,
            TestCancellation)).Value;
        var snapshot = SaleProductSnapshot.Create(
            product.ProductId,
            product.Reference,
            product.DisplayName,
            product.Presentation,
            product.UnitSalePrice).Value;
        var result = draft.AddItem(snapshot, SaleQuantity.One);
        Assert.True(result.IsSuccess);
    }

    private sealed class FixedDraftIdGenerator : ISaleDraftIdGenerator
    {
        private readonly SaleDraftId id;

        public FixedDraftIdGenerator(SaleDraftId id)
        {
            this.id = id;
        }

        public SaleDraftId NewId()
        {
            return id;
        }
    }

    private sealed class SalesContext
    {
        public SalesContext()
        {
            Drafts = new InMemorySaleDraftStore();
            Catalog = new InMemoryProductCatalogReader();
            Stock = new InMemoryStockAvailabilityReader();
            Preparations = new InMemorySaleFinalizationPreparationStore();
            Create = new CreateSaleDraftHandler(new InMemorySaleDraftIdGenerator(), Drafts);
            Search = new SearchSellableProductsHandler(Catalog, Stock);
            Add = new AddProductToSaleDraftHandler(Drafts, Catalog, Stock);
            Change = new ChangeSaleItemQuantityHandler(Drafts, Stock);
            Remove = new RemoveSaleItemHandler(Drafts);
            Clear = new ClearSaleDraftHandler(Drafts);
            Prepare = new PrepareSaleFinalizationHandler(Drafts, Stock, Preparations);
        }

        public InMemorySaleDraftStore Drafts { get; }

        public InMemoryProductCatalogReader Catalog { get; }

        public InMemoryStockAvailabilityReader Stock { get; }

        public InMemorySaleFinalizationPreparationStore Preparations { get; }

        public CreateSaleDraftHandler Create { get; }

        public SearchSellableProductsHandler Search { get; }

        public AddProductToSaleDraftHandler Add { get; }

        public ChangeSaleItemQuantityHandler Change { get; }

        public RemoveSaleItemHandler Remove { get; }

        public ClearSaleDraftHandler Clear { get; }

        public PrepareSaleFinalizationHandler Prepare { get; }

        public async Task<SaleDraftView> CreateDraftAsync()
        {
            return (await Create.HandleAsync(TestCancellation)).Value;
        }

        public async Task<Result<SaleDraftView>> AddByIdAsync(
            SaleDraftView draft,
            long productId,
            int quantity)
        {
            return await Add.HandleAsync(
                new AddProductToSaleDraftCommand(
                    draft.Id,
                    SellableProductLookup.ByProductId(ProductId.Create(productId).Value),
                    SaleQuantity.Create(quantity).Value,
                    draft.Version),
                TestCancellation);
        }

        public async Task<Result<SaleDraftView>> GetDraftAsync(SaleDraftId draftId)
        {
            return await new GetSaleDraftHandler(Drafts).HandleAsync(
                new GetSaleDraftQuery(draftId),
                TestCancellation);
        }
    }
}
