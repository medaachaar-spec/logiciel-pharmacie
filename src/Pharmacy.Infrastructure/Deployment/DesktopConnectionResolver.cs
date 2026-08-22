using Microsoft.Data.SqlClient;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Deployment;

public static class DesktopConnectionResolver
{
    public static string? Resolve()
    {
        var explicitOverride = Environment.GetEnvironmentVariable(
            PharmacyDbContextFactory.ConnectionEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(explicitOverride))
        {
            SqlConnectionStringBuilder builder;
            try
            {
                builder = new SqlConnectionStringBuilder(explicitOverride);
            }
            catch (ArgumentException exception)
            {
                throw InvalidExplicitOverride(
                    "la chaîne de connexion est mal formée",
                    exception);
            }

            if (!string.Equals(
                    builder.InitialCatalog,
                    "Pharmacy_Development",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidExplicitOverride(
                    "la base explicitement ciblée n’est pas autorisée");
            }

            return explicitOverride;
        }

        var path = Environment.GetEnvironmentVariable(PilotConfiguration.ConfigurationEnvironmentVariable)
            ?? PilotConfiguration.DefaultPath;
        return File.Exists(path) ? PilotConfiguration.Load(path).BuildDatabaseConnectionString() : null;
    }

    private static InvalidOperationException InvalidExplicitOverride(
        string reason,
        Exception? innerException = null) => new(
            $"La variable {PharmacyDbContextFactory.ConnectionEnvironmentVariable} est refusée : {reason}. " +
            "Aucune autre base ne sera sélectionnée automatiquement. Pour le développement local, ciblez Pharmacy_Development ; " +
            "pour le pilote, supprimez cette variable et utilisez la configuration pilote documentée.",
            innerException);
}
