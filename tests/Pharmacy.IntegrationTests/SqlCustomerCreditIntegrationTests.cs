using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Customers.Commands;
using Pharmacy.Application.Customers.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Commands;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Finalization.Models;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Catalog;
using Pharmacy.Infrastructure.Inventory;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Sales;
using Pharmacy.Infrastructure.Sales.InMemory;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class SqlCustomerCreditIntegrationTests
{
    private const string TestDatabasePrefix = "Pharmacy_CRD_Test_";

    [Fact]
    public async Task CRD001_Through_CRD024_RealSqlCreditGatePasses()
    {
        var token = TestContext.Current.CancellationToken;
        var server = ServerConnection();
        var databaseName = TestDatabasePrefix + Guid.NewGuid().ToString("N")[..12];
        ValidateTemporaryDatabaseName(databaseName);
        var database = new SqlConnectionStringBuilder(server.ConnectionString)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Pharmacy.CRD.Tests",
            Pooling = false
        };
        await CreateDatabaseAsync(server.ConnectionString, databaseName);

        try
        {
            var factory = new TestDbContextFactory(database.ConnectionString);
            await using (var context = await factory.CreateDbContextAsync(token))
            {
                await context.Database.MigrateAsync(token);
                Assert.Empty(await context.Database.GetPendingMigrationsAsync(token));
                Assert.Equal(13, (await context.Database.GetAppliedMigrationsAsync(token)).Count());
            }

            var clock = new FixedClock(
                new DateOnly(2026, 8, 16),
                new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc));
            var product = await CreateProductAndStockAsync(factory, clock, token);
            var catalog = new SqlProductCatalogReader(factory);
            var availability = new SqlInventoryStockAvailabilityReader(factory, clock);
            var finalizer = new SqlSaleFinalizer(factory, clock);
            var customers = new SqlCustomerCreditCashStore(factory, clock);

            var noCreditCustomer = await customers.CreateAsync(
                new CreateCustomerRequest(Guid.NewGuid(), "CLIENT-QA-01", null, null, false),
                token);
            var creditCustomer = await customers.CreateAsync(
                new CreateCustomerRequest(Guid.NewGuid(), "CLIENT-QA-CREDIT-01", null, null, true),
                token);
            Assert.True(noCreditCustomer.IsSuccess);
            Assert.True(creditCustomer.IsSuccess);

            var deniedPreparation = await PrepareAsync(
                Guid.NewGuid(),
                await CreateDraftAsync(product.ProductId, 5, catalog, availability, token),
                availability,
                token);
            var denied = await finalizer.FinalizeAsync(
                MixedRequest(deniedPreparation, noCreditCustomer.Value.CustomerId, 40m, 60m),
                token);
            Assert.Equal("sales.credit.not_allowed", denied.Error.Code);
            await AssertSaleMutationCountsAsync(database.ConnectionString, 0, 0, 0, 0, 0);
            Assert.Equal(30, await StockAsync(database.ConnectionString, product.ProductId));

            var invalidPreparation = await PrepareAsync(
                Guid.NewGuid(),
                await CreateDraftAsync(product.ProductId, 5, catalog, availability, token),
                availability,
                token);
            var invalidRequests = new[]
            {
                new FinalizeSaleRequest(
                    invalidPreparation,
                    new SalePaymentRequest(SalePaymentMethod.Cash, -1m, 0m, 1m, false),
                    new SaleCreditRequest(creditCustomer.Value.CustomerId, 101m)),
                new FinalizeSaleRequest(
                    invalidPreparation,
                    new SalePaymentRequest(SalePaymentMethod.Cash, 101m, 101m, 0m, false)),
                new FinalizeSaleRequest(
                    invalidPreparation,
                    new SalePaymentRequest(SalePaymentMethod.Cash, 101m, 101m, 0m, false),
                    new SaleCreditRequest(creditCustomer.Value.CustomerId, -1m)),
                new FinalizeSaleRequest(
                    invalidPreparation,
                    new SalePaymentRequest(SalePaymentMethod.Cash, 40m, 40m, 0m, false),
                    new SaleCreditRequest(creditCustomer.Value.CustomerId, 50m)),
                new FinalizeSaleRequest(
                    invalidPreparation,
                    new SalePaymentRequest(SalePaymentMethod.Cash, 0m, 0m, 0m, false)),
                new FinalizeSaleRequest(
                    invalidPreparation,
                    new SalePaymentRequest(SalePaymentMethod.Cash, 39.99999m, 39.99999m, 0m, false),
                    new SaleCreditRequest(creditCustomer.Value.CustomerId, 60.00001m)),
                new FinalizeSaleRequest(
                    invalidPreparation,
                    new SalePaymentRequest(SalePaymentMethod.Cash, 40m, 40m, 0m, false),
                    new SaleCreditRequest(999_999, 60m))
            };
            foreach (var invalidRequest in invalidRequests)
            {
                Assert.True((await finalizer.FinalizeAsync(invalidRequest, token)).IsFailure);
            }
            await AssertSaleMutationCountsAsync(database.ConnectionString, 0, 0, 0, 0, 0);
            Assert.Equal(30, await StockAsync(database.ConnectionString, product.ProductId));

            var partialSale = await CreateCreditSaleAsync(
                finalizer,
                product.ProductId,
                5,
                creditCustomer.Value.CustomerId,
                40m,
                60m,
                catalog,
                availability,
                token);
            Assert.Equal(100m, partialSale.Total.Amount);
            Assert.Equal(40m, partialSale.Payment.Amount.Amount);
            Assert.Equal(25, await StockAsync(database.ConnectionString, product.ProductId));
            Assert.Equal(1, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM sales.Payments WHERE SaleId=@sale AND Amount=40 AND PaymentMethod=N'Cash';",
                new SqlParameter("@sale", SqlDbType.BigInt) { Value = partialSale.SaleId }));
            Assert.Equal(1, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM customers.CustomerLedgerEntries WHERE CustomerId=@customer AND EntryType=N'CreditSale' AND Amount=60;",
                new SqlParameter("@customer", SqlDbType.BigInt) { Value = creditCustomer.Value.CustomerId }));
            Assert.Equal(1, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM cash.CashLedgerEntries WHERE EntryType=N'SaleCashIn' AND Amount=40;"));
            Assert.Equal(1, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM sales.SaleLines WHERE SaleId=@sale AND UnitPrice=20 AND Quantity=5 AND LineTotal=100;",
                new SqlParameter("@sale", SqlDbType.BigInt) { Value = partialSale.SaleId }));

            var pureCreditSale = await CreateCreditSaleAsync(
                finalizer,
                product.ProductId,
                1,
                creditCustomer.Value.CustomerId,
                0m,
                20m,
                catalog,
                availability,
                token);
            Assert.Equal(0m, pureCreditSale.Payment.Amount.Amount);
            Assert.Equal(1, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM cash.CashLedgerEntries WHERE EntryType=N'SaleCashIn';"));
            Assert.Equal(80m, (await customers.GetByIdAsync(creditCustomer.Value.CustomerId, token)).Value.CreditBalance.Amount);

            var firstPaymentOperation = Guid.NewGuid();
            var firstPayment = await customers.RegisterPaymentAsync(
                Payment(firstPaymentOperation, creditCustomer.Value.CustomerId, 20m),
                token);
            Assert.True(firstPayment.IsSuccess);
            Assert.Equal(60m, firstPayment.Value.CreditBalance.Amount);
            var ledgerBeforeFinalPayment = firstPayment.Value.Ledger.ToArray();

            var overpaymentOperation = Guid.NewGuid();
            var overpayment = await customers.RegisterPaymentAsync(
                Payment(overpaymentOperation, creditCustomer.Value.CustomerId, 61m),
                token);
            Assert.Equal("customers.credit.payment.invalid", overpayment.Error.Code);
            Assert.Equal(0, await CountOperationAsync(database.ConnectionString, "customers.CustomerLedgerEntries", overpaymentOperation));
            Assert.Equal(0, await CountCashByReferenceAsync(database.ConnectionString, overpaymentOperation));

            var finalPaymentOperation = Guid.NewGuid();
            var externalCardPayment = Payment(
                finalPaymentOperation,
                creditCustomer.Value.CustomerId,
                60m,
                SalePaymentMethod.ExternalCard,
                true);
            var finalPayment = await customers.RegisterPaymentAsync(externalCardPayment, token);
            Assert.True(finalPayment.IsSuccess);
            Assert.Equal(0m, finalPayment.Value.CreditBalance.Amount);
            Assert.Equal(0, await CountCashByReferenceAsync(database.ConnectionString, finalPaymentOperation));
            Assert.All(ledgerBeforeFinalPayment, before =>
                Assert.Contains(finalPayment.Value.Ledger, after => after == before));

            var replay = await customers.RegisterPaymentAsync(externalCardPayment, token);
            Assert.True(replay.IsSuccess);
            Assert.Equal(1, await CountOperationAsync(
                database.ConnectionString,
                "customers.CustomerLedgerEntries",
                finalPaymentOperation));
            var conflictingReplay = await customers.RegisterPaymentAsync(
                externalCardPayment with { Amount = 50m },
                token);
            Assert.Equal("customers.operation.conflict", conflictingReplay.Error.Code);
            var zeroDebt = await customers.RegisterPaymentAsync(
                Payment(Guid.NewGuid(), creditCustomer.Value.CustomerId, 1m),
                token);
            Assert.Equal("customers.credit.payment.invalid", zeroDebt.Error.Code);

            var reloadedCustomers = new SqlCustomerCreditCashStore(factory, clock);
            var reloaded = await reloadedCustomers.GetByIdAsync(creditCustomer.Value.CustomerId, token);
            Assert.Equal(0m, reloaded.Value.CreditBalance.Amount);
            Assert.Equal(4, reloaded.Value.Ledger.Count);

            var inactiveCustomer = await customers.CreateAsync(
                new CreateCustomerRequest(Guid.NewGuid(), "CLIENT-QA-INACTIVE", null, null, true),
                token);
            Assert.True(inactiveCustomer.IsSuccess);
            await CreateCreditSaleAsync(
                finalizer,
                product.ProductId,
                1,
                inactiveCustomer.Value.CustomerId,
                0m,
                20m,
                catalog,
                availability,
                token);
            var deactivated = await customers.UpdateAsync(
                new UpdateCustomerRequest(
                    inactiveCustomer.Value.CustomerId,
                    inactiveCustomer.Value.FullName,
                    inactiveCustomer.Value.Phone,
                    inactiveCustomer.Value.Note,
                    false,
                    true,
                    inactiveCustomer.Value.RowVersion),
                token);
            Assert.False(deactivated.Value.IsActive);
            var inactiveCreditPreparation = await PrepareAsync(
                Guid.NewGuid(),
                await CreateDraftAsync(product.ProductId, 1, catalog, availability, token),
                availability,
                token);
            Assert.Equal("sales.credit.not_allowed", (await finalizer.FinalizeAsync(
                PureCreditRequest(inactiveCreditPreparation, inactiveCustomer.Value.CustomerId), token)).Error.Code);
            var inactiveDebtPayment = await customers.RegisterPaymentAsync(
                Payment(Guid.NewGuid(), inactiveCustomer.Value.CustomerId, 10m),
                token);
            Assert.True(inactiveDebtPayment.IsSuccess);
            Assert.Equal(10m, inactiveDebtPayment.Value.CreditBalance.Amount);

            var concurrentCustomer = await customers.CreateAsync(
                new CreateCustomerRequest(Guid.NewGuid(), "CLIENT-QA-CONCURRENT", null, null, true),
                token);
            await CreateCreditSaleAsync(
                finalizer,
                product.ProductId,
                5,
                concurrentCustomer.Value.CustomerId,
                0m,
                100m,
                catalog,
                availability,
                token);
            var barrier = new FirstAttemptBarrier(2);
            var concurrentStore = new SqlCustomerCreditCashStore(factory, clock, barrier.SignalAndWaitAsync);
            var concurrentOperations = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var concurrentResults = await Task.WhenAll(
                concurrentStore.RegisterPaymentAsync(
                    Payment(concurrentOperations[0], concurrentCustomer.Value.CustomerId, 70m), token),
                concurrentStore.RegisterPaymentAsync(
                    Payment(concurrentOperations[1], concurrentCustomer.Value.CustomerId, 60m), token));
            Assert.Single(concurrentResults, result => result.IsSuccess);
            Assert.Single(concurrentResults, result => result.IsFailure);
            var concurrentBalance = (await customers.GetByIdAsync(concurrentCustomer.Value.CustomerId, token)).Value.CreditBalance.Amount;
            Assert.Contains(concurrentBalance, new[] { 30m, 40m });
            Assert.Equal(1, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM customers.CustomerLedgerEntries WHERE CustomerId=@customer AND EntryType=N'CreditPayment';",
                new SqlParameter("@customer", SqlDbType.BigInt) { Value = concurrentCustomer.Value.CustomerId }));

            var cashFailureOperation = Guid.NewGuid();
            await CreateFailureTriggerAsync(
                database.ConnectionString,
                "cash.CRD_FailCash",
                "cash.CashLedgerEntries",
                cashFailureOperation,
                token);
            try
            {
                Assert.True((await customers.RegisterPaymentAsync(
                    Payment(cashFailureOperation, concurrentCustomer.Value.CustomerId, 10m), token)).IsFailure);
            }
            finally
            {
                await DropTriggerAsync(database.ConnectionString, "cash.CRD_FailCash", token);
            }
            Assert.Equal(concurrentBalance, (await customers.GetByIdAsync(concurrentCustomer.Value.CustomerId, token)).Value.CreditBalance.Amount);
            Assert.Equal(0, await CountOperationAsync(database.ConnectionString, "customers.CustomerLedgerEntries", cashFailureOperation));
            Assert.Equal(0, await CountCashByReferenceAsync(database.ConnectionString, cashFailureOperation));

            var ledgerFailureOperation = Guid.NewGuid();
            await CreateFailureTriggerAsync(
                database.ConnectionString,
                "customers.CRD_FailLedger",
                "customers.CustomerLedgerEntries",
                ledgerFailureOperation,
                token);
            try
            {
                Assert.True((await customers.RegisterPaymentAsync(
                    Payment(ledgerFailureOperation, concurrentCustomer.Value.CustomerId, 10m), token)).IsFailure);
            }
            finally
            {
                await DropTriggerAsync(database.ConnectionString, "customers.CRD_FailLedger", token);
            }
            Assert.Equal(concurrentBalance, (await customers.GetByIdAsync(concurrentCustomer.Value.CustomerId, token)).Value.CreditBalance.Amount);
            Assert.Equal(0, await CountOperationAsync(database.ConnectionString, "customers.CustomerLedgerEntries", ledgerFailureOperation));
            Assert.Equal(0, await CountCashByReferenceAsync(database.ConnectionString, ledgerFailureOperation));

            var returnCustomer = await customers.CreateAsync(
                new CreateCustomerRequest(Guid.NewGuid(), "CLIENT-QA-RETURN", null, null, true),
                token);
            var returnedSale = await CreateCreditSaleAsync(
                finalizer,
                product.ProductId,
                5,
                returnCustomer.Value.CustomerId,
                40m,
                60m,
                catalog,
                availability,
                token);
            var saleLineId = await ScalarAsync<long>(database.ConnectionString,
                "SELECT Id FROM sales.SaleLines WHERE SaleId=@sale;",
                new SqlParameter("@sale", SqlDbType.BigInt) { Value = returnedSale.SaleId });
            var creditReduction = await customers.ReturnAsync(
                new ReturnSaleRequest(
                    Guid.NewGuid(),
                    returnedSale.SaleId,
                    [new ReturnSaleLineRequest(saleLineId, 1, ReturnStockDestination.NotRestockable)],
                    ReturnRefundMethod.CreditReduction),
                token);
            Assert.True(creditReduction.IsSuccess);
            Assert.Equal(20m, creditReduction.Value.RefundAmount.Amount);
            Assert.Equal(40m, (await customers.GetByIdAsync(returnCustomer.Value.CustomerId, token)).Value.CreditBalance.Amount);
            Assert.Equal(0, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM cash.CashLedgerEntries WHERE SourceType=N'SaleReturn' AND SourceReference=@reference;",
                new SqlParameter("@reference", SqlDbType.NVarChar, 128) { Value = creditReduction.Value.Reference }));
            var excessiveReduction = await customers.ReturnAsync(
                new ReturnSaleRequest(
                    Guid.NewGuid(),
                    returnedSale.SaleId,
                    [new ReturnSaleLineRequest(saleLineId, 3, ReturnStockDestination.NotRestockable)],
                    ReturnRefundMethod.CreditReduction),
                token);
            Assert.Equal("sales.return.invalid", excessiveReduction.Error.Code);
            Assert.Equal(1, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM sales.SaleReturns WHERE OriginalSaleId=@sale;",
                new SqlParameter("@sale", SqlDbType.BigInt) { Value = returnedSale.SaleId }));
            Assert.Equal(40m, (await customers.GetByIdAsync(returnCustomer.Value.CustomerId, token)).Value.CreditBalance.Amount);

            Assert.Equal(0, await ScalarAsync<int>(database.ConnectionString,
                "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'sales.Payments') AND name IN (N'PAN',N'CVV',N'CardNumber',N'CardToken');"));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task RBSELL020_UnauthorizedCustomerMutationsLeaveSqlUnchanged()
    {
        var token = TestContext.Current.CancellationToken;
        var server = ServerConnection();
        var databaseName = TestDatabasePrefix + Guid.NewGuid().ToString("N")[..12];
        ValidateTemporaryDatabaseName(databaseName);
        var database = new SqlConnectionStringBuilder(server.ConnectionString)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Pharmacy.RBSELL020.Tests",
            Pooling = false
        };
        await CreateDatabaseAsync(server.ConnectionString, databaseName);

        try
        {
            var factory = new TestDbContextFactory(database.ConnectionString);
            await using (var context = await factory.CreateDbContextAsync(token))
            {
                await context.Database.MigrateAsync(token);
            }

            var clock = new FixedClock(
                new DateOnly(2026, 8, 22),
                new DateTime(2026, 8, 22, 8, 0, 0, DateTimeKind.Utc));
            var store = new SqlCustomerCreditCashStore(factory, clock);
            var customer = await store.CreateAsync(
                new CreateCustomerRequest(Guid.NewGuid(), "Client RBAC Seller", null, null, false), token);
            Assert.True(customer.IsSuccess);

            var beforeCustomers = await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM customers.Customers;");
            var beforeLedger = await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM customers.CustomerLedgerEntries;");
            var seller = new TestPermissionAuthorizer(
                SecurityPermissions.CatalogView,
                SecurityPermissions.InventoryView,
                SecurityPermissions.CustomersView,
                SecurityPermissions.SalesCreate,
                SecurityPermissions.SalesViewHistory);
            var commands = new CustomerCommandHandlers(store, store, seller);

            var create = await commands.CreateAsync(
                new CreateCustomerRequest(Guid.NewGuid(), "Création interdite", null, null, false), token);
            var update = await commands.UpdateAsync(
                new UpdateCustomerRequest(
                    customer.Value.CustomerId, "Modification interdite", null, null,
                    true, false, customer.Value.RowVersion), token);
            var credit = await commands.UpdateAsync(
                new UpdateCustomerRequest(
                    customer.Value.CustomerId, customer.Value.FullName, null, null,
                    true, true, customer.Value.RowVersion), token);
            var payment = await commands.RegisterCreditPaymentAsync(
                new RegisterCreditPaymentRequest(
                    Guid.NewGuid(), customer.Value.CustomerId, 10m,
                    SalePaymentMethod.Cash, false), token);

            Assert.All(new[] { create, update, credit, payment }, result =>
                Assert.Equal("security.authorization.denied", result.Error.Code));
            Assert.Equal(beforeCustomers, await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM customers.Customers;"));
            Assert.Equal(beforeLedger, await ScalarAsync<int>(database.ConnectionString, "SELECT COUNT(*) FROM customers.CustomerLedgerEntries;"));
            Assert.Equal("Client RBAC Seller", await ScalarAsync<string>(
                database.ConnectionString,
                "SELECT FullName FROM customers.Customers WHERE Id=@id;",
                new SqlParameter("@id", SqlDbType.BigInt) { Value = customer.Value.CustomerId }));
            Assert.Equal(0, await ScalarAsync<int>(
                database.ConnectionString,
                "SELECT CONVERT(int, CreditAllowed) FROM customers.Customers WHERE Id=@id;",
                new SqlParameter("@id", SqlDbType.BigInt) { Value = customer.Value.CustomerId }));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }

    private static async Task<(long ProductId, long LocationId)> CreateProductAndStockAsync(
        IDbContextFactory<PharmacyDbContext> factory,
        FixedClock clock,
        CancellationToken token)
    {
        var catalogStore = new SqlCatalogStore(factory);
        var category = await catalogStore.CreateAsync(
            new CreateCatalogCategoryCommand("CRD-QA", "Crédit QA"), token);
        Assert.True(category.IsSuccess);
        var product = await new CreateCatalogProductHandler(catalogStore, TestPermissionAuthorizer.AllowAll).HandleAsync(
            new CreateCatalogProductCommand(
                "Produit CRD",
                "Unité",
                category.Value.CategoryId,
                "CRD-PRODUCT-001",
                20m),
            token);
        Assert.True(product.IsSuccess);
        var inventory = new InventoryCommandHandlers(new SqlInventoryStore(factory), clock);
        var location = await inventory.CreateLocationAsync(
            new CreateStockLocationCommand(Guid.NewGuid(), "CRD-MAIN", "Stock CRD", true), token);
        Assert.True(location.IsSuccess);
        var opening = await inventory.RecordOpeningAsync(
            new RecordStockMovementCommand(
                Guid.NewGuid(),
                StockMovementType.Opening,
                product.Value.ProductId,
                location.Value.LocationId,
                null,
                30,
                "Ouverture CRD",
                "IntegrationTest",
                "CRD"),
            token);
        Assert.True(opening.IsSuccess);
        return (product.Value.ProductId, location.Value.LocationId);
    }

    private static async Task<FinalizedSaleView> CreateCreditSaleAsync(
        ISaleFinalizer finalizer,
        long productId,
        int quantity,
        long customerId,
        decimal immediate,
        decimal credit,
        IProductCatalogReader catalog,
        IStockAvailabilityReader availability,
        CancellationToken token)
    {
        var preparation = await PrepareAsync(
            Guid.NewGuid(),
            await CreateDraftAsync(productId, quantity, catalog, availability, token),
            availability,
            token);
        var result = await finalizer.FinalizeAsync(
            MixedRequest(preparation, customerId, immediate, credit), token);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    private static FinalizeSaleRequest MixedRequest(
        SaleFinalizationPreparation preparation,
        long customerId,
        decimal immediate,
        decimal credit) =>
        new(
            preparation,
            new SalePaymentRequest(SalePaymentMethod.Cash, immediate, immediate, 0m, false),
            new SaleCreditRequest(customerId, credit));

    private static FinalizeSaleRequest PureCreditRequest(
        SaleFinalizationPreparation preparation,
        long customerId) =>
        MixedRequest(preparation, customerId, 0m, preparation.Total.Amount);

    private static RegisterCreditPaymentRequest Payment(
        Guid operationId,
        long customerId,
        decimal amount,
        SalePaymentMethod method = SalePaymentMethod.Cash,
        bool externalCardConfirmed = false) =>
        new(operationId, customerId, amount, method, externalCardConfirmed);

    private static async Task<DraftScenario> CreateDraftAsync(
        long productId,
        int quantity,
        IProductCatalogReader catalog,
        IStockAvailabilityReader availability,
        CancellationToken token)
    {
        var store = new InMemorySaleDraftStore();
        var draft = await new CreateSaleDraftHandler(new InMemorySaleDraftIdGenerator(), store).HandleAsync(token);
        var added = await new AddProductToSaleDraftHandler(store, catalog, availability).HandleAsync(
            new AddProductToSaleDraftCommand(
                draft.Value.Id,
                SellableProductLookup.ByProductId(ProductId.Create(productId).Value),
                SaleQuantity.Create(quantity).Value,
                draft.Value.Version),
            token);
        Assert.True(added.IsSuccess);
        return new DraftScenario(store, added.Value);
    }

    private static async Task<SaleFinalizationPreparation> PrepareAsync(
        Guid operationId,
        DraftScenario scenario,
        IStockAvailabilityReader availability,
        CancellationToken token)
    {
        var result = await new PrepareSaleFinalizationHandler(
            scenario.Store,
            availability,
            new InMemorySaleFinalizationPreparationStore()).HandleAsync(
                new PrepareSaleFinalizationCommand(operationId, scenario.Draft.Id, scenario.Draft.Version),
                token);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task AssertSaleMutationCountsAsync(
        string connectionString,
        int sales,
        int payments,
        int customerLedger,
        int cashLedger,
        int saleMovements)
    {
        Assert.Equal(sales, await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM sales.Sales;"));
        Assert.Equal(payments, await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM sales.Payments;"));
        Assert.Equal(customerLedger, await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM customers.CustomerLedgerEntries;"));
        Assert.Equal(cashLedger, await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM cash.CashLedgerEntries;"));
        Assert.Equal(saleMovements, await ScalarAsync<int>(connectionString,
            "SELECT COUNT(*) FROM inventory.StockMovements WHERE MovementType=N'Sale';"));
    }

    private static Task<int> StockAsync(string connectionString, long productId) =>
        ScalarAsync<int>(connectionString,
            "SELECT AvailableQuantity FROM inventory.StockBalances WHERE ProductId=@product AND StockBatchId IS NULL;",
            new SqlParameter("@product", SqlDbType.BigInt) { Value = productId });

    private static Task<int> CountOperationAsync(
        string connectionString,
        string table,
        Guid operationId) =>
        ScalarAsync<int>(connectionString,
            $"SELECT COUNT(*) FROM {table} WHERE OperationId=@operation;",
            new SqlParameter("@operation", SqlDbType.UniqueIdentifier) { Value = operationId });

    private static Task<int> CountCashByReferenceAsync(string connectionString, Guid operationId) =>
        ScalarAsync<int>(connectionString,
            "SELECT COUNT(*) FROM cash.CashLedgerEntries WHERE SourceReference LIKE @fragment;",
            new SqlParameter("@fragment", SqlDbType.NVarChar, 128) { Value = $"%{operationId:N}%" });

    private static async Task CreateFailureTriggerAsync(
        string connectionString,
        string triggerName,
        string tableName,
        Guid operationId,
        CancellationToken token)
    {
        await ExecuteAsync(
            connectionString,
            $"CREATE TRIGGER {triggerName} ON {tableName} AFTER INSERT AS " +
            $"BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE OperationId=CONVERT(uniqueidentifier,N'{operationId:D}')) " +
            "THROW 51000, 'CRD rollback probe', 1; END;",
            token);
    }

    private static Task DropTriggerAsync(string connectionString, string triggerName, CancellationToken token) =>
        ExecuteAsync(connectionString, $"DROP TRIGGER IF EXISTS {triggerName};", token);

    private static SqlConnectionStringBuilder ServerConnection()
    {
        var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("PHARMACY_TEST_SQLSERVER_CONNECTION est requise.");
        }

        return new SqlConnectionStringBuilder(raw)
        {
            InitialCatalog = "master",
            ApplicationName = "Pharmacy.CRD.Tests",
            Pooling = false
        };
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        ValidateTemporaryDatabaseName(databaseName);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE {Quote(databaseName)};";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string connectionString, string databaseName)
    {
        ValidateTemporaryDatabaseName(databaseName);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"IF DB_ID(@name) IS NOT NULL BEGIN ALTER DATABASE {Quote(databaseName)} " +
            $"SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {Quote(databaseName)}; END;";
        command.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = databaseName });
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(
        string connectionString,
        string sql,
        params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("La requête CRD n'a retourné aucune valeur.");
        return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(
        string connectionString,
        string sql,
        CancellationToken token,
        params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync(token);
    }

    private static string Quote(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static void ValidateTemporaryDatabaseName(string databaseName)
    {
        if (!Regex.IsMatch(databaseName, "^Pharmacy_CRD_Test_[0-9a-f]{12}$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("Refus d'opérer sur une base hors préfixe CRD temporaire.");
        }
    }

    private sealed record DraftScenario(InMemorySaleDraftStore Store, SaleDraftView Draft);

    private sealed class FirstAttemptBarrier(int participants)
    {
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public Task SignalAndWaitAsync(CancellationToken token)
        {
            var arrival = Interlocked.Increment(ref arrivals);
            if (arrival > participants)
            {
                return Task.CompletedTask;
            }

            if (arrival == participants)
            {
                completion.TrySetResult();
            }

            return completion.Task.WaitAsync(token);
        }
    }

    private sealed class FixedClock(DateOnly currentDate, DateTime utcNow) : ISalesClock, IInventoryDateProvider
    {
        public DateOnly CurrentDate { get; } = currentDate;
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class TestDbContextFactory : IDbContextFactory<PharmacyDbContext>
    {
        private readonly DbContextOptions<PharmacyDbContext> options;

        public TestDbContextFactory(string connectionString)
        {
            var builder = new DbContextOptionsBuilder<PharmacyDbContext>();
            builder.UsePharmacySqlServer(connectionString);
            options = builder.Options;
        }

        public PharmacyDbContext CreateDbContext() => new(options);

        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
