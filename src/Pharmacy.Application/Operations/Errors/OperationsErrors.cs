using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Operations.Errors;

public static class OperationsErrors
{
    public static Error InvalidOperationId { get; } = new("operations.operation_id.invalid", "L’identifiant d’opération est obligatoire.");
    public static Error BackupPathNotConfigured { get; } = new("backup.path.not_configured", "Le dossier de sauvegarde local n’est pas configuré ou accessible.");
    public static Error BackupPathInvalid { get; } = new("backup.path.invalid", "Le chemin de sauvegarde n’est pas autorisé.");
    public static Error BackupNotFound { get; } = new("backup.file.not_found", "Le fichier de sauvegarde demandé est introuvable.");
    public static Error DatabaseUnavailable { get; } = new("operations.database.unavailable", "SQL Server est indisponible pour cette opération.");
    public static Error InvalidImportFile { get; } = new("import.file.invalid", "Le fichier doit être un CSV UTF-8 valide et respecter la limite de taille.");
    public static Error InvalidImportMapping { get; } = new("import.mapping.invalid", "Le mapping ne correspond pas aux colonnes du fichier.");
    public static Error ImportValidationFailed { get; } = new("import.validation.failed", "L’import contient des erreurs bloquantes ; corrigez le fichier avant confirmation.");
}
