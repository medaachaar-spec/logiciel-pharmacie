namespace Pharmacy.Application.Operations.Models;

public enum ManualTerminalOutcome { Accepted, Declined, Cancelled }

public sealed record ReceiptLineData(string Description, int Quantity, decimal UnitPrice, decimal LineTotal);
public sealed record ReceiptPrintRequest(
    Guid OperationId,
    long SaleId,
    string SaleReference,
    DateTime CompletedAtUtc,
    IReadOnlyList<ReceiptLineData> Lines,
    decimal Total,
    string PaymentMethod,
    bool IsReprint);
public sealed record ReceiptPrintResult(bool Succeeded, string Status, string? ErrorCode, int AttemptCount);
public sealed record ManualTerminalResult(ManualTerminalOutcome Outcome, string Status, DateTime ConfirmedAtUtc);
public sealed record DeviceDiagnosticItem(string Device, string Status, string Detail, bool IsReady);
public sealed record DeviceDiagnosticView(DateTime CheckedAtUtc, IReadOnlyList<DeviceDiagnosticItem> Items);

public sealed class HidScannerBuffer
{
    private readonly TimeSpan maximumGap;
    private readonly TimeSpan duplicateWindow;
    private readonly System.Text.StringBuilder buffer = new();
    private DateTime? lastKeyAtUtc;
    private string? lastCompletedCode;
    private DateTime? lastCompletedAtUtc;

    public HidScannerBuffer(TimeSpan? maximumGap = null, TimeSpan? duplicateWindow = null)
    {
        this.maximumGap = maximumGap ?? TimeSpan.FromMilliseconds(120);
        this.duplicateWindow = duplicateWindow ?? TimeSpan.FromMilliseconds(500);
    }

    public string? Push(char key, DateTime occurredAtUtc)
    {
        if (key is '\r' or '\n')
        {
            if (buffer.Length == 0) return null;
            var code = buffer.ToString();
            buffer.Clear();
            lastKeyAtUtc = null;
            if (string.Equals(code, lastCompletedCode, StringComparison.Ordinal)
                && lastCompletedAtUtc.HasValue
                && occurredAtUtc - lastCompletedAtUtc.Value <= duplicateWindow) return null;
            lastCompletedCode = code;
            lastCompletedAtUtc = occurredAtUtc;
            return code;
        }

        if (char.IsControl(key)) return null;
        if (lastKeyAtUtc.HasValue && occurredAtUtc - lastKeyAtUtc.Value > maximumGap) buffer.Clear();
        buffer.Append(key);
        lastKeyAtUtc = occurredAtUtc;
        return null;
    }

    public void Reset() { buffer.Clear(); lastKeyAtUtc = null; }
}
