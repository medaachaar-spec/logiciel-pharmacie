using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Catalog.Models;
using Pharmacy.Application.Catalog.Queries;
using Pharmacy.Application.Customers.Commands;
using Pharmacy.Application.Customers.Models;
using Pharmacy.Application.Customers.Queries;
using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Models;
using Pharmacy.Application.Inventory.Queries;
using Pharmacy.Application.Operations.Contracts;
using Pharmacy.Application.Operations.Models;
using Pharmacy.Application.Purchasing.Commands;
using Pharmacy.Application.Purchasing.Models;
using Pharmacy.Application.Purchasing.Queries;
using Pharmacy.Application.Reporting.Contracts;
using Pharmacy.Application.Reporting.Models;
using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Commands;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Finalization.Queries;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Application.Sales.Suspensions;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Purchasing;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Catalog;
using Pharmacy.Infrastructure.Inventory;
using Pharmacy.Infrastructure.Operations;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Purchasing;
using Pharmacy.Infrastructure.Reporting;
using Pharmacy.Infrastructure.Sales;
using Pharmacy.Infrastructure.Sales.InMemory;
using Pharmacy.Infrastructure.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class EndToEndBusinessFlowTests
{
    private const string OwnerLogin = "owner.e2e";
    private const string OwnerPassword = "Owner-E2E-Validation-2026!";

    [Fact]
    public async Task E2E001_CompleteBusinessScenarioPersistsAcrossModulesRestartsBackupAndRestore()
    {
        var token = TestContext.Current.CancellationToken;
        var state = new ScenarioState(DateTime.UtcNow.AddMinutes(-1));
        var environment = await E2ESqlEnvironment.CreateAsync(token);
        E2EApplication? application = null;

        try
        {
            application = E2EApplication.Build(environment.DatabaseConnection);
            await BootstrapSecurityAsync(application, environment, state, token);
            await CreateCatalogAsync(application, state, token);
            await CreatePurchasingFlowAsync(application, environment, state, token);

            await application.DisposeAsync();
            application = E2EApplication.Build(environment.DatabaseConnection);
            await LoginAsync(application, token);
            await AssertPurchasingPersistenceAsync(application, state, token);

            await RunInventoryAsync(application, state, token);
            await CreateCreditCustomerAsync(application, state, token);
            await RunInitialSalesAsync(application, environment, state, token);
            await SuspendSaleAsync(application, environment, state, token);

            await application.DisposeAsync();
            application = E2EApplication.Build(environment.DatabaseConnection);
            await LoginAsync(application, token);
            await ResumeSuspendedSaleAsync(application, environment, state, token);
            await RunCreditReturnsAndHistoryAsync(application, environment, state, token);
            await AssertReportingStockFinanceAndAuditAsync(application, environment, state, token);

            await application.DisposeAsync();
            application = E2EApplication.Build(environment.DatabaseConnection);
            await LoginAsync(application, token);
            await AssertFullRebuildAsync(application, state, token);
            await RunBackupAndRestoreAsync(application, environment, state, token);

            await application.DisposeAsync();
            application = null;
        }
        finally
        {
            if (application is not null)
            {
                await application.DisposeAsync();
            }

            await environment.CleanupAsync(token);
        }

        Assert.Equal(0, await environment.TemporaryDatabaseResidueAsync(token));
        Assert.True(string.IsNullOrWhiteSpace(environment.BackupPath) || !File.Exists(environment.BackupPath));
        state.Pass("E2E27 Cleanup");
        Assert.Equal(27, state.Checkpoints.Count);
    }

    private static async Task BootstrapSecurityAsync(
        E2EApplication application,
        E2ESqlEnvironment environment,
        ScenarioState state,
        CancellationToken token)
    {
        var authentication = application.Get<IAuthenticationService>();
        Assert.False(Success(await authentication.HasUsersAsync(token)));

        var bootstrap = Success(await application.Get<ISecurityBootstrapService>().BootstrapOwnerAsync(
            new BootstrapOwnerRequest(Guid.NewGuid(), OwnerLogin, "Propriétaire E2E", OwnerPassword), token));
        Assert.NotEmpty(bootstrap.Codes);
        state.RecoveryCodes.AddRange(bootstrap.Codes);
        Assert.True(Success(await authentication.HasUsersAsync(token)));
        Assert.Equal(1, await environment.ScalarAsync<int>(
            "SELECT COUNT(*) FROM security.UserAccounts;", token));

        var login = Success(await authentication.LoginAsync(
            new LoginRequest(OwnerLogin, OwnerPassword, Guid.NewGuid()), token));
        Assert.Contains("OWNER", login.Session.Roles);
        Assert.Equal(SecurityPermissions.All.Order(), login.Session.Permissions.Order());

        var administration = application.Get<ISecurityAdministrationService>();
        var ownerRole = Assert.Single(Success(await administration.GetRolesAsync(token)), role => role.Code == "OWNER");
        Assert.Equal(SecurityPermissions.All.Order(), ownerRole.Permissions.Select(permission => permission.Code).Order());
        Assert.Equal(1, await environment.ScalarAsync<int>(
            "SELECT COUNT(*) FROM audit.AuditEvents WHERE Action=N'bootstrap.owner' AND Result=N'Success';", token));
        state.Pass("E2E01 Security bootstrap/login");
    }

    private static async Task LoginAsync(E2EApplication application, CancellationToken token)
    {
        var authentication = application.Get<IAuthenticationService>();
        Assert.True(Success(await authentication.HasUsersAsync(token)));
        var login = Success(await authentication.LoginAsync(
            new LoginRequest(OwnerLogin, OwnerPassword, Guid.NewGuid()), token));
        Assert.Contains("OWNER", login.Session.Roles);
        Assert.Equal(SecurityPermissions.All.Count, login.Session.Permissions.Count);
    }

    private static async Task CreateCatalogAsync(
        E2EApplication application,
        ScenarioState state,
        CancellationToken token)
    {
        state.Category = Success(await application.Get<CreateCatalogCategoryHandler>().HandleAsync(
            new CreateCatalogCategoryCommand("E2E-QA", "Médicaments synthétiques E2E", DefaultTracksLots: true, DefaultTracksExpiry: true), token));

        state.M1 = await CreateProductAsync(application, state.Category.CategoryId,
            "MED-E2E-001", "Paracétamol 500 mg", 10m, "6110000000001", token);
        state.M2 = await CreateProductAsync(application, state.Category.CategoryId,
            "MED-E2E-002", "Ibuprofène 400 mg", 20m, "6110000000002", token);
        state.M3 = await CreateProductAsync(application, state.Category.CategoryId,
            "MED-E2E-003", "Produit crédit/retour E2E", 25m, "6110000000003", token);

        var search = Success(await application.Get<SearchCatalogProductsHandler>().HandleAsync(
            new SearchCatalogProductsQuery("MED-E2E", Limit: 10), token));
        Assert.Equal(3, search.Count);
        Assert.Equal(new[] { 10m, 20m, 25m }, search.OrderBy(product => product.InternalCode).Select(product => product.CurrentSalePrice));
        state.Pass("E2E02 Catalog");
    }

    private static async Task<E2EProduct> CreateProductAsync(
        E2EApplication application,
        long categoryId,
        string code,
        string name,
        decimal price,
        string barcode,
        CancellationToken token)
    {
        var product = Success(await application.Get<CreateCatalogProductHandler>().HandleAsync(
            new CreateCatalogProductCommand(
                name,
                "Conditionnement QA",
                categoryId,
                code,
                price,
                [new CatalogBarcodeInput(barcode, true, "EAN13")],
                TracksLots: true,
                TracksExpiry: true,
                MinimumStock: 2m),
            token));
        Assert.Equal(barcode, Assert.Single(product.Barcodes).Value);
        return new E2EProduct(product.ProductId, code, name, price);
    }

    private static async Task CreatePurchasingFlowAsync(
        E2EApplication application,
        E2ESqlEnvironment environment,
        ScenarioState state,
        CancellationToken token)
    {
        state.Location = Success(await application.Get<InventoryCommandHandlers>().CreateLocationAsync(
            new CreateStockLocationCommand(Guid.NewGuid(), "E2E-MAIN", "Magasin principal E2E", true), token));

        var purchasing = application.Get<PurchasingCommandHandlers>();
        state.Supplier = Success(await purchasing.CreateSupplierAsync(
            new CreateSupplierCommand("SUP-E2E-001", "Fournisseur E2E"), token));
        Assert.Equal("SUP-E2E-001", state.Supplier.Code);
        state.Pass("E2E03 Supplier");

        var order = Success(await purchasing.CreateOrderAsync(
            new CreatePurchaseOrderCommand(state.Supplier.SupplierId, "PO-E2E-001", DateOnly.FromDateTime(DateTime.UtcNow)), token));
        foreach (var line in new[]
                 {
                     (state.M1, 10, 5m),
                     (state.M2, 8, 8m),
                     (state.M3, 6, 12m)
                 })
        {
            order = Success(await purchasing.AddOrderLineAsync(
                new AddPurchaseOrderLineCommand(order.OrderId, line.Item1.Id, line.Item2, line.Item3, order.RowVersion), token));
        }

        state.Order = Success(await purchasing.ConfirmOrderAsync(
            new ConfirmPurchaseOrderCommand(order.OrderId, order.RowVersion), token));
        Assert.Equal(PurchaseOrderStatus.Confirmed, state.Order.Status);
        Assert.Equal(3, state.Order.Lines.Count);
        Assert.Equal(0, await TotalStockAsync(application, state.M1.Id, token));
        Assert.Equal(0, await TotalStockAsync(application, state.M2.Id, token));
        Assert.Equal(0, await TotalStockAsync(application, state.M3.Id, token));
        state.Pass("E2E04 PurchaseOrder");

        var firstOperation = Guid.NewGuid();
        var firstCommand = Receipt(
            firstOperation,
            state,
            state.Order,
            "BL-E2E-001",
            [6, 4, 3],
            "A",
            [180, 210, 240]);
        var firstReceipt = Success(await purchasing.FinalizeReceiptAsync(firstCommand, token));
        var receiptReplay = Success(await purchasing.FinalizeReceiptAsync(firstCommand, token));
        Assert.Equal(firstReceipt.ReceiptId, receiptReplay.ReceiptId);
        Assert.Equal(1, await environment.ScalarAsync<int>(
            "SELECT COUNT(*) FROM purchasing.GoodsReceipts WHERE OperationId=@operationId;",
            token,
            new SqlParameter("@operationId", SqlDbType.UniqueIdentifier) { Value = firstOperation }));

        state.Order = Success(await application.Get<PurchasingQueryHandlers>().GetOrderAsync(state.Order.OrderId, token));
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, state.Order.Status);
        Assert.Equal(new[] { 4, 4, 3 }, state.Order.Lines.OrderBy(line => line.ProductCodeSnapshot).Select(line => line.RemainingQuantity));
        state.Pass("E2E05 Partial receipt");

        var secondCommand = Receipt(
            Guid.NewGuid(),
            state,
            state.Order,
            "BL-E2E-002",
            [4, 4, 3],
            "B",
            [365, 395, 425]);
        Success(await purchasing.FinalizeReceiptAsync(secondCommand, token));
        state.Order = Success(await application.Get<PurchasingQueryHandlers>().GetOrderAsync(state.Order.OrderId, token));
        Assert.Equal(PurchaseOrderStatus.FullyReceived, state.Order.Status);
        Assert.All(state.Order.Lines, line => Assert.Equal(0, line.RemainingQuantity));
        Assert.Equal(10, await TotalStockAsync(application, state.M1.Id, token));
        Assert.Equal(8, await TotalStockAsync(application, state.M2.Id, token));
        Assert.Equal(6, await TotalStockAsync(application, state.M3.Id, token));
        Assert.Equal(2, await environment.ScalarAsync<int>("SELECT COUNT(*) FROM purchasing.GoodsReceipts;", token));
        state.Pass("E2E06 Full receipt");
    }

    private static FinalizeGoodsReceiptCommand Receipt(
        Guid operationId,
        ScenarioState state,
        PurchaseOrderView order,
        string deliveryNote,
        IReadOnlyList<int> quantities,
        string lotSuffix,
        IReadOnlyList<int> expiryDays)
    {
        var products = new[] { state.M1, state.M2, state.M3 };
        var lines = products.Select((product, index) =>
        {
            var orderLine = order.Lines.Single(line => line.ProductId == product.Id);
            return new FinalizeGoodsReceiptLineCommand(
                product.Id,
                orderLine.LineId,
                quantities[index],
                orderLine.UnitPurchasePrice!.Value,
                null,
                $"LOT-{product.Code}-{lotSuffix}",
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(expiryDays[index]));
        }).ToArray();

        return new FinalizeGoodsReceiptCommand(
            operationId,
            state.Supplier.SupplierId,
            order.OrderId,
            state.Location.LocationId,
            deliveryNote,
            DateOnly.FromDateTime(DateTime.UtcNow),
            order.RowVersion,
            lines);
    }

    private static async Task AssertPurchasingPersistenceAsync(
        E2EApplication application,
        ScenarioState state,
        CancellationToken token)
    {
        var catalog = application.Get<GetCatalogProductByIdHandler>();
        Assert.Equal(state.M1.Code, Success(await catalog.HandleAsync(new GetCatalogProductByIdQuery(state.M1.Id), token)).InternalCode);
        Assert.Equal(state.Supplier.SupplierId, Success(await application.Get<PurchasingQueryHandlers>()
            .GetSupplierAsync(state.Supplier.SupplierId, token)).SupplierId);
        Assert.Equal(PurchaseOrderStatus.FullyReceived, Success(await application.Get<PurchasingQueryHandlers>()
            .GetOrderAsync(state.Order.OrderId, token)).Status);
        Assert.Equal(10, await TotalStockAsync(application, state.M1.Id, token));
        Assert.Equal(8, await TotalStockAsync(application, state.M2.Id, token));
        Assert.Equal(6, await TotalStockAsync(application, state.M3.Id, token));
    }

    private static async Task RunInventoryAsync(
        E2EApplication application,
        ScenarioState state,
        CancellationToken token)
    {
        var commands = application.Get<InventoryCommandHandlers>();
        var session = Success(await commands.CreateSessionAsync(
            new CreateInventorySessionCommand(Guid.NewGuid(), "INV-E2E-001", state.Location.LocationId), token));
        session = Success(await commands.StartSessionAsync(
            new StartInventorySessionCommand(Guid.NewGuid(), session.SessionId, session.RowVersion), token));

        var m2Reduced = false;
        foreach (var original in session.Lines.ToArray())
        {
            var current = session.Lines.Single(line => line.CountLineId == original.CountLineId);
            var counted = current.ExpectedQuantity;
            if (!m2Reduced && current.ProductId == state.M2.Id)
            {
                counted--;
                m2Reduced = true;
            }

            session = Success(await commands.SaveCountLineAsync(
                new SaveInventoryCountLineCommand(
                    Guid.NewGuid(), session.SessionId, current.CountLineId, counted, session.RowVersion, current.RowVersion),
                token));
        }

        Assert.True(m2Reduced);
        session = Success(await commands.FinalizeSessionAsync(
            new FinalizeInventorySessionCommand(Guid.NewGuid(), session.SessionId, session.RowVersion), token));
        Assert.Equal(InventorySessionStatus.Completed, session.Status);
        Assert.Equal(7, await TotalStockAsync(application, state.M2.Id, token));
        var movement = Assert.Single(Success(await application.Get<InventoryQueryHandlers>()
            .GetMovementsAsync(state.M2.Id, state.Location.LocationId, 100, token)),
            item => item.MovementType == StockMovementType.InventoryDecrease);
        Assert.Equal(1, movement.Quantity);
        state.Pass("E2E07 Inventory");
    }

    private static async Task CreateCreditCustomerAsync(
        E2EApplication application,
        ScenarioState state,
        CancellationToken token)
    {
        state.Customer = Success(await application.Get<CustomerCommandHandlers>().CreateAsync(
            new CreateCustomerRequest(Guid.NewGuid(), "CLIENT-E2E-001", null, "Client synthétique QA", true), token));
        Assert.True(state.Customer.CreditAllowed);
        Assert.Equal(0m, state.Customer.CreditBalance.Amount);
    }

    private static async Task RunInitialSalesAsync(
        E2EApplication application,
        E2ESqlEnvironment environment,
        ScenarioState state,
        CancellationToken token)
    {
        var cashExecution = await FinalizeSaleAsync(
            application, state.M1.Id, 5, SalePaymentMethod.Cash, null, 0m, null, token);
        state.CashSale = cashExecution.Sale;
        Assert.Equal(50m, state.CashSale.Total.Amount);
        Assert.Equal(SalePaymentMethod.Cash, state.CashSale.Payment.Method);
        Assert.Equal(5, await TotalStockAsync(application, state.M1.Id, token));
        var allocation = Assert.Single(Assert.Single(state.CashSale.Lines).Allocations);
        Assert.EndsWith("-A", allocation.BatchNumber, StringComparison.Ordinal);
        Assert.Equal(5, allocation.Quantity);

        var saleReplay = Success(await application.Get<FinalizeSaleHandler>().HandleAsync(cashExecution.Command, token));
        Assert.Equal(state.CashSale.SaleId, saleReplay.SaleId);
        Assert.Equal(1, await environment.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sales.Sales WHERE OperationId=@operationId;",
            token,
            new SqlParameter("@operationId", SqlDbType.UniqueIdentifier) { Value = cashExecution.Command.OperationId }));
        state.Pass("E2E08 Cash sale + FEFO");

        state.TpeSale = (await FinalizeSaleAsync(
            application, state.M2.Id, 2, SalePaymentMethod.ExternalCard, null, 0m, null, token)).Sale;
        Assert.Equal(40m, state.TpeSale.Total.Amount);
        Assert.True(state.TpeSale.Payment.ExternalCardConfirmed);
        Assert.Equal(5, await TotalStockAsync(application, state.M2.Id, token));
        Assert.Equal(0, await environment.ScalarAsync<int>(
            "SELECT COUNT(*) FROM cash.CashLedgerEntries WHERE SourceReference=@reference;",
            token,
            new SqlParameter("@reference", SqlDbType.NVarChar, 64) { Value = state.TpeSale.Reference }));
        state.Pass("E2E09 TPE sale");
    }

    private static async Task SuspendSaleAsync(
        E2EApplication application,
        E2ESqlEnvironment environment,
        ScenarioState state,
        CancellationToken token)
    {
        var draft = await CreateDraftAsync(application, state.M3.Id, 1, token);
        var request = new SuspendSaleRequest(
            Guid.NewGuid(),
            null,
            draft.Lines.Select(line => new SuspendSaleLineRequest(
                line.ProductId.Value,
                line.Reference.Value,
                line.DisplayName,
                line.Presentation,
                line.UnitPrice.Amount,
                line.Quantity.Value)).ToArray());
        var workflow = application.Get<SuspendedSaleWorkflow>();
        state.Suspension = Success(await workflow.SuspendAsync(request, token));
        var replay = Success(await workflow.SuspendAsync(request, token));
        Assert.Equal(state.Suspension.SuspendedSaleId, replay.SuspendedSaleId);
        Assert.Equal(6, await TotalStockAsync(application, state.M3.Id, token));
        Assert.Equal(2, await environment.ScalarAsync<int>("SELECT COUNT(*) FROM sales.Sales;", token));
        Assert.Equal(2, await environment.ScalarAsync<int>("SELECT COUNT(*) FROM sales.Payments;", token));
        Assert.Equal(1, await environment.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sales.SuspendedSales WHERE OperationId=@operationId;",
            token,
            new SqlParameter("@operationId", SqlDbType.UniqueIdentifier) { Value = request.OperationId }));
        state.Pass("E2E10 Suspension");
    }

    private static async Task ResumeSuspendedSaleAsync(
        E2EApplication application,
        E2ESqlEnvironment environment,
        ScenarioState state,
        CancellationToken token)
    {
        var open = Success(await application.Get<SuspendedSaleWorkflow>().GetOpenAsync(token));
        var suspension = Assert.Single(open, item => item.SuspendedSaleId == state.Suspension.SuspendedSaleId);
        Assert.Equal(state.M3.Code, Assert.Single(suspension.Lines).ProductReference.Value);
        Assert.Equal(6, await TotalStockAsync(application, state.M3.Id, token));
        state.Pass("E2E11 Restart + resume");

        var draft = Success(await application.Get<RestoreSuspendedSaleDraftHandler>().HandleAsync(suspension, token));
        var operationId = Guid.NewGuid();
        var command = new FinalizeSaleCommand(
            operationId,
            draft.Id,
            draft.Version,
            SalePaymentMethod.Cash,
            draft.Total.Amount,
            false,
            SuspendedSale: new SuspendedSaleCompletionRequest(suspension.SuspendedSaleId, suspension.RowVersion));
        state.ResumedSale = Success(await application.Get<FinalizeSaleHandler>().HandleAsync(command, token));
        var replay = Success(await application.Get<FinalizeSaleHandler>().HandleAsync(command, token));
        Assert.Equal(state.ResumedSale.SaleId, replay.SaleId);
        Assert.Equal(5, await TotalStockAsync(application, state.M3.Id, token));
        Assert.Empty(Success(await application.Get<SuspendedSaleWorkflow>().GetOpenAsync(token)));
        Assert.Equal(1, await environment.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sales.Sales WHERE OperationId=@operationId;",
            token,
            new SqlParameter("@operationId", SqlDbType.UniqueIdentifier) { Value = operationId }));
        state.Pass("E2E12 Finalize resumed sale");
    }

    private static async Task RunCreditReturnsAndHistoryAsync(
        E2EApplication application,
        E2ESqlEnvironment environment,
        ScenarioState state,
        CancellationToken token)
    {
        state.CreditSale = (await FinalizeSaleAsync(
            application,
            state.M3.Id,
            2,
            SalePaymentMethod.Cash,
            state.Customer.CustomerId,
            30m,
            null,
            token)).Sale;
        Assert.Equal(50m, state.CreditSale.Total.Amount);
        Assert.Equal(20m, state.CreditSale.Payment.Amount.Amount);
        Assert.Equal(30m, state.CreditSale.CreditAmount.Amount);
        state.Customer = Success(await application.Get<CustomerQueryHandlers>().GetByIdAsync(state.Customer.CustomerId, token));
        Assert.Equal(30m, state.Customer.CreditBalance.Amount);
        Assert.Equal(3, await TotalStockAsync(application, state.M3.Id, token));
        state.Pass("E2E13 Credit sale");

        var partialOperation = Guid.NewGuid();
        var partialRequest = new RegisterCreditPaymentRequest(
            partialOperation, state.Customer.CustomerId, 10m, SalePaymentMethod.Cash, false);
        state.Customer = Success(await application.Get<CustomerCommandHandlers>().RegisterCreditPaymentAsync(partialRequest, token));
        Assert.Equal(20m, state.Customer.CreditBalance.Amount);
        var partialReplay = Success(await application.Get<CustomerCommandHandlers>().RegisterCreditPaymentAsync(partialRequest, token));
        Assert.Equal(20m, partialReplay.CreditBalance.Amount);
        Assert.Equal(1, await environment.ScalarAsync<int>(
            "SELECT COUNT(*) FROM customers.CustomerLedgerEntries WHERE OperationId=@operationId;",
            token,
            new SqlParameter("@operationId", SqlDbType.UniqueIdentifier) { Value = partialOperation }));
        state.Pass("E2E14 Partial settlement");

        var authentication = application.Get<IAuthenticationService>();
        Success(await authentication.ReauthenticateAsync(OwnerPassword, Guid.NewGuid(), token));
        var preparation = Success(await application.Get<GetSaleReturnPreparationHandler>().HandleAsync(
            new GetSaleReturnPreparationQuery(state.CashSale.SaleId), token));
        var saleLine = Assert.Single(preparation.Lines);
        var returnHandler = application.Get<ReturnSaleHandler>();

        var restockOperation = Guid.NewGuid();
        var restockRequest = new ReturnSaleRequest(
            restockOperation,
            state.CashSale.SaleId,
            [new ReturnSaleLineRequest(saleLine.SaleLineId, 1, ReturnStockDestination.RestockApproved)],
            ReturnRefundMethod.Cash);
        state.RestockedReturn = Success(await returnHandler.HandleAsync(restockRequest, token));
        Assert.Equal(10m, state.RestockedReturn.RefundAmount.Amount);
        Assert.Equal(6, await TotalStockAsync(application, state.M1.Id, token));
        var returnReplay = Success(await returnHandler.HandleAsync(restockRequest, token));
        Assert.Equal(state.RestockedReturn.ReturnId, returnReplay.ReturnId);
        Assert.Equal(1, await environment.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sales.SaleReturns WHERE OperationId=@operationId;",
            token,
            new SqlParameter("@operationId", SqlDbType.UniqueIdentifier) { Value = restockOperation }));
        state.Pass("E2E15 RestockApproved return");

        state.NotRestockableReturn = Success(await returnHandler.HandleAsync(
            new ReturnSaleRequest(
                Guid.NewGuid(),
                state.CashSale.SaleId,
                [new ReturnSaleLineRequest(saleLine.SaleLineId, 1, ReturnStockDestination.NotRestockable)],
                ReturnRefundMethod.Cash),
            token));
        Assert.Equal(10m, state.NotRestockableReturn.RefundAmount.Amount);
        Assert.Equal(6, await TotalStockAsync(application, state.M1.Id, token));
        state.Pass("E2E16 NotRestockable return");

        state.Customer = Success(await application.Get<CustomerCommandHandlers>().RegisterCreditPaymentAsync(
            new RegisterCreditPaymentRequest(Guid.NewGuid(), state.Customer.CustomerId, 20m, SalePaymentMethod.Cash, false), token));
        Assert.Equal(0m, state.Customer.CreditBalance.Amount);
        Assert.Single(state.Customer.Ledger, entry => entry.EntryType == "CreditSale");
        Assert.Equal(2, state.Customer.Ledger.Count(entry => entry.EntryType == "CreditPayment"));
        state.Pass("E2E17 Debt fully settled");

        var currentProduct = Success(await application.Get<GetCatalogProductByIdHandler>().HandleAsync(
            new GetCatalogProductByIdQuery(state.M1.Id), token));
        Success(await authentication.ReauthenticateAsync(OwnerPassword, Guid.NewGuid(), token));
        Success(await application.Get<ChangeCatalogProductPriceHandler>().HandleAsync(
            new ChangeCatalogProductPriceCommand(state.M1.Id, 11m, "Validation snapshot E2E", currentProduct.LocalSettingsRowVersion), token));
        var historical = Success(await application.Get<GetSaleByIdHandler>().HandleAsync(
            new GetSaleByIdQuery(state.CashSale.SaleId), token));
        var historicalLine = Assert.Single(historical.Lines);
        Assert.Equal(10m, historicalLine.UnitPrice.Amount);
        Assert.Equal(state.M1.Name, historicalLine.ProductName);
        Assert.Equal(50m, historical.Total.Amount);
        Assert.EndsWith("-A", Assert.Single(historicalLine.Allocations).BatchNumber, StringComparison.Ordinal);
        Assert.Equal(11m, Success(await application.Get<GetCatalogProductByIdHandler>()
            .HandleAsync(new GetCatalogProductByIdQuery(state.M1.Id), token)).CurrentSalePrice);
        state.Pass("E2E18 Historical snapshot");
    }

    private static async Task AssertReportingStockFinanceAndAuditAsync(
        E2EApplication application,
        E2ESqlEnvironment environment,
        ScenarioState state,
        CancellationToken token)
    {
        var query = new ReportingQuery(new ReportingPeriod(state.StartedAtUtc, DateTime.UtcNow.AddMinutes(1)), 200);
        state.Reporting = Success(await application.Get<IReportingService>().GetSnapshotAsync(query, token));
        var summary = state.Reporting.Summary;
        Assert.Equal(165m, summary.GrossSales);
        Assert.Equal(20m, summary.Refunds);
        Assert.Equal(145m, summary.NetSales);
        Assert.Equal(4, summary.SaleCount);
        Assert.Equal(2, summary.ReturnCount);
        Assert.Equal(135m, summary.Payments.Sum(payment => payment.Amount));
        Assert.Equal(0m, summary.OpenCredit);
        Assert.Equal(30m, summary.CreditPayments);
        Assert.Equal(14, summary.CurrentSellableStock);
        Assert.Equal(1, summary.PurchaseOrders);
        Assert.Equal(24, summary.ReceivedQuantity);
        Assert.Equal(4, state.Reporting.Sales.Count);
        Assert.Equal(2, state.Reporting.Returns.Count);
        Assert.Equal(2, state.Reporting.Purchasing.Count);
        Assert.Single(state.Reporting.InventoryAdjustments, row => row.ProductId == state.M2.Id && row.Quantity == 1);
        Assert.Equal(0m, Assert.Single(state.Reporting.CustomerCredit, row => row.CustomerId == state.Customer.CustomerId).OpenBalance);
        state.Pass("E2E19 Reporting");

        Assert.Equal(6, await TotalStockAsync(application, state.M1.Id, token)); // 10 - 5 + 1
        Assert.Equal(5, await TotalStockAsync(application, state.M2.Id, token)); // 8 - 1 inventory - 2 sold
        Assert.Equal(3, await TotalStockAsync(application, state.M3.Id, token)); // 6 - 1 resumed - 2 credit
        Assert.All(state.Reporting.Stock.Where(row => new[] { state.M1.Id, state.M2.Id, state.M3.Id }.Contains(row.ProductId)),
            row => Assert.True(row.SellableQuantity >= 0));
        state.Pass("E2E20 Stock invariants");

        Assert.Equal(165m, await environment.ScalarAsync<decimal>("SELECT SUM(GrandTotal) FROM sales.Sales;", token));
        Assert.Equal(135m, await environment.ScalarAsync<decimal>("SELECT SUM(Amount) FROM sales.Payments;", token));
        Assert.Equal(30m, await environment.ScalarAsync<decimal>(
            "SELECT SUM(Amount) FROM customers.CustomerLedgerEntries WHERE EntryType=N'CreditSale';", token));
        Assert.Equal(30m, await environment.ScalarAsync<decimal>(
            "SELECT SUM(Amount) FROM customers.CustomerLedgerEntries WHERE EntryType=N'CreditPayment';", token));
        Assert.Equal(20m, await environment.ScalarAsync<decimal>("SELECT SUM(RefundAmount) FROM sales.SaleReturns;", token));
        Assert.Equal(105m, await environment.ScalarAsync<decimal>("SELECT SUM(Amount) FROM cash.CashLedgerEntries;", token));
        Assert.Equal(0m, state.Customer.CreditBalance.Amount);
        state.Pass("E2E21 Financial invariants");

        var audit = Success(await application.Get<IAuditReader>().SearchAsync(
            new AuditSearchQuery(FromUtc: state.StartedAtUtc, ToUtc: DateTime.UtcNow.AddMinutes(1), Limit: 500), token));
        foreach (var action in new[]
                 {
                     "bootstrap.owner", "supplier.create", "order.create", "receipt.finalize",
                     "inventory.create", "inventory.finalize", "sale.finalize", "sale.suspend",
                     "sale.resume", "customer.create", "credit.allow", "credit.payment",
                     "sale.return_refund", "price.change"
                 })
        {
            Assert.Contains(audit, item => item.Action == action && item.Result == "Success");
        }

        Assert.Equal(0, await environment.ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM audit.AuditEvents a
            LEFT JOIN audit.AuditEventDetails d ON d.AuditEventId=a.Id
            WHERE LOWER(CONCAT(a.EventType,N'|',a.Module,N'|',a.EntityType,N'|',a.EntityId,N'|',a.Action,N'|',a.ActorDisplayName,N'|',a.ReasonCode,N'|',d.FieldName,N'|',d.OldValueRedacted,N'|',d.NewValueRedacted))
                  LIKE N'%passwordhash%'
               OR LOWER(CONCAT(a.EventType,N'|',a.Module,N'|',a.EntityType,N'|',a.EntityId,N'|',a.Action,N'|',a.ActorDisplayName,N'|',a.ReasonCode,N'|',d.FieldName,N'|',d.OldValueRedacted,N'|',d.NewValueRedacted))
                  LIKE N'%pan%'
               OR LOWER(CONCAT(a.EventType,N'|',a.Module,N'|',a.EntityType,N'|',a.EntityId,N'|',a.Action,N'|',a.ActorDisplayName,N'|',a.ReasonCode,N'|',d.FieldName,N'|',d.OldValueRedacted,N'|',d.NewValueRedacted))
                  LIKE N'%cvv%';
            """, token));
        foreach (var secret in state.RecoveryCodes.Append(OwnerPassword))
        {
            Assert.Equal(0, await environment.ScalarAsync<int>(
                """
                SELECT COUNT(*) FROM audit.AuditEvents a
                LEFT JOIN audit.AuditEventDetails d ON d.AuditEventId=a.Id
                WHERE CONCAT(a.EventType,N'|',a.Module,N'|',a.EntityType,N'|',a.EntityId,N'|',a.Action,N'|',a.ActorDisplayName,N'|',a.ReasonCode,N'|',d.FieldName,N'|',d.OldValueRedacted,N'|',d.NewValueRedacted) LIKE N'%' + @secret + N'%';
                """,
                token,
                new SqlParameter("@secret", SqlDbType.NVarChar, 256) { Value = secret }));
        }
        state.Pass("E2E22 Audit/secrets");
    }

    private static async Task AssertFullRebuildAsync(
        E2EApplication application,
        ScenarioState state,
        CancellationToken token)
    {
        Assert.Equal(11m, Success(await application.Get<GetCatalogProductByIdHandler>()
            .HandleAsync(new GetCatalogProductByIdQuery(state.M1.Id), token)).CurrentSalePrice);
        Assert.Equal(6, await TotalStockAsync(application, state.M1.Id, token));
        Assert.Equal(5, await TotalStockAsync(application, state.M2.Id, token));
        Assert.Equal(3, await TotalStockAsync(application, state.M3.Id, token));
        Assert.Equal(4, Success(await application.Get<GetRecentSalesHandler>()
            .HandleAsync(new GetRecentSalesQuery(10), token)).Count);
        Assert.Empty(Success(await application.Get<SuspendedSaleWorkflow>().GetOpenAsync(token)));
        Assert.Equal(0m, Success(await application.Get<CustomerQueryHandlers>()
            .GetByIdAsync(state.Customer.CustomerId, token)).CreditBalance.Amount);
        Assert.Equal(PurchaseOrderStatus.FullyReceived, Success(await application.Get<PurchasingQueryHandlers>()
            .GetOrderAsync(state.Order.OrderId, token)).Status);
        var rebuiltReport = Success(await application.Get<IReportingService>().GetSnapshotAsync(
            new ReportingQuery(new ReportingPeriod(state.StartedAtUtc, DateTime.UtcNow.AddMinutes(1)), 200), token));
        Assert.Equal(state.Reporting.Summary.GrossSales, rebuiltReport.Summary.GrossSales);
        Assert.Equal(state.Reporting.Summary.NetSales, rebuiltReport.Summary.NetSales);
        Assert.Equal(state.Reporting.Summary.CurrentSellableStock, rebuiltReport.Summary.CurrentSellableStock);
        state.Pass("E2E23 Full context rebuild");
    }

    private static async Task RunBackupAndRestoreAsync(
        E2EApplication application,
        E2ESqlEnvironment environment,
        ScenarioState state,
        CancellationToken token)
    {
        Success(await application.Get<IAuthenticationService>().ReauthenticateAsync(
            OwnerPassword, Guid.NewGuid(), token));
        state.PreBackupAuditId = await environment.ScalarAsync<long>(
            "SELECT COALESCE(MAX(Id),0) FROM audit.AuditEvents;", token);
        state.PreBackupAuditFingerprint = await environment.FingerprintAsync(
            environment.DatabaseName, "audit.AuditEvents", $"Id <= {state.PreBackupAuditId}", token);

        var backups = application.Get<IBackupRecoveryService>();
        var created = Success(await backups.CreateAsync(
            new CreateBackupRequest(Guid.NewGuid(), SqlBackupType.Full, false), token));
        environment.BackupPath = created.Backup.FullPath;
        Assert.True(File.Exists(environment.BackupPath));
        Assert.Equal("VerifyOnlyPassed", created.Backup.VerificationStatus);
        Assert.Equal(64, created.Backup.Sha256?.Length);
        var verified = Success(await backups.VerifyAsync(
            new VerifyBackupRequest(Guid.NewGuid(), environment.BackupPath), token));
        Assert.Equal(created.Backup.Sha256, verified.Sha256);
        state.Pass("E2E24 Backup");

        var restoredByService = Success(await backups.RestoreTestAsync(
            new RestoreTestRequest(Guid.NewGuid(), environment.BackupPath), token));
        Assert.Equal(10, restoredByService.AppliedMigrationCount);
        Assert.True(restoredByService.HealthCheckPassed);
        Assert.True(restoredByService.CleanupPassed);
        state.Pass("E2E25 Restore-test");

        await environment.RestoreForComparisonAsync(environment.BackupPath, token);
        Assert.Equal(10, await environment.ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;", token, databaseName: environment.RestoreDatabaseName));

        foreach (var table in new[]
                 {
                     "catalog.Products", "inventory.StockBalances", "inventory.StockBatches",
                     "purchasing.PurchaseOrders", "purchasing.GoodsReceipts", "sales.Sales",
                     "sales.SaleLines", "sales.SaleLineAllocations", "sales.Payments",
                     "sales.SaleReturns", "sales.SaleReturnLines", "customers.Customers",
                     "customers.CustomerLedgerEntries", "cash.CashLedgerEntries"
                 })
        {
            Assert.Equal(
                await environment.FingerprintAsync(environment.DatabaseName, table, null, token),
                await environment.FingerprintAsync(environment.RestoreDatabaseName, table, null, token));
        }

        foreach (var view in new[]
                 {
                     "reporting.vw_SalesSummary", "reporting.vw_ProductSales", "reporting.vw_Payments",
                     "reporting.vw_ReturnsSummary", "reporting.vw_CustomerCredit", "reporting.vw_CashMovements",
                     "reporting.vw_StockSummary", "reporting.vw_InventoryAdjustments",
                     "reporting.vw_PurchaseOrders", "reporting.vw_Receipts", "reporting.vw_ExpiryAlerts"
                 })
        {
            Assert.Equal(
                await environment.FingerprintAsync(environment.DatabaseName, view, null, token),
                await environment.FingerprintAsync(environment.RestoreDatabaseName, view, null, token));
        }

        Assert.Equal(
            state.PreBackupAuditFingerprint,
            await environment.FingerprintAsync(environment.RestoreDatabaseName, "audit.AuditEvents", null, token));
        Assert.Equal(0, await environment.ScalarAsync<int>(
            "SELECT COUNT(*) FROM audit.AuditEvents WHERE Id>@id AND Module<>N'backup';",
            token,
            new SqlParameter("@id", SqlDbType.BigInt) { Value = state.PreBackupAuditId }));
        state.Pass("E2E26 Source/restore comparison");
    }

    private static async Task<SaleExecution> FinalizeSaleAsync(
        E2EApplication application,
        long productId,
        int quantity,
        SalePaymentMethod paymentMethod,
        long? customerId,
        decimal creditAmount,
        SuspendedSaleCompletionRequest? suspension,
        CancellationToken token)
    {
        var draft = await CreateDraftAsync(application, productId, quantity, token);
        var command = new FinalizeSaleCommand(
            Guid.NewGuid(),
            draft.Id,
            draft.Version,
            paymentMethod,
            paymentMethod == SalePaymentMethod.Cash ? draft.Total.Amount - creditAmount : null,
            paymentMethod == SalePaymentMethod.ExternalCard,
            customerId,
            creditAmount,
            suspension);
        return new SaleExecution(Success(await application.Get<FinalizeSaleHandler>().HandleAsync(command, token)), command);
    }

    private static async Task<SaleDraftView> CreateDraftAsync(
        E2EApplication application,
        long productId,
        int quantity,
        CancellationToken token)
    {
        var draft = Success(await application.Get<CreateSaleDraftHandler>().HandleAsync(token));
        return Success(await application.Get<AddProductToSaleDraftHandler>().HandleAsync(
            new AddProductToSaleDraftCommand(
                draft.Id,
                SellableProductLookup.ByProductId(Success(ProductId.Create(productId))),
                Success(SaleQuantity.Create(quantity)),
                draft.Version),
            token));
    }

    private static async Task<int> TotalStockAsync(
        E2EApplication application,
        long productId,
        CancellationToken token) =>
        Success(await application.Get<InventoryQueryHandlers>().GetProductDetailsAsync(productId, null, token)).TotalQuantity;

    private static T Success<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);
        return result.Value;
    }

    private static void Success(Result result) =>
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);

    private sealed record E2EProduct(long Id, string Code, string Name, decimal Price);
    private sealed record SaleExecution(FinalizedSaleView Sale, FinalizeSaleCommand Command);

    private sealed class ScenarioState(DateTime startedAtUtc)
    {
        public DateTime StartedAtUtc { get; } = startedAtUtc;
        public List<string> RecoveryCodes { get; } = [];
        public HashSet<string> Checkpoints { get; } = new(StringComparer.Ordinal);
        public CatalogCategoryView Category { get; set; } = null!;
        public E2EProduct M1 { get; set; } = null!;
        public E2EProduct M2 { get; set; } = null!;
        public E2EProduct M3 { get; set; } = null!;
        public StockLocationView Location { get; set; } = null!;
        public SupplierView Supplier { get; set; } = null!;
        public PurchaseOrderView Order { get; set; } = null!;
        public CustomerDetailView Customer { get; set; } = null!;
        public FinalizedSaleView CashSale { get; set; } = null!;
        public FinalizedSaleView TpeSale { get; set; } = null!;
        public SuspendedSaleSummaryView Suspension { get; set; } = null!;
        public FinalizedSaleView ResumedSale { get; set; } = null!;
        public FinalizedSaleView CreditSale { get; set; } = null!;
        public SaleReturnView RestockedReturn { get; set; } = null!;
        public SaleReturnView NotRestockableReturn { get; set; } = null!;
        public ReportingSnapshotView Reporting { get; set; } = null!;
        public long PreBackupAuditId { get; set; }
        public string PreBackupAuditFingerprint { get; set; } = string.Empty;

        public void Pass(string checkpoint) => Assert.True(Checkpoints.Add(checkpoint), $"Checkpoint dupliqué: {checkpoint}");
    }

    private sealed class E2EApplication : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly AsyncServiceScope scope;

        private E2EApplication(ServiceProvider provider)
        {
            this.provider = provider;
            scope = provider.CreateAsyncScope();
        }

        public static E2EApplication Build(string connectionString)
        {
            var services = new ServiceCollection();
            services.AddSqlCatalogAdapters(connectionString);
            services.AddSqlInventoryAdapters();
            services.AddSqlPurchasingAdapters();
            services.AddSqlSalesAdapters();
            services.AddLocalSqlSecurity();
            services.AddSqlReporting();
            services.AddOperationalServices();
            services.AddSingleton<ISaleDraftStore, InMemorySaleDraftStore>();
            services.AddSingleton<ISaleDraftIdGenerator, InMemorySaleDraftIdGenerator>();
            services.AddSingleton<ISaleFinalizationPreparationStore, InMemorySaleFinalizationPreparationStore>();

            services.AddTransient<CreateCatalogCategoryHandler>();
            services.AddTransient<CreateCatalogProductHandler>();
            services.AddTransient<ChangeCatalogProductPriceHandler>();
            services.AddTransient<GetCatalogProductByIdHandler>();
            services.AddTransient<SearchCatalogProductsHandler>();
            services.AddTransient<InventoryCommandHandlers>();
            services.AddTransient<InventoryQueryHandlers>();
            services.AddTransient<PurchasingCommandHandlers>();
            services.AddTransient<PurchasingQueryHandlers>();
            services.AddTransient<CustomerCommandHandlers>();
            services.AddTransient<CustomerQueryHandlers>();
            services.AddTransient<CreateSaleDraftHandler>();
            services.AddTransient<AddProductToSaleDraftHandler>();
            services.AddTransient<RestoreSuspendedSaleDraftHandler>();
            services.AddTransient<PrepareSaleFinalizationHandler>();
            services.AddTransient<FinalizeSaleHandler>();
            services.AddTransient<GetSaleByIdHandler>();
            services.AddTransient<GetRecentSalesHandler>();
            services.AddTransient<GetSaleReturnPreparationHandler>();
            services.AddTransient<ReturnSaleHandler>();
            services.AddTransient<SuspendedSaleWorkflow>();

            return new E2EApplication(services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            }));
        }

        public T Get<T>() where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await provider.DisposeAsync();
        }
    }

    private sealed class E2ESqlEnvironment
    {
        private const string DatabasePrefix = "Pharmacy_E2E_Test_";
        private const string RestorePrefix = "Pharmacy_E2E_Restore_";
        private static readonly Regex SafeName = new("^Pharmacy_E2E_(Test|Restore)_[0-9a-f]{12}$", RegexOptions.CultureInvariant);
        private readonly string serverConnection;

        private E2ESqlEnvironment(string serverConnection, string databaseName, string restoreDatabaseName)
        {
            this.serverConnection = serverConnection;
            DatabaseName = databaseName;
            RestoreDatabaseName = restoreDatabaseName;
            DatabaseConnection = new SqlConnectionStringBuilder(serverConnection)
            {
                InitialCatalog = databaseName,
                ApplicationName = "Pharmacy.E2E001.Tests",
                Pooling = false
            }.ConnectionString;
        }

        public string DatabaseName { get; }
        public string RestoreDatabaseName { get; }
        public string DatabaseConnection { get; }
        public string? BackupPath { get; set; }

        public static async Task<E2ESqlEnvironment> CreateAsync(CancellationToken token)
        {
            var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new InvalidOperationException("PHARMACY_TEST_SQLSERVER_CONNECTION est requise pour E2E-01.");
            }

            var server = new SqlConnectionStringBuilder(raw)
            {
                InitialCatalog = "master",
                Pooling = false,
                ApplicationName = "Pharmacy.E2E001.DatabaseLifecycle"
            }.ConnectionString;
            var environment = new E2ESqlEnvironment(
                server,
                DatabasePrefix + Guid.NewGuid().ToString("N")[..12],
                RestorePrefix + Guid.NewGuid().ToString("N")[..12]);
            environment.ValidateNames();
            await environment.ExecuteServerAsync($"CREATE DATABASE [{environment.DatabaseName}];", token);
            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<PharmacyDbContext>();
                optionsBuilder.UsePharmacySqlServer(environment.DatabaseConnection);
                var options = optionsBuilder.Options;
                await using var context = new PharmacyDbContext(options);
                await context.Database.MigrateAsync(token);
                Assert.Equal(10, (await context.Database.GetAppliedMigrationsAsync(token)).Count());
                Assert.Empty(await context.Database.GetPendingMigrationsAsync(token));
                return environment;
            }
            catch
            {
                await environment.CleanupAsync(token);
                throw;
            }
        }

        public async Task<T> ScalarAsync<T>(
            string sql,
            CancellationToken token,
            params SqlParameter[] parameters) =>
            await ScalarAsync<T>(sql, token, parameters, DatabaseName);

        public async Task<T> ScalarAsync<T>(
            string sql,
            CancellationToken token,
            SqlParameter[]? parameters = null,
            string? databaseName = null)
        {
            var target = databaseName ?? DatabaseName;
            ValidateTarget(target);
            var connectionString = new SqlConnectionStringBuilder(serverConnection) { InitialCatalog = target }.ConnectionString;
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(token);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            if (parameters is { Length: > 0 }) command.Parameters.AddRange(parameters);
            var value = await command.ExecuteScalarAsync(token);
            if (value is null || value is DBNull) throw new InvalidOperationException("Valeur SQL E2E attendue absente.");
            return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
        }

        public async Task<string> FingerprintAsync(
            string databaseName,
            string source,
            string? predicate,
            CancellationToken token)
        {
            ValidateTarget(databaseName);
            if (!Regex.IsMatch(source, "^[a-z]+\\.[A-Za-z_]+$", RegexOptions.CultureInvariant))
                throw new InvalidOperationException("Source SQL E2E non sûre.");
            if (predicate is not null && !Regex.IsMatch(predicate, "^Id <= [0-9]+$", RegexOptions.CultureInvariant))
                throw new InvalidOperationException("Prédicat SQL E2E non sûr.");
            var where = predicate is null ? string.Empty : " WHERE " + predicate;
            return await ScalarAsync<string>(
                $"SELECT CONCAT(COUNT_BIG(*),N'|',COALESCE(CONVERT(bigint,CHECKSUM_AGG(BINARY_CHECKSUM(*))),0)) FROM {source}{where};",
                token,
                databaseName: databaseName);
        }

        public async Task RestoreForComparisonAsync(string backupPath, CancellationToken token)
        {
            if (!Path.IsPathRooted(backupPath) || !File.Exists(backupPath))
                throw new InvalidOperationException("Backup E2E absent pour la restauration comparative.");
            ValidateTarget(RestoreDatabaseName);

            await using var connection = new SqlConnection(serverConnection);
            await connection.OpenAsync(token);
            var files = new List<(string LogicalName, string Type)>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = "RESTORE FILELISTONLY FROM DISK=@path;";
                list.Parameters.Add(new SqlParameter("@path", SqlDbType.NVarChar, 4000) { Value = backupPath });
                await using var reader = await list.ExecuteReaderAsync(token);
                var logical = reader.GetOrdinal("LogicalName");
                var type = reader.GetOrdinal("Type");
                while (await reader.ReadAsync(token)) files.Add((reader.GetString(logical), reader.GetString(type)));
            }

            var dataPath = await ServerStringAsync(connection, "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultDataPath'));", token);
            var logPath = await ServerStringAsync(connection, "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultLogPath'));", token);
            var moves = files.Select((file, index) => new
            {
                file.LogicalName,
                Physical = Path.Combine(file.Type == "L" ? logPath : dataPath,
                    $"{RestoreDatabaseName}_{index}{(file.Type == "L" ? ".ldf" : ".mdf")}")
            }).ToArray();
            await using var restore = connection.CreateCommand();
            restore.CommandTimeout = 300;
            restore.CommandText = $"RESTORE DATABASE [{RestoreDatabaseName}] FROM DISK=@path WITH " +
                                  string.Join(", ", moves.Select((_, index) => $"MOVE @logical{index} TO @physical{index}")) +
                                  ", RECOVERY, CHECKSUM;";
            restore.Parameters.Add(new SqlParameter("@path", SqlDbType.NVarChar, 4000) { Value = backupPath });
            for (var index = 0; index < moves.Length; index++)
            {
                restore.Parameters.Add(new SqlParameter($"@logical{index}", SqlDbType.NVarChar, 128) { Value = moves[index].LogicalName });
                restore.Parameters.Add(new SqlParameter($"@physical{index}", SqlDbType.NVarChar, 4000) { Value = moves[index].Physical });
            }
            await restore.ExecuteNonQueryAsync(token);
        }

        public async Task CleanupAsync(CancellationToken token)
        {
            SqlConnection.ClearAllPools();
            ValidateNames();
            await ExecuteServerAsync(
                $"IF DB_ID(N'{RestoreDatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{RestoreDatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{RestoreDatabaseName}]; END; " +
                $"IF DB_ID(N'{DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}]; END;",
                token);
            if (!string.IsNullOrWhiteSpace(BackupPath) && File.Exists(BackupPath)) File.Delete(BackupPath);
        }

        public async Task<int> TemporaryDatabaseResidueAsync(CancellationToken token)
        {
            await using var connection = new SqlConnection(serverConnection);
            await connection.OpenAsync(token);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name LIKE N'Pharmacy_E2E_Test_%' OR name LIKE N'Pharmacy_E2E_Restore_%';";
            return Convert.ToInt32(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture);
        }

        private async Task ExecuteServerAsync(string sql, CancellationToken token)
        {
            await using var connection = new SqlConnection(serverConnection);
            await connection.OpenAsync(token);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 300;
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(token);
        }

        private static async Task<string> ServerStringAsync(SqlConnection connection, string sql, CancellationToken token)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToString(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture)
                   ?? throw new InvalidOperationException("Chemin SQL Server absent.");
        }

        private void ValidateNames()
        {
            ValidateTarget(DatabaseName);
            ValidateTarget(RestoreDatabaseName);
        }

        private static void ValidateTarget(string databaseName)
        {
            if (!SafeName.IsMatch(databaseName))
                throw new InvalidOperationException("Cible SQL E2E non sûre.");
        }
    }
}
