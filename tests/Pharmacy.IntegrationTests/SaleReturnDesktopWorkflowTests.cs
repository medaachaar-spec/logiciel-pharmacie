using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Finalization.Queries;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Desktop.Components.Pos;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class SaleReturnDesktopWorkflowTests
{
    [Fact]
    public void RET002_01_DesktopHistoryExposesThePersistentReturnWorkflow()
    {
        var page = File.ReadAllText(DesktopPath("Components", "Pages", "DemoPosPage.razor"));
        var panel = File.ReadAllText(DesktopPath("Components", "Pos", "SaleReturnPanel.razor"));
        var services = File.ReadAllText(DesktopPath("SalesServiceCollectionExtensions.cs"));

        Assert.Contains("OpenReturnAsync(sale.SaleId)", page, StringComparison.Ordinal);
        Assert.Contains("Retour", page, StringComparison.Ordinal);
        Assert.Contains("SaleReturnPanel", page, StringComparison.Ordinal);
        Assert.Contains("quantité encore retournable", panel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AddScoped<SalesReturnCoordinator>", services, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RET002_02_LoadsSoldReturnedAndReturnableQuantitiesExactly()
    {
        var fixture = Fixture.Create();

        Assert.True(await fixture.Coordinator.LoadAsync(42, TestContext.Current.CancellationToken));

        Assert.Equal("VTE-RET-002", fixture.Coordinator.Preparation!.Reference);
        Assert.Collection(
            fixture.Coordinator.Selections,
            first =>
            {
                Assert.Equal(3, first.Line.SoldQuantity);
                Assert.Equal(1, first.Line.ReturnedQuantity);
                Assert.Equal(2, first.Line.ReturnableQuantity);
            },
            second =>
            {
                Assert.Equal(2, second.Line.SoldQuantity);
                Assert.Equal(2, second.Line.ReturnedQuantity);
                Assert.Equal(0, second.Line.ReturnableQuantity);
            });
    }

    [Fact]
    public async Task RET002_06_07_DesktopEstimateUsesHistoricalSnapshotPrice()
    {
        var fixture = Fixture.Create();
        await fixture.Coordinator.LoadAsync(42, TestContext.Current.CancellationToken);
        var line = fixture.Coordinator.Selections[0];

        fixture.Coordinator.SetSelected(line.Line.SaleLineId, true);
        fixture.Coordinator.SetQuantity(line.Line.SaleLineId, 2);

        Assert.Equal(100m, fixture.Coordinator.EstimatedRefund);
        Assert.True(fixture.Coordinator.BeginConfirmation());
    }

    [Fact]
    public async Task RET002_13_DoubleSubmitCallsBackendOnlyOnceAndKeepsOneOperationId()
    {
        var fixture = Fixture.Create(blockReturn: true);
        await fixture.Coordinator.LoadAsync(42, TestContext.Current.CancellationToken);
        fixture.Coordinator.SetSelected(7, true);
        Assert.True(fixture.Coordinator.BeginConfirmation());

        var first = fixture.Coordinator.ConfirmAsync(TestContext.Current.CancellationToken);
        await fixture.Store.WaitUntilEnteredAsync(TestContext.Current.CancellationToken);
        var second = await fixture.Coordinator.ConfirmAsync(TestContext.Current.CancellationToken);
        fixture.Store.Release();

        Assert.False(second);
        Assert.True(await first);
        Assert.Equal(1, fixture.Store.CallCount);
        Assert.NotEqual(Guid.Empty, fixture.Store.LastRequest!.OperationId);
        Assert.NotNull(fixture.Coordinator.LastResult);
    }

    private static SaleReturnPreparationView Preparation() => new(
        42,
        "VTE-RET-002",
        new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc),
        SalePaymentMethod.Cash,
        Money.Create(190m).Value,
        null,
        Money.Zero,
        new[]
        {
            new SaleReturnableLineView(
                7,
                ProductId.Create(101).Value,
                ProductReference.Create("M1").Value,
                "Médicament 1",
                "Boîte",
                Money.Create(50m).Value,
                3,
                1,
                2),
            new SaleReturnableLineView(
                8,
                ProductId.Create(102).Value,
                ProductReference.Create("M2").Value,
                "Médicament 2",
                null,
                Money.Create(20m).Value,
                2,
                2,
                0)
        },
        Array.Empty<SaleReturnView>());

    private static string DesktopPath(params string[] segments) =>
        Path.Combine(new[] { RepositoryRoot }.Concat(new[] { "src", "Pharmacy.Desktop" }).Concat(segments).ToArray());

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Repository root not found.");
        }
    }

    private sealed record Fixture(
        SalesReturnCoordinator Coordinator,
        RecordingReturnStore Store)
    {
        public static Fixture Create(bool blockReturn = false)
        {
            var reader = new StubReturnReader(Preparation());
            var store = new RecordingReturnStore(reader, blockReturn);
            var handler = new ReturnSaleHandler(store, new AllowAuthorizer(), new NoOpAudit());
            return new Fixture(
                new SalesReturnCoordinator(
                    new GetSaleReturnPreparationHandler(reader),
                    handler),
                store);
        }
    }

    private sealed class StubReturnReader(SaleReturnPreparationView preparation) : ISaleReturnReader
    {
        public SaleReturnPreparationView Preparation { get; set; } = preparation;

        public Task<Result<SaleReturnPreparationView>> GetPreparationAsync(
            long saleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(saleId == Preparation.SaleId
                ? Result<SaleReturnPreparationView>.Success(Preparation)
                : Result<SaleReturnPreparationView>.Failure(
                    Pharmacy.Application.Sales.Errors.SalesApplicationErrors.SaleNotFound));
    }

    private sealed class RecordingReturnStore(StubReturnReader reader, bool blockReturn) : ISaleReturnStore
    {
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public ReturnSaleRequest? LastRequest { get; private set; }

        public Task WaitUntilEnteredAsync(CancellationToken cancellationToken) =>
            entered.Task.WaitAsync(cancellationToken);

        public void Release() => released.TrySetResult();

        public async Task<Result<SaleReturnView>> ReturnAsync(
            ReturnSaleRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            entered.TrySetResult();
            if (blockReturn)
            {
                await released.Task.WaitAsync(cancellationToken);
            }

            var lineRequest = Assert.Single(request.Lines);
            var original = reader.Preparation.Lines.Single(line => line.SaleLineId == lineRequest.SaleLineId);
            var result = new SaleReturnView(
                1,
                "RET-TEST-001",
                request.SaleId,
                Money.Create(original.UnitPrice.Amount * lineRequest.Quantity).Value,
                request.RefundMethod,
                new DateTime(2026, 8, 16, 11, 0, 0, DateTimeKind.Utc),
                new[]
                {
                    new SaleReturnLineView(
                        original.SaleLineId,
                        original.ProductId,
                        lineRequest.Quantity,
                        original.UnitPrice,
                        Money.Create(original.UnitPrice.Amount * lineRequest.Quantity).Value,
                        lineRequest.StockDestination)
                });
            reader.Preparation = reader.Preparation with
            {
                Lines = reader.Preparation.Lines.Select(line => line.SaleLineId == original.SaleLineId
                    ? line with
                    {
                        ReturnedQuantity = line.ReturnedQuantity + lineRequest.Quantity,
                        ReturnableQuantity = line.ReturnableQuantity - lineRequest.Quantity
                    }
                    : line).ToArray(),
                PreviousReturns = new[] { result }
            };
            return Result<SaleReturnView>.Success(result);
        }
    }

    private sealed class AllowAuthorizer : IPermissionAuthorizer
    {
        public Result Authorize(string permission, bool requireRecentReauthentication = false) => Result.Success();
    }

    private sealed class NoOpAudit : IBusinessAuditRecorder
    {
        public Task RecordSuccessAsync(
            string module,
            string entityType,
            string? entityId,
            string action,
            Guid? operationId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
