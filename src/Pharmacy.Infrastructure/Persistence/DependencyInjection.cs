using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Pharmacy.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPharmacySqlServerPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "Une connexion SQL Server non vide est requise.",
                nameof(connectionString));
        }

        services.AddDbContext<PharmacyDbContext>(options =>
            options.UsePharmacySqlServer(connectionString));
        return services;
    }
}
