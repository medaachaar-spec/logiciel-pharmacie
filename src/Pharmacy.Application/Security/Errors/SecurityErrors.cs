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

    public static Error InvalidUser { get; } = new(
        "security.user.invalid",
        "L’identifiant et le nom affiché sont obligatoires et ne doivent pas dépasser 128 et 256 caractères.");

    public static Error InvalidRoleSelection { get; } = new(
        "security.user.invalid_role_selection",
        "Sélectionnez au moins un rôle actif existant.");

    public static Error InvalidRole { get; } = new(
        "security.role.invalid",
        "Le code et le nom du rôle sont obligatoires. Le code doit commencer par une lettre et utiliser uniquement lettres, chiffres, point, tiret ou soulignement.");

    public static Error RoleCodeAlreadyExists { get; } = new(
        "security.role.code_exists",
        "Ce code de rôle est déjà utilisé.");

    public static Error RoleNotFound { get; } = new(
        "security.role.not_found",
        "Le rôle demandé est introuvable.");

    public static Error SystemRoleProtected { get; } = new(
        "security.role.system_protected",
        "Ce rôle système est protégé et reste disponible en lecture seule.");

    public static Error LastOwnerProtected { get; } = new(
        "security.owner.last_account_protected",
        "Le dernier compte propriétaire technique actif doit rester protégé.");

    public static Error InvalidPermissionSelection { get; } = new(
        "security.permission.invalid_selection",
        "La sélection contient une permission inexistante. Rechargez la matrice avant de recommencer.");

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
