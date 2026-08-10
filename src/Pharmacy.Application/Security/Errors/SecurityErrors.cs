using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Security.Errors;

public static class SecurityErrors
{
    public static Error InvalidCredentials { get; } = new(
        "security.authentication.invalid_credentials",
        "Identifiant ou mot de passe incorrect.");

    public static Error AccountLocked { get; } = new(
        "security.authentication.locked",
        "Ce compte est temporairement verrouillé. Réessayez plus tard.");

    public static Error AccountDisabled { get; } = new(
        "security.authentication.invalid_credentials",
        "Identifiant ou mot de passe incorrect.");

    public static Error PasswordPolicyFailed { get; } = new(
        "security.password.policy_failed",
        "Le mot de passe doit contenir entre 15 et 128 caractères et ne pas être courant.");

    public static Error AuthenticationRequired { get; } = new(
        "security.authorization.authentication_required",
        "Authentifiez-vous pour continuer.");

    public static Error PermissionDenied { get; } = new(
        "security.authorization.denied",
        "Vous n’avez pas la permission requise.");

    public static Error ReauthenticationRequired { get; } = new(
        "security.authorization.reauthentication_required",
        "Cette opération sensible exige une réauthentification récente.");

    public static Error BootstrapAlreadyCompleted { get; } = new(
        "security.bootstrap.already_completed",
        "Le compte propriétaire initial existe déjà.");

    public static Error UserNotFound { get; } = new(
        "security.user.not_found",
        "Le compte demandé est introuvable.");

    public static Error LoginAlreadyExists { get; } = new(
        "security.user.login_exists",
        "Cet identifiant est déjà utilisé.");

    public static Error ConcurrencyConflict { get; } = new(
        "security.concurrency.conflict",
        "Les informations de sécurité ont changé. Rechargez-les avant de recommencer.");

    public static Error RecoveryCodeInvalid { get; } = new(
        "security.recovery.invalid",
        "Le code de récupération est invalide ou déjà utilisé.");

    public static Error DatabaseUnavailable { get; } = new(
        "security.database.unavailable",
        "Le service de données local est indisponible. Les opérations métier sont temporairement bloquées.");
}
