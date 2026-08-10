using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Reporting.Errors;

public static class ReportingErrors
{
    public static Error InvalidPeriod { get; } = new(
        "reporting.period.invalid",
        "La période doit utiliser UTC et sa borne de début doit précéder sa borne de fin.");

    public static Error InvalidLimit { get; } = new(
        "reporting.limit.invalid",
        "La limite technique doit être comprise entre 1 et 1000 lignes.");

    public static Error InvalidSettings { get; } = new(
        "reporting.settings.invalid",
        "Un horizon de péremption activé doit être explicitement défini et positif ou nul.");

    public static Error InvalidOperationId { get; } = new(
        "reporting.operation_id.invalid",
        "L’identifiant d’opération est obligatoire.");

    public static Error DatabaseUnavailable { get; } = new(
        "reporting.database.unavailable",
        "Les rapports SQL sont temporairement indisponibles. Vérifiez la connexion puis réessayez.");
}
