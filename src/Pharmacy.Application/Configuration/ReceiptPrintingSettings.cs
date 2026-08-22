using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Configuration;

public sealed record ReceiptPrintingSettingsView(
    bool ReceiptPrintingEnabled,
    DateTime UpdatedAtUtc,
    byte[] RowVersion);

public sealed record UpdateReceiptPrintingSettingsRequest(
    Guid OperationId,
    bool ReceiptPrintingEnabled,
    byte[] RowVersion);

public interface IReceiptPrintingSettingsService
{
    Task<Result<ReceiptPrintingSettingsView>> GetAsync(
        CancellationToken cancellationToken = default);

    Task<Result<ReceiptPrintingSettingsView>> UpdateAsync(
        UpdateReceiptPrintingSettingsRequest request,
        CancellationToken cancellationToken = default);
}

public interface IReceiptPrintingPolicy
{
    bool ReceiptPrintingEnabled { get; }
}

public static class ReceiptPrintingSettingsDefaults
{
    public const bool ReceiptPrintingEnabled = false;
}

public static class ReceiptPrintingSettingsErrors
{
    public static readonly Error DatabaseUnavailable = new(
        "configuration.database.unavailable",
        "Les paramètres de l’application sont indisponibles.");

    public static readonly Error ConcurrencyConflict = new(
        "configuration.concurrency.conflict",
        "Les paramètres ont été modifiés. Actualisez puis réessayez.");
}
