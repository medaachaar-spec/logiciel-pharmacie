using Pharmacy.Application.Dashboard.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Dashboard.Contracts;

public interface IDashboardService
{
    Task<Result<DashboardSalesView>> GetGlobalSalesAsync(
        DashboardSalesQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<DashboardCreditView>> GetOpenCreditAsync(
        CancellationToken cancellationToken = default);

    Task<Result<DashboardInventoryView>> GetInventoryAlertsAsync(
        int expiryLimit = 6,
        CancellationToken cancellationToken = default);
}
