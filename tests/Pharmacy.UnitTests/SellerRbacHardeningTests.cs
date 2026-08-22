using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Catalog.Contracts;
using Pharmacy.Application.Catalog.Models;
using Pharmacy.Application.Catalog.Queries;
using Pharmacy.Application.Customers.Commands;
using Pharmacy.Application.Customers.Contracts;
using Pharmacy.Application.Customers.Models;
using Pharmacy.Application.Customers.Queries;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class SellerRbacHardeningTests
{
    private static readonly CancellationToken Token = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RBSELL001_CatalogViewCanListProducts()
    {
        var reader = new CatalogReader();
        var result = await new SearchCatalogProductsHandler(
            reader,
            new TestPermissionAuthorizer(SecurityPermissions.CatalogView))
            .HandleAsync(new SearchCatalogProductsQuery(), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, reader.SearchCalls);
    }

    [Fact]
    public async Task RBSELL002_CatalogViewCanReadProductDetails()
    {
        var reader = new CatalogReader();
        var result = await new GetCatalogProductByIdHandler(
            reader,
            new TestPermissionAuthorizer(SecurityPermissions.CatalogView))
            .HandleAsync(new GetCatalogProductByIdQuery(1), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, reader.GetCalls);
    }

    [Fact]
    public async Task RBSELL003_SellerCannotCreateProductWithoutCatalogManage()
    {
        var store = new CatalogStore();
        var result = await new CreateCatalogProductHandler(store, Seller())
            .HandleAsync(CreateProduct(), Token);

        AssertDenied(result);
        Assert.Equal(0, store.MutationCalls);
    }

    [Fact]
    public async Task RBSELL004_SellerCannotUpdateProduct()
    {
        var store = new CatalogStore();
        var result = await new UpdateCatalogProductHandler(store, Seller())
            .HandleAsync(UpdateProduct(), Token);

        AssertDenied(result);
        Assert.Equal(0, store.MutationCalls);
    }

    [Fact]
    public async Task RBSELL005_SellerCannotDeactivateOrReactivateProduct()
    {
        var store = new CatalogStore();
        var handler = new SetCatalogProductActivationHandler(store, Seller());

        AssertDenied(await handler.HandleAsync(new(1, false, false, RowVersion(), RowVersion()), Token));
        AssertDenied(await handler.HandleAsync(new(1, true, true, RowVersion(), RowVersion()), Token));
        Assert.Equal(0, store.MutationCalls);
    }

    [Fact]
    public async Task RBSELL006_SellerCannotAddBarcode()
    {
        var store = new CatalogStore();
        var result = await new AddCatalogBarcodeHandler(store, Seller())
            .HandleAsync(new AddCatalogBarcodeCommand(1, "SELLER-BARCODE", true), Token);

        AssertDenied(result);
        Assert.Equal(0, store.MutationCalls);
    }

    [Fact]
    public async Task RBSELL007_SellerCannotDeactivateBarcode()
    {
        var store = new CatalogStore();
        var result = await new UpdateCatalogBarcodeHandler(store, Seller())
            .HandleAsync(new UpdateCatalogBarcodeCommand(1, 1, "SELLER-BARCODE", true, false, RowVersion()), Token);

        AssertDenied(result);
        Assert.Equal(0, store.MutationCalls);
    }

    [Fact]
    public async Task RBSELL008_SellerCannotChangePriceWithoutCatalogChangePrice()
    {
        var store = new CatalogStore();
        var result = await new ChangeCatalogProductPriceHandler(store, Seller())
            .HandleAsync(new ChangeCatalogProductPriceCommand(1, 20m, null, RowVersion()), Token);

        AssertDenied(result);
        Assert.Equal(0, store.MutationCalls);
    }

    [Fact]
    public void RBSELL010_UiHidesNewProductForReadOnlySeller()
    {
        var source = DesktopSource("Components", "Pages", "CatalogPage.razor");

        Assert.Contains("@if (CanManageCatalog)", source, StringComparison.Ordinal);
        Assert.Contains("CanManageCatalog => Authorizer.Authorize(SecurityPermissions.CatalogManage).IsSuccess", source, StringComparison.Ordinal);
        Assert.Contains("if (!CanManageCatalog) return", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RBSELL011_UiKeepsProductDetailsReadOnlyForSeller()
    {
        var source = DesktopSource("Components", "Pages", "CatalogPage.razor");

        Assert.Contains("disabled=\"@(Catalog.IsBusy || !CanManageCatalog)\"", source, StringComparison.Ordinal);
        Assert.Contains("@if (CanChangePrice)", source, StringComparison.Ordinal);
        Assert.Contains("@product.CurrentSalePrice", source, StringComparison.Ordinal);
        Assert.Contains("@foreach (var history in product.PriceHistory)", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RBSELL012_CatalogManagerRetainsManagement()
    {
        var store = new CatalogStore();
        var result = await new CreateCatalogProductHandler(
            store,
            new TestPermissionAuthorizer(SecurityPermissions.CatalogManage))
            .HandleAsync(CreateProduct(), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, store.MutationCalls);
    }

    [Fact]
    public async Task RBSELL013_CatalogChangePriceAllowsOnlyPriceChange()
    {
        var store = new CatalogStore();
        var authorizer = new TestPermissionAuthorizer(SecurityPermissions.CatalogChangePrice);

        var price = await new ChangeCatalogProductPriceHandler(store, authorizer)
            .HandleAsync(new ChangeCatalogProductPriceCommand(1, 20m, null, RowVersion()), Token);
        var update = await new UpdateCatalogProductHandler(store, authorizer)
            .HandleAsync(UpdateProduct(), Token);

        Assert.True(price.IsSuccess);
        AssertDenied(update);
        Assert.Equal(1, store.MutationCalls);
    }

    [Fact]
    public async Task RBSELL014_CustomersViewAllowsSearchAndRead()
    {
        var store = new CustomerStore();
        var queries = new CustomerQueryHandlers(
            store,
            new TestPermissionAuthorizer(SecurityPermissions.CustomersView));

        Assert.True((await queries.SearchAsync(new CustomerSearchQuery(null), Token)).IsSuccess);
        Assert.True((await queries.GetByIdAsync(1, Token)).IsSuccess);
        Assert.Equal(2, store.ReadCalls);
    }

    [Fact]
    public async Task RBSELL015_CustomersViewAloneCannotCreateCustomer()
    {
        var store = new CustomerStore();
        var result = await new CustomerCommandHandlers(store, store, Seller())
            .CreateAsync(new CreateCustomerRequest(Guid.NewGuid(), "Client Seller", null, null, false), Token);

        AssertDenied(result);
        Assert.Equal(0, store.MutationCalls);
    }

    [Fact]
    public async Task RBSELL016_CustomersViewAloneCannotModifyCustomer()
    {
        var store = new CustomerStore();
        var result = await new CustomerCommandHandlers(store, store, Seller())
            .UpdateAsync(UpdateCustomer(false), Token);

        AssertDenied(result);
        Assert.Equal(0, store.ReadCalls);
        Assert.Equal(0, store.MutationCalls);
    }

    [Fact]
    public async Task RBSELL017_CustomersViewAloneCannotChangeCreditAuthorization()
    {
        var store = new CustomerStore();
        var result = await new CustomerCommandHandlers(store, store, Seller())
            .UpdateAsync(UpdateCustomer(true), Token);

        AssertDenied(result);
        Assert.Equal(0, store.MutationCalls);
    }

    [Fact]
    public async Task RBSELL018_CustomersManageAllowsCustomerManagementWithoutCreditMutation()
    {
        var store = new CustomerStore();
        var commands = new CustomerCommandHandlers(
            store,
            store,
            new TestPermissionAuthorizer(SecurityPermissions.CustomersManage));

        Assert.True((await commands.CreateAsync(
            new CreateCustomerRequest(Guid.NewGuid(), "Client Manager", null, null, false), Token)).IsSuccess);
        Assert.True((await commands.UpdateAsync(UpdateCustomer(false), Token)).IsSuccess);
        Assert.Equal(2, store.MutationCalls);
    }

    [Fact]
    public async Task RBSELL019_CustomersManageCreditIsRequiredForCreditMutations()
    {
        var store = new CustomerStore();
        var manageOnly = new CustomerCommandHandlers(
            store,
            store,
            new TestPermissionAuthorizer(SecurityPermissions.CustomersManage));
        var full = new CustomerCommandHandlers(
            store,
            store,
            new TestPermissionAuthorizer(
                SecurityPermissions.CustomersManage,
                SecurityPermissions.CustomersManageCredit));

        AssertDenied(await manageOnly.CreateAsync(
            new CreateCustomerRequest(Guid.NewGuid(), "Crédit interdit", null, null, true), Token));
        AssertDenied(await manageOnly.UpdateAsync(UpdateCustomer(true), Token));
        AssertDenied(await manageOnly.RegisterCreditPaymentAsync(Payment(), Token));
        Assert.True((await full.CreateAsync(
            new CreateCustomerRequest(Guid.NewGuid(), "Crédit autorisé", null, null, true), Token)).IsSuccess);
        Assert.True((await full.UpdateAsync(UpdateCustomer(true), Token)).IsSuccess);
        Assert.Equal(2, store.MutationCalls);
    }

    [Fact]
    public void RBSELL021_CustomersUiHidesMutationsForReadOnlySeller()
    {
        var source = DesktopSource("Components", "Pages", "CustomersPage.razor");

        Assert.Contains("@if (CanManageCustomers)", source, StringComparison.Ordinal);
        Assert.Contains("@if (CanManageCustomerCredit)", source, StringComparison.Ordinal);
        Assert.Contains("if (!CanManageCustomers) return", source, StringComparison.Ordinal);
        Assert.Contains("if (!CanManageCustomerCredit) return", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RBSELL022_CatalogViewCanListCategories()
    {
        var reader = new CatalogReader();
        var result = await new ListCatalogCategoriesHandler(
            reader,
            new TestPermissionAuthorizer(SecurityPermissions.CatalogView))
            .HandleAsync(Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, reader.CategoryCalls);
    }

    [Fact]
    public async Task RBSELL023_CatalogReadsRequireCatalogView()
    {
        var reader = new CatalogReader();
        var denied = new TestPermissionAuthorizer();

        AssertDenied(await new SearchCatalogProductsHandler(reader, denied)
            .HandleAsync(new SearchCatalogProductsQuery(), Token));
        AssertDenied(await new GetCatalogProductByIdHandler(reader, denied)
            .HandleAsync(new GetCatalogProductByIdQuery(1), Token));
        AssertDenied(await new ListCatalogCategoriesHandler(reader, denied).HandleAsync(Token));
        Assert.Equal(0, reader.SearchCalls);
        Assert.Equal(0, reader.GetCalls);
        Assert.Equal(0, reader.CategoryCalls);
    }

    private static IPermissionAuthorizer Seller() => new TestPermissionAuthorizer(
        SecurityPermissions.CatalogView,
        SecurityPermissions.InventoryView,
        SecurityPermissions.CustomersView,
        SecurityPermissions.SalesCreate,
        SecurityPermissions.SalesViewHistory);

    private static CreateCatalogProductCommand CreateProduct() =>
        new("Produit RBAC", "Boîte", 1, "RBSELL-PRODUCT", 10m);

    private static UpdateCatalogProductCommand UpdateProduct() =>
        new(1, "Produit modifié", "Boîte", 1, "RBSELL-PRODUCT", true, true,
            false, false, false, 0, null, null, null, RowVersion(), RowVersion());

    private static UpdateCustomerRequest UpdateCustomer(bool creditAllowed) =>
        new(1, "Client modifié", null, null, true, creditAllowed, new byte[8]);

    private static RegisterCreditPaymentRequest Payment() =>
        new(Guid.NewGuid(), 1, 10m, SalePaymentMethod.Cash, false);

    private static string RowVersion() => Convert.ToBase64String(new byte[8]);

    private static void AssertDenied<T>(Result<T> result) =>
        Assert.Equal("security.authorization.denied", result.Error.Code);

    private static string DesktopSource(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(
            directory.FullName,
            "src",
            "Pharmacy.Desktop",
            Path.Combine(segments)));
    }

    private static CatalogProductDetails ProductDetails() => new(
        1, "Produit RBAC", "Boîte", 1, "Catégorie", "RBSELL-PRODUCT",
        true, true, true, true, false, false, false, 0, null, null, null,
        10m, "MAD", DateTime.UtcNow, DateTime.UtcNow, RowVersion(), RowVersion(),
        [new CatalogBarcodeView(1, "RBSELL-BARCODE", "RBSELL-BARCODE", true, "Demo", true, DateTime.UtcNow, RowVersion())],
        Array.Empty<CatalogPriceHistoryView>());

    private static CustomerDetailView CustomerDetails(bool creditAllowed = false) => new(
        1, "CLIENT-RBSELL", "Client RBAC", null, null, true, creditAllowed,
        Money.Create(20m).Value, DateTime.UtcNow, DateTime.UtcNow, new byte[8],
        Array.Empty<CustomerLedgerEntryView>());

    private sealed class CatalogReader : ICatalogProductReader, ICatalogCategoryReader
    {
        public int SearchCalls { get; private set; }
        public int GetCalls { get; private set; }
        public int CategoryCalls { get; private set; }

        public Task<Result<IReadOnlyList<CatalogProductListItem>>> SearchAsync(
            CatalogProductSearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            return Task.FromResult(Result<IReadOnlyList<CatalogProductListItem>>.Success(
                Array.Empty<CatalogProductListItem>()));
        }

        public Task<Result<CatalogProductDetails>> GetByIdAsync(long productId, CancellationToken cancellationToken = default)
        {
            GetCalls++;
            return Task.FromResult(Result<CatalogProductDetails>.Success(ProductDetails()));
        }

        public Task<Result<IReadOnlyList<CatalogCategoryView>>> ListAsync(CancellationToken cancellationToken = default)
        {
            CategoryCalls++;
            return Task.FromResult(Result<IReadOnlyList<CatalogCategoryView>>.Success(
                Array.Empty<CatalogCategoryView>()));
        }
    }

    private sealed class CatalogStore : ICatalogProductStore
    {
        public int MutationCalls { get; private set; }

        private Task<Result<CatalogProductDetails>> Success()
        {
            MutationCalls++;
            return Task.FromResult(Result<CatalogProductDetails>.Success(ProductDetails()));
        }

        public Task<Result<CatalogProductDetails>> CreateAsync(CreateCatalogProductCommand command, CancellationToken cancellationToken = default) => Success();
        public Task<Result<CatalogProductDetails>> UpdateAsync(UpdateCatalogProductCommand command, CancellationToken cancellationToken = default) => Success();
        public Task<Result<CatalogProductDetails>> SetActivationAsync(SetCatalogProductActivationCommand command, CancellationToken cancellationToken = default) => Success();
        public Task<Result<CatalogProductDetails>> AddBarcodeAsync(AddCatalogBarcodeCommand command, CancellationToken cancellationToken = default) => Success();
        public Task<Result<CatalogProductDetails>> UpdateBarcodeAsync(UpdateCatalogBarcodeCommand command, CancellationToken cancellationToken = default) => Success();
        public Task<Result<CatalogProductDetails>> ChangePriceAsync(ChangeCatalogProductPriceCommand command, CancellationToken cancellationToken = default) => Success();
    }

    private sealed class CustomerStore : ICustomerStore, ICreditPaymentStore
    {
        public int ReadCalls { get; private set; }
        public int MutationCalls { get; private set; }

        public Task<Result<CustomerDetailView>> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
        {
            MutationCalls++;
            return Task.FromResult(Result<CustomerDetailView>.Success(CustomerDetails(request.CreditAllowed)));
        }

        public Task<Result<CustomerDetailView>> UpdateAsync(UpdateCustomerRequest request, CancellationToken cancellationToken = default)
        {
            MutationCalls++;
            return Task.FromResult(Result<CustomerDetailView>.Success(CustomerDetails(request.CreditAllowed)));
        }

        public Task<Result<IReadOnlyList<CustomerSummaryView>>> SearchAsync(CustomerSearchQuery query, CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            return Task.FromResult(Result<IReadOnlyList<CustomerSummaryView>>.Success(Array.Empty<CustomerSummaryView>()));
        }

        public Task<Result<CustomerDetailView>> GetByIdAsync(long customerId, CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            return Task.FromResult(Result<CustomerDetailView>.Success(CustomerDetails()));
        }

        public Task<Result<CustomerDetailView>> RegisterPaymentAsync(RegisterCreditPaymentRequest request, CancellationToken cancellationToken = default)
        {
            MutationCalls++;
            return Task.FromResult(Result<CustomerDetailView>.Success(CustomerDetails(true)));
        }
    }
}
