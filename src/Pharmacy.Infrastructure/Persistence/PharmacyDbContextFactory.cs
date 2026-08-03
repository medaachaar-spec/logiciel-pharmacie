using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class PharmacyDbContextFactory
    : IDesignTimeDbContextFactory<PharmacyDbContext>
{
    public const string ConnectionEnvironmentVariable = "PHARMACY_SQLSERVER_CONNECTION";

    public PharmacyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"La variable d’environnement {ConnectionEnvironmentVariable} est requise pour les opérations EF Core.");
        }

        var options = new DbContextOptionsBuilder<PharmacyDbContext>();
        options.UsePharmacySqlServer(connectionString);
        return new PharmacyDbContext(options.Options);
    }
}
