namespace Pharmacy.Infrastructure.Persistence.Entities.Reporting;

internal sealed class ReportSettingsEntity
{
    public long Id { get; set; }
    public bool LowStockAlertsEnabled { get; set; }
    public bool ExpiryAlertsEnabled { get; set; }
    public int? ExpiryHorizonDays { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
