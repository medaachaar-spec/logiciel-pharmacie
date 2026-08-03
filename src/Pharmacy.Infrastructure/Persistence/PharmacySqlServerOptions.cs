using Microsoft.EntityFrameworkCore;

namespace Pharmacy.Infrastructure.Persistence;

public static class PharmacySqlServerOptions
{
    public const int CompatibilityLevel = 170;

    public static DbContextOptionsBuilder UsePharmacySqlServer(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "Une connexion SQL Server non vide est requise.",
                nameof(connectionString));
        }

        optionsBuilder.UseSqlServer(connectionString, sqlServerOptions =>
        {
            sqlServerOptions.UseCompatibilityLevel(CompatibilityLevel);
            sqlServerOptions.EnableRetryOnFailure();
            sqlServerOptions.MigrationsAssembly(typeof(PharmacyDbContext).Assembly.GetName().Name);
        });

        return optionsBuilder;
    }
}
