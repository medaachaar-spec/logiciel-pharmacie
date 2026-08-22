using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Dashboard.Errors;

public static class DashboardErrors
{
    public static Error InvalidSalesQuery { get; } = new(
        "dashboard.sales_query.invalid",
        "La période du tableau de bord doit être de 7 ou 30 jours.");

    public static Error InvalidExpiryLimit { get; } = new(
        "dashboard.expiry_limit.invalid",
        "Le nombre d’alertes demandé doit être compris entre 1 et 20.");

    public static Error DatabaseUnavailable { get; } = new(
        "dashboard.database.unavailable",
        "Cette partie du tableau de bord est temporairement indisponible.");
}
