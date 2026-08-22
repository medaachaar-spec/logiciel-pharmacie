using Pharmacy.Application.Configuration;

namespace Pharmacy.Desktop.Components.Configuration;

public sealed class ReceiptPrintingSettingsCoordinator : IReceiptPrintingPolicy
{
    private readonly IReceiptPrintingSettingsService service;
    private int operationInProgress;

    public ReceiptPrintingSettingsCoordinator(IReceiptPrintingSettingsService service)
    {
        this.service = service;
        Current = DefaultView();
    }

    public event Action? Changed;

    public ReceiptPrintingSettingsView Current { get; private set; }

    public bool ReceiptPrintingEnabled => Current.ReceiptPrintingEnabled;

    public bool IsBusy { get; private set; }

    public bool IsUsingFallback { get; private set; } = true;

    public string? Message { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Current = DefaultView();
        IsUsingFallback = true;
        var result = await service.GetAsync(cancellationToken);
        if (result.IsFailure)
        {
            Message = result.Error.Message;
            NotifyChanged();
            return;
        }

        Current = result.Value;
        IsUsingFallback = false;
        Message = null;
        NotifyChanged();
    }

    public async Task<bool> UpdateAsync(
        bool receiptPrintingEnabled,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref operationInProgress, 1, 0) != 0)
        {
            return false;
        }

        IsBusy = true;
        Message = null;
        NotifyChanged();
        try
        {
            var result = await service.UpdateAsync(
                new UpdateReceiptPrintingSettingsRequest(
                    Guid.NewGuid(),
                    receiptPrintingEnabled,
                    Current.RowVersion.ToArray()),
                cancellationToken);
            if (result.IsFailure)
            {
                Message = result.Error.Message;
                return false;
            }

            Current = result.Value;
            IsUsingFallback = false;
            Message = receiptPrintingEnabled
                ? "Impression des tickets activée."
                : "Impression des tickets désactivée.";
            return true;
        }
        finally
        {
            IsBusy = false;
            Interlocked.Exchange(ref operationInProgress, 0);
            NotifyChanged();
        }
    }

    private static ReceiptPrintingSettingsView DefaultView() => new(
        ReceiptPrintingSettingsDefaults.ReceiptPrintingEnabled,
        DateTime.UnixEpoch,
        []);

    private void NotifyChanged() => Changed?.Invoke();
}
