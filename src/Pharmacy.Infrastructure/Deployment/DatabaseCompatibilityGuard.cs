using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Deployment;

public sealed record DatabaseCompatibilityResult(bool IsCompatible, string Message);

public static class DatabaseCompatibilityGuard
{
    public static async Task<DatabaseCompatibilityResult> CheckAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        var factory = services.GetRequiredService<IDbContextFactory<PharmacyDbContext>>();
        try
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            if (!await context.Database.CanConnectAsync(cancellationToken))
            {
                return new(false, "La base SQL configurée est inaccessible.");
            }

            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            return pending.Length == 0
                ? new(true, "Schéma SQL compatible.")
                : new(false, $"Schéma SQL incompatible : {pending.Length} migration(s) contrôlée(s) requise(s).");
        }
        catch (Exception ex) when (ex is InvalidOperationException or Microsoft.Data.SqlClient.SqlException)
        {
            return new(false, "Le contrôle de compatibilité SQL a échoué. Exécutez l'outil de déploiement.");
        }
    }
}
