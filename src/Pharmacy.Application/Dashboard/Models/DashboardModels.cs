namespace Pharmacy.Application.Dashboard.Models;

public sealed record DashboardSalesQuery(int Days, int TopProductLimit = 5)
{
    public bool IsValid => Days is 7 or 30 && TopProductLimit is >= 1 and <= 10;
}

public sealed record DashboardPeriod(DateTime FromUtc, DateTime ToUtc)
{
    public bool IsValid => FromUtc.Kind == DateTimeKind.Utc
        && ToUtc.Kind == DateTimeKind.Utc
        && FromUtc < ToUtc;
}

public sealed record DashboardDailySalesPoint(
    DateOnly Day,
    int SaleCount,
    decimal GrossSales);

public sealed record DashboardTopProductView(
    long ProductId,
    string ProductName,
    int SoldQuantity,
    decimal GrossSales);

public sealed record DashboardSalesView(
    DashboardPeriod TrendPeriod,
    DateTime GeneratedAtUtc,
    int TodaySaleCount,
    decimal TodayGrossSales,
    IReadOnlyList<DashboardDailySalesPoint> Trend,
    IReadOnlyList<DashboardTopProductView> TopProducts);

public sealed record DashboardCreditView(decimal OpenCredit);

public sealed record DashboardExpiryAlertView(
    long BatchId,
    long ProductId,
    string ProductName,
    string? BatchNumber,
    DateOnly ExpiryDate,
    long Quantity,
    int DaysRemaining,
    string Status);

public sealed record DashboardInventoryView(
    bool LowStockAlertsEnabled,
    int LowStockProducts,
    bool ExpiryAlertsEnabled,
    int? ExpiryHorizonDays,
    IReadOnlyList<DashboardExpiryAlertView> ExpiryAlerts);
