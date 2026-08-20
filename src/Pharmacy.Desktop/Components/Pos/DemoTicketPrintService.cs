using Pharmacy.Application.Operations.Contracts;
using Pharmacy.Application.Operations.Models;

namespace Pharmacy.Desktop.Components.Pos;

public enum DemoPrintStatus
{
    NotRequested,
    InProgress,
    Printed,
    Failed
}

public enum DemoPrintSimulationOutcome
{
    Success,
    Failure
}

public sealed class DemoTicketPrintService
{
    private readonly object syncRoot = new();
    private readonly IReceiptPrinter? printer;
    private string? saleReference;

    public DemoTicketPrintService() { }
    public DemoTicketPrintService(IReceiptPrinter printer) => this.printer = printer;

    public DemoPrintStatus Status { get; private set; } = DemoPrintStatus.NotRequested;

    public int AttemptCount { get; private set; }

    public DateTimeOffset? LastAttemptAtLocal { get; private set; }

    public bool ContinuedWithoutTicket { get; private set; }

    public void PrepareForSale(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        lock (syncRoot)
        {
            saleReference = reference;
            Status = DemoPrintStatus.NotRequested;
            AttemptCount = 0;
            LastAttemptAtLocal = null;
            ContinuedWithoutTicket = false;
        }
    }

    public bool TryBegin(string reference, bool isReprint = false)
    {
        lock (syncRoot)
        {
            if (!Matches(reference)
                || Status == DemoPrintStatus.InProgress
                || (Status == DemoPrintStatus.Printed && !isReprint)
                || ContinuedWithoutTicket)
            {
                return false;
            }

            Status = DemoPrintStatus.InProgress;
            AttemptCount++;
            LastAttemptAtLocal = DateTimeOffset.Now;
            return true;
        }
    }

    public async Task<bool> PrintPersistedAsync(
        SalesPosCompletedSale sale,
        bool isReprint,
        bool simulateFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sale);
        if (!TryBegin(sale.Reference, isReprint)) return false;

        try
        {
            if (simulateFailure)
            {
                await Task.Delay(120, cancellationToken);
                return Complete(sale.Reference, DemoPrintSimulationOutcome.Failure);
            }

            if (printer is null)
            {
                await Task.Delay(120, cancellationToken);
                return Complete(sale.Reference, DemoPrintSimulationOutcome.Success);
            }

            var request = new ReceiptPrintRequest(
                Guid.NewGuid(), sale.SaleId, sale.Reference, sale.CompletedAtUtc,
                sale.Lines.Select(line => new ReceiptLineData(
                    line.ProductName, line.Quantity.Value, line.UnitPrice.Amount, line.Subtotal.Amount)).ToArray(),
                sale.Total.Amount,
                sale.PaymentMethod == DemoPaymentMethod.Cash ? "Cash" : "ExternalCard",
                isReprint);
            var result = await printer.PrintAsync(request, cancellationToken);
            return Complete(sale.Reference, result.IsSuccess && result.Value.Succeeded
                ? DemoPrintSimulationOutcome.Success
                : DemoPrintSimulationOutcome.Failure);
        }
        catch (OperationCanceledException)
        {
            Complete(sale.Reference, DemoPrintSimulationOutcome.Failure);
            throw;
        }
        catch (Exception)
        {
            return Complete(sale.Reference, DemoPrintSimulationOutcome.Failure);
        }
    }

    public bool Complete(string reference, DemoPrintSimulationOutcome outcome)
    {
        lock (syncRoot)
        {
            if (!Matches(reference) || Status != DemoPrintStatus.InProgress)
            {
                return false;
            }

            Status = outcome == DemoPrintSimulationOutcome.Success
                ? DemoPrintStatus.Printed
                : DemoPrintStatus.Failed;
            LastAttemptAtLocal = DateTimeOffset.Now;
            return true;
        }
    }

    public bool ContinueWithoutTicket(string reference)
    {
        lock (syncRoot)
        {
            if (!Matches(reference) || Status != DemoPrintStatus.Failed)
            {
                return false;
            }

            ContinuedWithoutTicket = true;
            return true;
        }
    }

    public void Reset()
    {
        lock (syncRoot)
        {
            saleReference = null;
            Status = DemoPrintStatus.NotRequested;
            AttemptCount = 0;
            LastAttemptAtLocal = null;
            ContinuedWithoutTicket = false;
        }
    }

    private bool Matches(string reference)
    {
        return string.Equals(saleReference, reference, StringComparison.Ordinal);
    }
}
