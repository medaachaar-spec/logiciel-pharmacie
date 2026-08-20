using Microsoft.Data.SqlClient;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Deployment;

public static class DesktopConnectionResolver
{
    public static string? Resolve()
    {
        var development = Environment.GetEnvironmentVariable(PharmacyDbContextFactory.ConnectionEnvironmentVariable);
        if (TargetsDatabase(development, "Pharmacy_Development"))
        {
            return development;
        }

        var path = Environment.GetEnvironmentVariable(PilotConfiguration.ConfigurationEnvironmentVariable)
            ?? PilotConfiguration.DefaultPath;
        return File.Exists(path) ? PilotConfiguration.Load(path).BuildDatabaseConnectionString() : null;
    }

    private static bool TargetsDatabase(string? connectionString, string databaseName)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;
        try
        {
            return string.Equals(new SqlConnectionStringBuilder(connectionString).InitialCatalog, databaseName, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
