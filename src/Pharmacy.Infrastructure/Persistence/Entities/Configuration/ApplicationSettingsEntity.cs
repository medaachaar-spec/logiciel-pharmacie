namespace Pharmacy.Infrastructure.Persistence.Entities.Configuration;

internal sealed class ApplicationSettingsEntity
{
    public long Id { get; set; }
    public bool ReceiptPrintingEnabled { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
