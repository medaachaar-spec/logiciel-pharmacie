using Pharmacy.Application.Operations.Contracts;
using Pharmacy.Application.Operations.Models;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Desktop.Components.Pos;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Operations;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class SalesGateDesktopTests
{
    [Fact]
    public void SAL0707_PosUiDistinguishesSaleSuccessFromPrintFailure()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "Pharmacy.Desktop",
            "Components",
            "Pos",
            "DemoPosSuccess.razor"));

        Assert.Contains("Commit SQL terminé", source, StringComparison.Ordinal);
        Assert.Contains("Vente enregistrée", source, StringComparison.Ordinal);
        Assert.Contains("Ticket non imprimé", source, StringComparison.Ordinal);
        Assert.Contains("Vente terminée", source, StringComparison.Ordinal);
        Assert.Contains("aucun paiement ni vente n’est recréé", source, StringComparison.Ordinal);
        Assert.Contains("Réessayer l’impression", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Réessayer la vente", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SAL0708_ReprintDoesNotMutatePersistedBusinessSnapshot()
    {
        var sale = CompletedSale(10m, "Snapshot reprint");
        var printer = new RecordingPrinter();
        var service = new DemoTicketPrintService(printer);
        service.PrepareForSale(sale.Reference);

        Assert.True(await service.PrintPersistedAsync(sale, false, false, TestContext.Current.CancellationToken));
        Assert.True(await service.PrintPersistedAsync(sale, true, false, TestContext.Current.CancellationToken));
        Assert.True(await service.PrintPersistedAsync(sale, true, false, TestContext.Current.CancellationToken));

        Assert.Equal(3, printer.Requests.Count);
        Assert.Equal(2, printer.Requests.Count(request => request.IsReprint));
        Assert.All(printer.Requests, request => Assert.Equal(sale.SaleId, request.SaleId));
        Assert.Equal(10m, sale.Total.Amount);
        Assert.Equal("Snapshot reprint", Assert.Single(sale.Lines).ProductName);
    }

    [Fact]
    public async Task SAL0709_ReprintUsesHistoricalSaleSnapshot()
    {
        var sale = CompletedSale(10m, "Ancienne désignation");
        var printer = new RecordingPrinter();
        var service = new DemoTicketPrintService(printer);
        service.PrepareForSale(sale.Reference);

        Assert.True(await service.PrintPersistedAsync(sale, false, false, TestContext.Current.CancellationToken));
        Assert.True(await service.PrintPersistedAsync(sale, true, false, TestContext.Current.CancellationToken));

        var reprint = Assert.Single(printer.Requests, request => request.IsReprint);
        Assert.Equal(10m, Assert.Single(reprint.Lines).UnitPrice);
        Assert.Equal("Ancienne désignation", Assert.Single(reprint.Lines).Description);
        Assert.Equal(10m, reprint.Total);
    }

    [Fact]
    public async Task SAL0710_PrinterUnavailableIsHandledWithoutRawExceptionOrStuckState()
    {
        var sale = CompletedSale(10m, "Printer unavailable");
        var service = new DemoTicketPrintService(new ThrowingPrinter());
        service.PrepareForSale(sale.Reference);

        var handled = await service.PrintPersistedAsync(
            sale,
            false,
            false,
            TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(DemoPrintStatus.Failed, service.Status);
        Assert.Equal(1, service.AttemptCount);
        Assert.True(service.ContinueWithoutTicket(sale.Reference));
    }

    [Fact]
    public async Task SAL0711_PrintAuditRemainsADeviceEventDistinctFromSaleFinalization()
    {
        var audit = new CapturingAuditRecorder();
        var printer = new SimulatedReceiptPrinter(audit);
        var sale = CompletedSale(10m, "Audit ticket");
        var line = Assert.Single(sale.Lines);

        Assert.True((await printer.PrintAsync(new ReceiptPrintRequest(
            Guid.NewGuid(),
            sale.SaleId,
            sale.Reference,
            sale.CompletedAtUtc,
            [new ReceiptLineData(line.ProductName, line.Quantity.Value, line.UnitPrice.Amount, line.Subtotal.Amount)],
            sale.Total.Amount,
            "ExternalCard",
            false), TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await printer.PrintAsync(new ReceiptPrintRequest(
            Guid.NewGuid(),
            sale.SaleId,
            sale.Reference,
            sale.CompletedAtUtc,
            [new ReceiptLineData(line.ProductName, line.Quantity.Value, line.UnitPrice.Amount, line.Subtotal.Amount)],
            sale.Total.Amount,
            "ExternalCard",
            true), TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await printer.PrintAsync(new ReceiptPrintRequest(
            Guid.NewGuid(), 0, string.Empty, sale.CompletedAtUtc, [], 0m, "ExternalCard", false),
            TestContext.Current.CancellationToken)).IsFailure);

        Assert.Equal(["receipt.print.simulated", "receipt.reprint.simulated"], audit.SuccessActions);
        Assert.Equal(["receipt.print.simulated"], audit.FailureActions);
        Assert.All(audit.Modules, module => Assert.Equal("devices", module));
        Assert.DoesNotContain("sale.finalize", audit.SuccessActions);
        Assert.DoesNotContain("sale.finalize", audit.FailureActions);
    }

    private static SalesPosCompletedSale CompletedSale(decimal price, string name)
    {
        var money = Money.Create(price).Value;
        var line = new FinalizedSaleLineView(
            101,
            ProductId.Create(101).Value,
            ProductReference.Create("SAL-PRINT-101").Value,
            name,
            "Boîte snapshot",
            money,
            SaleQuantity.One,
            money,
            [new SaleAllocationView(201, 301, 401, "LOT-HIST", new DateOnly(2027, 12, 31), 1)]);
        var payment = new SalePaymentView(
            501,
            SalePaymentMethod.ExternalCard,
            money,
            null,
            0m,
            true,
            new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc));
        return new SalesPosCompletedSale(new FinalizedSaleView(
            601,
            "VTE-SAL-PRINT-601",
            Guid.Parse("706b5c30-1111-4222-8333-444444444444"),
            new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc),
            money,
            1,
            [line],
            payment));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Racine du dépôt introuvable.");
    }

    private sealed class RecordingPrinter : IReceiptPrinter
    {
        public List<ReceiptPrintRequest> Requests { get; } = [];

        public Task<Result<ReceiptPrintResult>> PrintAsync(
            ReceiptPrintRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Result<ReceiptPrintResult>.Success(
                new ReceiptPrintResult(true, "Recorded", null, Requests.Count)));
        }
    }

    private sealed class ThrowingPrinter : IReceiptPrinter
    {
        public Task<Result<ReceiptPrintResult>> PrintAsync(
            ReceiptPrintRequest request,
            CancellationToken cancellationToken = default) =>
            throw new IOException("Synthetic physical printer outage.");
    }

    private sealed class CapturingAuditRecorder : IBusinessAuditRecorder
    {
        public List<string> SuccessActions { get; } = [];
        public List<string> FailureActions { get; } = [];
        public List<string> Modules { get; } = [];

        public Task RecordSuccessAsync(
            string module,
            string entityType,
            string? entityId,
            string action,
            Guid? operationId,
            CancellationToken cancellationToken = default)
        {
            Modules.Add(module);
            SuccessActions.Add(action);
            return Task.CompletedTask;
        }

        public Task RecordFailureAsync(
            string module,
            string entityType,
            string? entityId,
            string action,
            Guid? operationId,
            string reasonCode,
            CancellationToken cancellationToken = default)
        {
            Modules.Add(module);
            FailureActions.Add(action);
            return Task.CompletedTask;
        }
    }
}
