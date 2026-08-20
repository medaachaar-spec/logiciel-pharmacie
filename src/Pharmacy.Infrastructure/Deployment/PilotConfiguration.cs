using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Pharmacy.Infrastructure.Deployment;

public sealed record PilotConfiguration(
    string ServerInstance,
    string DatabaseName,
    string BackupDirectory)
{
    public const string ConfigurationEnvironmentVariable = "PHARMACY_PILOT_CONFIG";
    private static readonly Regex SafeDatabaseName = new("^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant);

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "LogicielPharmacie", "config", "pilot.json");

    public static PilotConfiguration Load(string? path = null)
    {
        var resolved = string.IsNullOrWhiteSpace(path)
            ? Environment.GetEnvironmentVariable(ConfigurationEnvironmentVariable) ?? DefaultPath
            : path;
        var fullPath = Path.GetFullPath(resolved);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("La configuration pilote est absente.", fullPath);
        }

        var configuration = JsonSerializer.Deserialize<PilotConfiguration>(
            File.ReadAllText(fullPath),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            })
            ?? throw new InvalidDataException("La configuration pilote est vide.");
        configuration = configuration with
        {
            BackupDirectory = Environment.ExpandEnvironmentVariables(configuration.BackupDirectory)
        };
        configuration.Validate();
        return configuration;
    }

    public void Validate()
        => Validate(allowIsolatedTestDatabase: false);

    internal void Validate(bool allowIsolatedTestDatabase)
    {
        if (string.IsNullOrWhiteSpace(ServerInstance) || ServerInstance.Length > 128 || ServerInstance.Contains(';', StringComparison.Ordinal))
        {
            throw new InvalidDataException("L'instance SQL pilote est invalide.");
        }

        if (!SafeDatabaseName.IsMatch(DatabaseName))
        {
            throw new InvalidDataException("Le nom de base pilote est invalide.");
        }

        var isolatedTest = allowIsolatedTestDatabase && DatabaseName.StartsWith("Pharmacy_Sprint18_Test_", StringComparison.Ordinal);
        if (!string.Equals(DatabaseName, "Pharmacy", StringComparison.OrdinalIgnoreCase) && !isolatedTest)
        {
            throw new InvalidDataException("La base pilote autorisée doit être nommée Pharmacy.");
        }

        if (string.IsNullOrWhiteSpace(BackupDirectory) || !Path.IsPathFullyQualified(BackupDirectory))
        {
            throw new InvalidDataException("Le dossier de sauvegarde doit être un chemin absolu.");
        }
    }

    public string BuildDatabaseConnectionString()
        => BuildDatabaseConnectionString(allowIsolatedTestDatabase: false);

    internal string BuildDatabaseConnectionString(bool allowIsolatedTestDatabase)
    {
        Validate(allowIsolatedTestDatabase);
        return new SqlConnectionStringBuilder
        {
            DataSource = ServerInstance,
            InitialCatalog = DatabaseName,
            IntegratedSecurity = true,
            Encrypt = true,
            TrustServerCertificate = true,
            MultipleActiveResultSets = false,
            ApplicationName = "Logiciel Pharmacie Pilot",
            ConnectTimeout = 15
        }.ConnectionString;
    }

    public string BuildMasterConnectionString()
        => BuildMasterConnectionString(allowIsolatedTestDatabase: false);

    internal string BuildMasterConnectionString(bool allowIsolatedTestDatabase)
    {
        var builder = new SqlConnectionStringBuilder(BuildDatabaseConnectionString(allowIsolatedTestDatabase))
        {
            InitialCatalog = "master",
            Pooling = false
        };
        return builder.ConnectionString;
    }
}
