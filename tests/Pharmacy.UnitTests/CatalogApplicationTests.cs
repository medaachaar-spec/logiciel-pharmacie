using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Catalog.Contracts;
using Pharmacy.Application.Catalog.Errors;
using Pharmacy.Application.Catalog.Models;
using Pharmacy.Application.Catalog.Queries;
using Pharmacy.Domain.Common;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class CatalogApplicationTests
{
    private static readonly CancellationToken TestCancellation = TestContext.Current.CancellationToken;

    [Fact]
    public async Task NegativePriceIsRejectedBeforeTheStore()
    {
        var store = new StubProductStore();
        var handler = new ChangeCatalogProductPriceHandler(store, TestPermissionAuthorizer.AllowAll);

        var result = await handler.HandleAsync(
            new ChangeCatalogProductPriceCommand(1, -0.0001m, null, RowVersion()),
            TestCancellation);

        Assert.True(result.IsFailure);
        Assert.Equal("catalog.price.invalid", result.Error.Code);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task InvalidRowVersionIsRejectedBeforeUpdate()
    {
        var store = new StubProductStore();
        var handler = new UpdateCatalogProductHandler(store, TestPermissionAuthorizer.AllowAll);

        var result = await handler.HandleAsync(
            new UpdateCatalogProductCommand(
                1, "Produit Demo", "Boîte", 1, "DEMO-UNIT-001",
                true, true, false, false, false, 0, null, null, null,
                "not-base64", RowVersion()),
            TestCancellation);

        Assert.True(result.IsFailure);
        Assert.Equal("catalog.validation.failed", result.Error.Code);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task SearchTrimsTextAndEnforcesLimit()
    {
        var reader = new StubProductReader();
        var handler = new SearchCatalogProductsHandler(reader, TestPermissionAuthorizer.AllowAll);

        var invalid = await handler.HandleAsync(
            new SearchCatalogProductsQuery("Demo", Limit: 101),
            TestCancellation);
        var valid = await handler.HandleAsync(
            new SearchCatalogProductsQuery("  Demo  ", Limit: 25),
            TestCancellation);

        Assert.True(invalid.IsFailure);
        Assert.True(valid.IsSuccess);
        Assert.Equal("Demo", reader.Criteria!.Text);
        Assert.Equal(25, reader.Criteria.Limit);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task ProductCreationValidatesReferenceCategoryAndThresholds()
    {
        var store = new StubProductStore();
        var handler = new CreateCatalogProductHandler(store, TestPermissionAuthorizer.AllowAll);

        var result = await handler.HandleAsync(
            new CreateCatalogProductCommand(
                "Produit Demo", "Boîte", 0, " ", 10m,
                MinimumStock: 5m, MaximumStock: 2m),
            TestCancellation);

        Assert.True(result.IsFailure);
        Assert.Equal("catalog.validation.failed", result.Error.Code);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task CategoryCommandsValidateInputAndConcurrencyToken()
    {
        var store = new StubCategoryStore();
        var create = new CreateCatalogCategoryHandler(store, TestPermissionAuthorizer.AllowAll);
        var update = new UpdateCatalogCategoryHandler(store, TestPermissionAuthorizer.AllowAll);

        var invalidCreate = await create.HandleAsync(
            new CreateCatalogCategoryCommand(" ", "Demo"),
            TestCancellation);
        var invalidUpdate = await update.HandleAsync(
            new UpdateCatalogCategoryCommand(1, "DEMO", "Demo", 0, false, false, false, "bad"),
            TestCancellation);

        Assert.True(invalidCreate.IsFailure);
        Assert.True(invalidUpdate.IsFailure);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public void ProductDetailsDefensivelyCopiesCollections()
    {
        var barcodes = new List<CatalogBarcodeView>
        {
            new(1, "000-DEMO", "000-DEMO", true, "Demo", true, DateTime.UtcNow, RowVersion())
        };
        var history = new List<CatalogPriceHistoryView>
        {
            new(1, null, 10m, "MAD", "InitialOnboarding", null, DateTime.UtcNow)
        };
        var details = Details(barcodes, history);

        barcodes.Clear();
        history.Clear();

        Assert.Single(details.Barcodes);
        Assert.Single(details.PriceHistory);
        Assert.IsAssignableFrom<IReadOnlyList<CatalogBarcodeView>>(details.Barcodes);
    }

    [Fact]
    public void RequiredStructuredErrorCodesAreStable()
    {
        var codes = new[]
        {
            CatalogApplicationErrors.ProductNotFound.Code,
            CatalogApplicationErrors.CategoryNotFound.Code,
            CatalogApplicationErrors.DuplicateReference.Code,
            CatalogApplicationErrors.DuplicateBarcode.Code,
            CatalogApplicationErrors.BarcodeNotFound.Code,
            CatalogApplicationErrors.InvalidPrice.Code,
            CatalogApplicationErrors.ProductConcurrencyConflict.Code,
            CatalogApplicationErrors.CategoryConcurrencyConflict.Code,
            CatalogApplicationErrors.ValidationFailed("Demo").Code,
            CatalogApplicationErrors.DatabaseUnavailable.Code
        };

        Assert.Equal(10, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.StartsWith("catalog.", code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActivationRequiresBothEightByteRowVersions()
    {
        var store = new StubProductStore();
        var handler = new SetCatalogProductActivationHandler(store, TestPermissionAuthorizer.AllowAll);

        var result = await handler.HandleAsync(
            new SetCatalogProductActivationCommand(1, false, false, RowVersion(), ""),
            TestCancellation);

        Assert.True(result.IsFailure);
        Assert.Equal(0, store.Calls);
    }

    private static string RowVersion() => Convert.ToBase64String(new byte[8]);

    private static CatalogProductDetails Details(
        IEnumerable<CatalogBarcodeView>? barcodes = null,
        IEnumerable<CatalogPriceHistoryView>? history = null) =>
        new(
            1, "Produit Demo", "Boîte", 1, "Demo", "DEMO-UNIT-001",
            true, true, true, true, false, false, false, 0, null, null, null,
            10m, "MAD", DateTime.UtcNow, DateTime.UtcNow, RowVersion(), RowVersion(),
            barcodes ?? Array.Empty<CatalogBarcodeView>(),
            history ?? Array.Empty<CatalogPriceHistoryView>());

    private sealed class StubProductReader : ICatalogProductReader
    {
        public int Calls { get; private set; }
        public CatalogProductSearchCriteria? Criteria { get; private set; }

        public Task<Result<IReadOnlyList<CatalogProductListItem>>> SearchAsync(
            CatalogProductSearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Criteria = criteria;
            return Task.FromResult(Result<IReadOnlyList<CatalogProductListItem>>.Success(
                Array.Empty<CatalogProductListItem>()));
        }

        public Task<Result<CatalogProductDetails>> GetByIdAsync(
            long productId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<CatalogProductDetails>.Success(Details()));
    }

    private sealed class StubProductStore : ICatalogProductStore
    {
        public int Calls { get; private set; }

        private Task<Result<CatalogProductDetails>> Success()
        {
            Calls++;
            return Task.FromResult(Result<CatalogProductDetails>.Success(Details()));
        }

        public Task<Result<CatalogProductDetails>> CreateAsync(CreateCatalogProductCommand command, CancellationToken cancellationToken = default) => Success();
        public Task<Result<CatalogProductDetails>> UpdateAsync(UpdateCatalogProductCommand command, CancellationToken cancellationToken = default) => Success();
        public Task<Result<CatalogProductDetails>> SetActivationAsync(SetCatalogProductActivationCommand command, CancellationToken cancellationToken = default) => Success();
        public Task<Result<CatalogProductDetails>> AddBarcodeAsync(AddCatalogBarcodeCommand command, CancellationToken cancellationToken = default) => Success();
        public Task<Result<CatalogProductDetails>> UpdateBarcodeAsync(UpdateCatalogBarcodeCommand command, CancellationToken cancellationToken = default) => Success();
        public Task<Result<CatalogProductDetails>> ChangePriceAsync(ChangeCatalogProductPriceCommand command, CancellationToken cancellationToken = default) => Success();
    }

    private sealed class StubCategoryStore : ICatalogCategoryStore
    {
        public int Calls { get; private set; }

        private Task<Result<CatalogCategoryView>> Success()
        {
            Calls++;
            return Task.FromResult(Result<CatalogCategoryView>.Success(
                new CatalogCategoryView(1, "DEMO", "Demo", true, false, false, false, 0, RowVersion())));
        }

        public Task<Result<CatalogCategoryView>> CreateAsync(CreateCatalogCategoryCommand command, CancellationToken cancellationToken = default) => Success();
        public Task<Result<CatalogCategoryView>> UpdateAsync(UpdateCatalogCategoryCommand command, CancellationToken cancellationToken = default) => Success();
        public Task<Result<CatalogCategoryView>> SetActivationAsync(SetCatalogCategoryActivationCommand command, CancellationToken cancellationToken = default) => Success();
    }
}
