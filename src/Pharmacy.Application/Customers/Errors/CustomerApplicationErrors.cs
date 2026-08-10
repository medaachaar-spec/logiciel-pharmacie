using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Customers.Errors;

public static class CustomerApplicationErrors
{
    public static Error InvalidOperationId { get; } = new(
        "customers.operation.invalid",
        "L’identifiant d’opération client ne peut pas être vide.");

    public static Error InvalidCustomerName { get; } = new(
        "customers.name.invalid",
        "Le nom du client est obligatoire.");

    public static Error CustomerNotFound { get; } = new(
        "customers.customer.not_found",
        "Le client demandé est introuvable.");

    public static Error CustomerInactive { get; } = new(
        "customers.customer.inactive",
        "Le client est inactif.");

    public static Error CreditNotAllowed { get; } = new(
        "customers.credit.not_allowed",
        "Le crédit n’est pas autorisé pour ce client.");

    public static Error InvalidCreditPayment { get; } = new(
        "customers.credit.payment.invalid",
        "Le règlement de crédit doit être strictement positif et ne pas dépasser la dette.");

    public static Error OperationConflict { get; } = new(
        "customers.operation.conflict",
        "Cet identifiant d’opération est déjà associé à une demande différente.");

    public static Error ConcurrencyConflict { get; } = new(
        "customers.concurrency.conflict",
        "La fiche client a changé. Rechargez-la avant de réessayer.");

    public static Error DatabaseUnavailable { get; } = new(
        "customers.database.unavailable",
        "Le service de données local est indisponible.");
}
