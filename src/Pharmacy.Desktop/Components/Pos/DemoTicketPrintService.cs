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
    private string? saleReference;

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

    public bool TryBegin(string reference)
    {
        lock (syncRoot)
        {
            if (!Matches(reference)
                || Status is DemoPrintStatus.InProgress or DemoPrintStatus.Printed
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
