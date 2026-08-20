using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Customers.Models;
using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Operations.Contracts;
using Pharmacy.Application.Operations.Models;
using Pharmacy.Application.Sales.Catalog.Models;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Commands;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Finalization.Models;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Catalog;
using Pharmacy.Infrastructure.Inventory;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Sales;
using Pharmacy.Infrastructure.Sales.InMemory;
using Pharmacy.Desktop.Components.Pos;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class SqlSalesGateIntegrationTests(SqlSalesGateFixture fixture) : IClassFixture<SqlSalesGateFixture>
{
    private CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SAL0601_OneHundredPercentExternalCardSalePersists()
    {
        var scenario = await fixture.PrepareAsync("0601");
        var sale = await fixture.FinalizeAsync(scenario, Token);

        Assert.True(sale.IsSuccess, sale.IsFailure ? sale.Error.Code : null);
        Assert.Equal(1, await fixture.CountAsync("sales.Sales", sale.Value.SaleId));
        Assert.Equal(1, await fixture.CountAsync("sales.SaleLines", sale.Value.SaleId));
        Assert.Equal(1, await fixture.CountAsync("sales.Payments", sale.Value.SaleId));
        Assert.Equal(scenario.InitialStock - 1, await fixture.StockAsync(scenario.ProductId));
    }

    [Fact]
    public async Task SAL0602_ExternalCardPaymentModeAndAmountAreExact()
    {
        var scenario = await fixture.PrepareAsync("0602", price: 13.25m, quantity: 2);
        var sale = (await fixture.FinalizeAsync(scenario, Token)).Value;

        Assert.Equal(SalePaymentMethod.ExternalCard, sale.Payment.Method);
        Assert.Equal(26.50m, sale.Payment.Amount.Amount);
        Assert.True(sale.Payment.ExternalCardConfirmed);
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sales.Payments WHERE SaleId=@id AND PaymentMethod=N'ExternalCard' AND Amount=26.5 AND TenderedAmount IS NULL AND ChangeGiven=0 AND ExternalCardConfirmed=1;",
            P("@id", sale.SaleId)));
    }

    [Fact]
    public async Task SAL0603_ExternalCardCreatesNoArtificialCashOrDebt()
    {
        var scenario = await fixture.PrepareAsync("0603");
        var sale = (await fixture.FinalizeAsync(scenario, Token)).Value;

        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM cash.CashLedgerEntries WHERE SourceType=N'Sale' AND SourceReference=@reference;",
            PText("@reference", sale.Reference)));
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM customers.CustomerLedgerEntries WHERE SourceType=N'Sale' AND SourceReference=@reference;",
            PText("@reference", sale.Reference)));
        Assert.Equal(0m, sale.CreditAmount.Amount);
    }

    [Fact]
    public async Task SAL0604_ExternalCardRequiresExplicitExternalConfirmation()
    {
        var scenario = await fixture.PrepareAsync("0604");
        var rejected = await fixture.FinalizeAsync(
            scenario with
            {
                Request = scenario.Request with
                {
                    Payment = scenario.Request.Payment with { ExternalCardConfirmed = false }
                }
            },
            Token);

        Assert.Equal("sales.payment.card_unconfirmed", rejected.Error.Code);
        await fixture.AssertNoSaleMutationAsync(scenario, Token);
    }

    [Fact]
    public async Task SAL0605_UnconfirmedOrDeclinedManualTerminalIsSafe()
    {
        var scenario = await fixture.PrepareAsync("0605");
        var rejected = await fixture.FinalizeAsync(
            scenario with
            {
                Request = scenario.Request with
                {
                    Payment = scenario.Request.Payment with { ExternalCardConfirmed = false }
                }
            },
            Token);

        Assert.True(rejected.IsFailure);
        await fixture.AssertNoSaleMutationAsync(scenario, Token);
        Assert.Equal(0, await fixture.ScalarAsync<int>("SELECT COUNT(*) FROM cash.CashLedgerEntries WHERE SourceReference LIKE N'%0605%';"));
    }

    [Fact]
    public async Task SAL0606_SalesSqlSchemaContainsNoSensitiveCardColumns()
    {
        var count = await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id " +
            "WHERE s.name=N'sales' AND (LOWER(c.name) LIKE N'%pan%' OR LOWER(c.name) LIKE N'%cvv%' OR LOWER(c.name) LIKE N'%cardnumber%' " +
            "OR LOWER(c.name) LIKE N'%trackdata%' OR LOWER(c.name)=N'pin' OR LOWER(c.name) LIKE N'%secret%');");

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task SAL0607_ExternalCardDoubleSubmitIsIdempotentAndConflictingPayloadIsRejected()
    {
        var scenario = await fixture.PrepareAsync("0607");
        var first = (await fixture.FinalizeAsync(scenario, Token)).Value;
        var replay = await fixture.FinalizeAsync(scenario, Token);
        var conflict = await fixture.FinalizeAsync(
            scenario with
            {
                Request = scenario.Request with
                {
                    Payment = new SalePaymentRequest(
                        SalePaymentMethod.Cash,
                        scenario.Request.Preparation.Total.Amount,
                        scenario.Request.Preparation.Total.Amount,
                        0m,
                        false)
                }
            },
            Token);

        Assert.Equal(first.SaleId, replay.Value.SaleId);
        Assert.Equal("sales.operation.conflict", conflict.Error.Code);
        Assert.Equal(1, await fixture.CountAsync("sales.Sales", first.SaleId));
        Assert.Equal(1, await fixture.CountAsync("sales.Payments", first.SaleId));
        Assert.Equal(1, await fixture.SaleMovementCountAsync(first.Reference));
    }

    [Fact]
    public async Task SAL0608_ExternalCardSqlFailureRollsBackAllMutations()
    {
        var scenario = await fixture.PrepareAsync("0608");
        var failed = await fixture.FinalizeAsync(
            scenario,
            Token,
            fixture.CreateFactory(new FailSaleInsertInterceptor()));

        Assert.Equal("sales.persistence.failed", failed.Error.Code);
        await fixture.AssertNoSaleMutationAsync(scenario, Token);
    }

    [Fact]
    public async Task SAL0609_ExternalCardSalePersistsAcrossNewContext()
    {
        var scenario = await fixture.PrepareAsync("0609", lots: true, quantity: 3);
        var committed = (await fixture.FinalizeAsync(scenario, Token)).Value;

        var reloaded = await new SqlSalesReader(fixture.CreateFactory())
            .GetByIdAsync(committed.SaleId, Token);

        Assert.True(reloaded.IsSuccess);
        Assert.Equal(SalePaymentMethod.ExternalCard, reloaded.Value.Payment.Method);
        Assert.Equal(committed.Total.Amount, reloaded.Value.Payment.Amount.Amount);
        Assert.Equal(3, reloaded.Value.TotalQuantity);
        Assert.Equal(2, Assert.Single(reloaded.Value.Lines).Allocations.Count);
    }

    [Fact]
    public async Task SAL0701_SaleCommitsBeforePhysicalPrintFailure()
    {
        var proof = await CommitThenFailPrintAsync("0701");

        Assert.True(proof.PrintHandled);
        Assert.Equal(1, await fixture.CountAsync("sales.Sales", proof.Sale.SaleId));
        Assert.Equal("Completed", proof.Sale.Status);
        Assert.Equal(DemoPrintStatus.Failed, proof.PrintService.Status);
    }

    [Fact]
    public async Task SAL0702_PrintFailureDoesNotRollbackSale()
    {
        var proof = await CommitThenFailPrintAsync("0702");
        var reloaded = await new SqlSalesReader(fixture.CreateFactory()).GetByIdAsync(proof.Sale.SaleId, Token);

        Assert.True(reloaded.IsSuccess);
        Assert.Equal(proof.Sale.Reference, reloaded.Value.Reference);
    }

    [Fact]
    public async Task SAL0703_PrintFailureDoesNotRollbackPayment()
    {
        var proof = await CommitThenFailPrintAsync("0703");

        Assert.Equal(1, await fixture.CountAsync("sales.Payments", proof.Sale.SaleId));
        Assert.Equal(proof.Sale.Total.Amount, await fixture.ScalarAsync<decimal>(
            "SELECT Amount FROM sales.Payments WHERE SaleId=@id;", P("@id", proof.Sale.SaleId)));
    }

    [Fact]
    public async Task SAL0704_PrintFailureDoesNotRestoreStock()
    {
        var proof = await CommitThenFailPrintAsync("0704");

        Assert.Equal(proof.Scenario.InitialStock - 1, await fixture.StockAsync(proof.Scenario.ProductId));
        Assert.Equal(1, await fixture.SaleMovementCountAsync(proof.Sale.Reference));
    }

    [Fact]
    public async Task SAL0705_RetryFinalizeAfterPrintFailureDoesNotDuplicateSale()
    {
        var proof = await CommitThenFailPrintAsync("0705");
        var replay = await fixture.FinalizeAsync(proof.Scenario, Token);

        Assert.Equal(proof.Sale.SaleId, replay.Value.SaleId);
        Assert.Equal(1, await fixture.CountAsync("sales.Sales", proof.Sale.SaleId));
    }

    [Fact]
    public async Task SAL0706_RetryAfterPrintFailureDoesNotDuplicatePaymentOrStockMovement()
    {
        var proof = await CommitThenFailPrintAsync("0706");
        Assert.True((await fixture.FinalizeAsync(proof.Scenario, Token)).IsSuccess);

        Assert.Equal(1, await fixture.CountAsync("sales.Payments", proof.Sale.SaleId));
        Assert.Equal(1, await fixture.SaleMovementCountAsync(proof.Sale.Reference));
        Assert.Equal(proof.Scenario.InitialStock - 1, await fixture.StockAsync(proof.Scenario.ProductId));
    }

    [Fact]
    public async Task SAL0801_HistoricalPriceRemainsAfterCatalogPriceUpdate()
    {
        var scenario = await fixture.PrepareAsync("0801", price: 10m);
        var sale = (await fixture.FinalizeAsync(scenario, Token)).Value;
        await fixture.SetPriceAsync(scenario.ProductId, 20m);

        var history = (await fixture.Reader.GetByIdAsync(sale.SaleId, Token)).Value;
        Assert.Equal(10m, Assert.Single(history.Lines).UnitPrice.Amount);
    }

    [Fact]
    public async Task SAL0802_HistoricalDesignationRemainsAfterCatalogRename()
    {
        var scenario = await fixture.PrepareAsync("0802", name: "Ancien nom SAL0802");
        var sale = (await fixture.FinalizeAsync(scenario, Token)).Value;
        await fixture.ExecuteAsync("UPDATE catalog.Products SET Name=N'Nouveau nom SAL0802' WHERE Id=@id;", P("@id", scenario.ProductId));

        var history = (await fixture.Reader.GetByIdAsync(sale.SaleId, Token)).Value;
        Assert.Equal("Ancien nom SAL0802", Assert.Single(history.Lines).ProductName);
    }

    [Fact]
    public async Task SAL0803_DisabledProductHistoryRemainsReadable()
    {
        var scenario = await fixture.PrepareAsync("0803");
        var sale = (await fixture.FinalizeAsync(scenario, Token)).Value;
        await fixture.ExecuteAsync("UPDATE catalog.Products SET IsActive=0 WHERE Id=@id;", P("@id", scenario.ProductId));

        var history = await new SqlSalesReader(fixture.CreateFactory()).GetByIdAsync(sale.SaleId, Token);
        Assert.True(history.IsSuccess);
        Assert.Single(history.Value.Lines);
    }

    [Fact]
    public async Task SAL0804_HistoricalSaleTotalRemainsStable()
    {
        var scenario = await fixture.PrepareAsync("0804", price: 10m, quantity: 3);
        var sale = (await fixture.FinalizeAsync(scenario, Token)).Value;
        await fixture.SetPriceAsync(scenario.ProductId, 99m);
        await fixture.ExecuteAsync("UPDATE catalog.Products SET Name=N'Modifié SAL0804', IsActive=0 WHERE Id=@id;", P("@id", scenario.ProductId));

        var history = (await fixture.Reader.GetByIdAsync(sale.SaleId, Token)).Value;
        Assert.Equal(30m, history.Total.Amount);
        Assert.Equal(history.Total.Amount, history.Lines.Sum(line => line.Subtotal.Amount));
        Assert.Equal(history.Total.Amount, history.Payment.Amount.Amount + history.CreditAmount.Amount);
    }

    [Fact]
    public async Task SAL0805_FefoAllocationsRemainHistoricalAfterInventoryChanges()
    {
        var scenario = await fixture.PrepareAsync("0805", lots: true, quantity: 3);
        var sale = (await fixture.FinalizeAsync(scenario, Token)).Value;
        await fixture.ExecuteAsync(
            "UPDATE inventory.StockBalances SET AvailableQuantity=AvailableQuantity+5 WHERE ProductId=@id;",
            P("@id", scenario.ProductId));

        var allocations = Assert.Single((await fixture.Reader.GetByIdAsync(sale.SaleId, Token)).Value.Lines).Allocations;
        Assert.Collection(
            allocations,
            allocation => { Assert.Equal(scenario.NearBatchNumber, allocation.BatchNumber); Assert.Equal(2, allocation.Quantity); },
            allocation => { Assert.Equal(scenario.FarBatchNumber, allocation.BatchNumber); Assert.Equal(1, allocation.Quantity); });
    }

    [Fact]
    public async Task SAL0806_ReturnPreparationUsesOriginalPriceAfterCatalogChange()
    {
        var scenario = await fixture.PrepareAsync("0806", price: 10m);
        var sale = (await fixture.FinalizeAsync(scenario, Token)).Value;
        await fixture.SetPriceAsync(scenario.ProductId, 20m);

        var preparation = await fixture.Reader.GetPreparationAsync(sale.SaleId, Token);
        Assert.True(preparation.IsSuccess);
        Assert.Equal(10m, Assert.Single(preparation.Value.Lines).UnitPrice.Amount);
    }

    [Fact]
    public async Task SAL0808_CreditLedgerKeepsFinalizedHistoricalAmount()
    {
        var scenario = await fixture.PrepareAsync("0808", price: 10m, quantity: 2);
        var customerStore = new SqlCustomerCreditCashStore(fixture.Factory, fixture.Clock);
        var customer = await customerStore.CreateAsync(
            new CreateCustomerRequest(Guid.NewGuid(), "Client crédit SAL0808", null, null, true),
            Token);
        Assert.True(customer.IsSuccess);
        var request = scenario.Request with
        {
            Payment = new SalePaymentRequest(SalePaymentMethod.ExternalCard, 12m, null, 0m, true),
            Credit = new SaleCreditRequest(customer.Value.CustomerId, 8m)
        };
        var sale = (await fixture.FinalizeAsync(scenario with { Request = request }, Token)).Value;
        await fixture.SetPriceAsync(scenario.ProductId, 50m);

        Assert.Equal(8m, await fixture.ScalarAsync<decimal>(
            "SELECT Amount FROM customers.CustomerLedgerEntries WHERE CustomerId=@id AND SourceReference=@reference;",
            P("@id", customer.Value.CustomerId), PText("@reference", sale.Reference)));
        Assert.Equal(20m, (await fixture.Reader.GetByIdAsync(sale.SaleId, Token)).Value.Total.Amount);
    }

    [Fact]
    public async Task SAL0809_ReplayDoesNotRecalculateSnapshotAfterCatalogChange()
    {
        var scenario = await fixture.PrepareAsync("0809", price: 10m, name: "Snapshot SAL0809");
        var first = (await fixture.FinalizeAsync(scenario, Token)).Value;
        await fixture.SetPriceAsync(scenario.ProductId, 20m);
        await fixture.ExecuteAsync("UPDATE catalog.Products SET Name=N'Catalogue changé SAL0809' WHERE Id=@id;", P("@id", scenario.ProductId));

        var replay = await fixture.FinalizeAsync(scenario, Token);
        Assert.Equal(first.SaleId, replay.Value.SaleId);
        Assert.Equal(10m, Assert.Single(replay.Value.Lines).UnitPrice.Amount);
        Assert.Equal("Snapshot SAL0809", Assert.Single(replay.Value.Lines).ProductName);
        Assert.Equal(1, await fixture.CountAsync("sales.SaleLines", first.SaleId));
    }

    [Fact]
    public async Task SAL0810_CatalogPriceChangeBeforeFinalizationIsExplicitlyRejected()
    {
        var scenario = await fixture.PrepareAsync("0810", price: 10m);
        await fixture.SetPriceAsync(scenario.ProductId, 20m);

        var rejected = await fixture.FinalizeAsync(scenario, Token);
        Assert.Equal("sales.price.changed", rejected.Error.Code);
        await fixture.AssertNoSaleMutationAsync(scenario, Token);
    }

    private async Task<PostCommitPrintProof> CommitThenFailPrintAsync(string tag)
    {
        var scenario = await fixture.PrepareAsync(tag);
        var sale = (await fixture.FinalizeAsync(scenario, Token)).Value;
        var printService = new DemoTicketPrintService(new ThrowingReceiptPrinter());
        printService.PrepareForSale(sale.Reference);
        var printed = await printService.PrintPersistedAsync(new SalesPosCompletedSale(sale), false, false, Token);
        return new PostCommitPrintProof(scenario, sale, printService, printed);
    }

    private static SqlParameter P(string name, long value) => new(name, SqlDbType.BigInt) { Value = value };
    private static SqlParameter PText(string name, string value) => new(name, SqlDbType.NVarChar, 128) { Value = value };

    private sealed record PostCommitPrintProof(
        SalesGateScenario Scenario,
        FinalizedSaleView Sale,
        DemoTicketPrintService PrintService,
        bool PrintHandled);

    private sealed class ThrowingReceiptPrinter : IReceiptPrinter
    {
        public Task<Result<ReceiptPrintResult>> PrintAsync(
            ReceiptPrintRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Synthetic printer unavailable after SQL commit.");
    }

    private sealed class FailSaleInsertInterceptor : DbCommandInterceptor
    {
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result) => Fail(command, result);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(Fail(command, result));

        private static InterceptionResult<DbDataReader> Fail(
            DbCommand command,
            InterceptionResult<DbDataReader> result)
        {
            if (command.CommandText.Contains("INSERT INTO [sales].[Sales]", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Synthetic SAL0608 failure before commit.");
            }

            return result;
        }
    }
}

public sealed class SqlSalesGateFixture : IAsyncLifetime
{
    private const string Prefix = "Pharmacy_SAL_Gate_Test_";
    private string serverConnection = string.Empty;
    private string databaseConnection = string.Empty;
    private string databaseName = string.Empty;

    public GateClock Clock { get; } = new(new DateOnly(2026, 8, 20), new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc));
    public IDbContextFactory<PharmacyDbContext> Factory { get; private set; } = null!;
    public SqlSalesReader Reader => new(Factory);
    public long CategoryId { get; private set; }
    public long LocationId { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var raw = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("PHARMACY_TEST_SQLSERVER_CONNECTION est requise.");
        }

        serverConnection = new SqlConnectionStringBuilder(raw)
        {
            InitialCatalog = "master",
            Pooling = false,
            ApplicationName = "Pharmacy.SAL.Gate.Tests"
        }.ConnectionString;
        databaseName = Prefix + Guid.NewGuid().ToString("N")[..12];
        ValidateDatabaseName(databaseName);
        await ExecuteServerAsync($"CREATE DATABASE [{databaseName}];");
        databaseConnection = new SqlConnectionStringBuilder(serverConnection)
        {
            InitialCatalog = databaseName,
            Pooling = false
        }.ConnectionString;
        Factory = CreateFactory();

        await using (var context = await Factory.CreateDbContextAsync())
        {
            await context.Database.MigrateAsync();
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
            Assert.Equal(10, (await context.Database.GetAppliedMigrationsAsync()).Count());
        }

        var category = await new SqlCatalogStore(Factory).CreateAsync(
            new CreateCatalogCategoryCommand("SAL-GATE", "Catégorie SAL-06..08"),
            CancellationToken.None);
        Assert.True(category.IsSuccess);
        CategoryId = category.Value.CategoryId;

        var location = await new InventoryCommandHandlers(new SqlInventoryStore(Factory), Clock)
            .CreateLocationAsync(new CreateStockLocationCommand(
                Guid.NewGuid(), "SAL-GATE", "Emplacement SAL-06..08", true));
        Assert.True(location.IsSuccess);
        LocationId = location.Value.LocationId;
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            ValidateDatabaseName(databaseName);
            await ExecuteServerAsync(
                $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END;");
        }

        SqlConnection.ClearAllPools();
    }

    public async Task<SalesGateScenario> PrepareAsync(
        string tag,
        decimal price = 10m,
        int quantity = 1,
        bool lots = false,
        string? name = null)
    {
        var token = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var product = await new CreateCatalogProductHandler(new SqlCatalogStore(Factory)).HandleAsync(
            new CreateCatalogProductCommand(
                name ?? $"Produit SAL{tag}",
                $"Présentation SAL{tag}",
                CategoryId,
                $"SAL-{tag}-{suffix}",
                price,
                TracksLots: lots,
                TracksExpiry: lots),
            token);
        Assert.True(product.IsSuccess, product.IsFailure ? product.Error.Code : null);

        var inventory = new InventoryCommandHandlers(new SqlInventoryStore(Factory), Clock);
        string? nearNumber = null;
        string? farNumber = null;
        if (lots)
        {
            nearNumber = $"N-{tag}-{suffix}";
            farNumber = $"F-{tag}-{suffix}";
            var near = await inventory.CreateBatchAsync(new CreateStockBatchCommand(
                Guid.NewGuid(), product.Value.ProductId, nearNumber, Clock.CurrentDate.AddMonths(1)), token);
            var far = await inventory.CreateBatchAsync(new CreateStockBatchCommand(
                Guid.NewGuid(), product.Value.ProductId, farNumber, Clock.CurrentDate.AddMonths(6)), token);
            Assert.True(near.IsSuccess);
            Assert.True(far.IsSuccess);
            Assert.True((await inventory.RecordOpeningAsync(Opening(product.Value.ProductId, near.Value.BatchId, 2), token)).IsSuccess);
            Assert.True((await inventory.RecordOpeningAsync(Opening(product.Value.ProductId, far.Value.BatchId, 8), token)).IsSuccess);
        }
        else
        {
            Assert.True((await inventory.RecordOpeningAsync(Opening(product.Value.ProductId, null, 10), token)).IsSuccess);
        }

        var catalog = new SqlProductCatalogReader(Factory);
        var availability = new SqlInventoryStockAvailabilityReader(Factory, Clock);
        var store = new InMemorySaleDraftStore();
        var draft = await new CreateSaleDraftHandler(new InMemorySaleDraftIdGenerator(), store).HandleAsync(token);
        Assert.True(draft.IsSuccess);
        var added = await new AddProductToSaleDraftHandler(store, catalog, availability).HandleAsync(
            new AddProductToSaleDraftCommand(
                draft.Value.Id,
                SellableProductLookup.ByProductId(ProductId.Create(product.Value.ProductId).Value),
                SaleQuantity.Create(quantity).Value,
                draft.Value.Version),
            token);
        Assert.True(added.IsSuccess, added.IsFailure ? added.Error.Code : null);
        var operationId = Guid.NewGuid();
        var prepared = await new PrepareSaleFinalizationHandler(
            store,
            availability,
            new InMemorySaleFinalizationPreparationStore()).HandleAsync(
                new PrepareSaleFinalizationCommand(operationId, added.Value.Id, added.Value.Version),
                token);
        Assert.True(prepared.IsSuccess, prepared.IsFailure ? prepared.Error.Code : null);
        var request = new FinalizeSaleRequest(
            prepared.Value,
            new SalePaymentRequest(
                SalePaymentMethod.ExternalCard,
                prepared.Value.Total.Amount,
                null,
                0m,
                true));
        return new SalesGateScenario(
            product.Value.ProductId,
            lots ? 10 : 10,
            operationId,
            request,
            nearNumber,
            farNumber);
    }

    public Task<Result<FinalizedSaleView>> FinalizeAsync(
        SalesGateScenario scenario,
        CancellationToken token,
        IDbContextFactory<PharmacyDbContext>? factory = null) =>
        new SqlSaleFinalizer(factory ?? Factory, Clock).FinalizeAsync(scenario.Request, token);

    public IDbContextFactory<PharmacyDbContext> CreateFactory(params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<PharmacyDbContext>();
        builder.UsePharmacySqlServer(databaseConnection);
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        return new FactoryImpl(builder.Options);
    }

    public async Task AssertNoSaleMutationAsync(SalesGateScenario scenario, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Assert.Equal(0, await ScalarAsync<int>(
            "SELECT COUNT(*) FROM sales.SaleOperations WHERE OperationId=@operation;",
            new SqlParameter("@operation", SqlDbType.UniqueIdentifier) { Value = scenario.OperationId }));
        Assert.Equal(scenario.InitialStock, await StockAsync(scenario.ProductId));
        Assert.Equal(0, await ScalarAsync<int>(
            "SELECT COUNT(*) FROM inventory.StockMovements WHERE MovementType=N'Sale' AND ProductId=@id;",
            new SqlParameter("@id", scenario.ProductId)));
    }

    public Task<int> StockAsync(long productId) => ScalarAsync<int>(
        "SELECT COALESCE(SUM(AvailableQuantity),0) FROM inventory.StockBalances WHERE ProductId=@id;",
        new SqlParameter("@id", productId));

    public Task<int> SaleMovementCountAsync(string reference) => ScalarAsync<int>(
        "SELECT COUNT(*) FROM inventory.StockMovements WHERE MovementType=N'Sale' AND SourceReference=@reference;",
        new SqlParameter("@reference", SqlDbType.NVarChar, 128) { Value = reference });

    public Task<int> CountAsync(string table, long saleId)
    {
        var foreignKey = table == "sales.Sales" ? "Id" : "SaleId";
        return ScalarAsync<int>($"SELECT COUNT(*) FROM {table} WHERE {foreignKey}=@id;", new SqlParameter("@id", saleId));
    }

    public Task SetPriceAsync(long productId, decimal price) => ExecuteAsync(
        "UPDATE catalog.ProductLocalSettings SET CurrentSalePrice=@price WHERE ProductId=@id;",
        new SqlParameter("@price", SqlDbType.Decimal) { Precision = 19, Scale = 4, Value = price },
        new SqlParameter("@id", productId));

    public async Task<T> ScalarAsync<T>(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(databaseConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Valeur SQL SAL absente.");
        return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task ExecuteAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(databaseConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    private RecordStockMovementCommand Opening(long productId, long? batchId, int quantity) => new(
        Guid.NewGuid(),
        StockMovementType.Opening,
        productId,
        LocationId,
        batchId,
        quantity,
        "Ouverture test SAL-06..08",
        "SalesGateTest",
        "SAL");

    private async Task ExecuteServerAsync(string sql)
    {
        await using var connection = new SqlConnection(serverConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static void ValidateDatabaseName(string value)
    {
        if (!Regex.IsMatch(value, "^Pharmacy_SAL_Gate_Test_[0-9a-f]{12}$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("Nom de base temporaire SAL refusé.");
        }
    }

    public sealed class GateClock(DateOnly date, DateTime utcNow) : ISalesClock, IInventoryDateProvider
    {
        public DateOnly CurrentDate { get; } = date;
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FactoryImpl(DbContextOptions<PharmacyDbContext> options) : IDbContextFactory<PharmacyDbContext>
    {
        public PharmacyDbContext CreateDbContext() => new(options);
        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}

public sealed record SalesGateScenario(
    long ProductId,
    int InitialStock,
    Guid OperationId,
    FinalizeSaleRequest Request,
    string? NearBatchNumber,
    string? FarBatchNumber);
