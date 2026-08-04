using Microsoft.EntityFrameworkCore;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Catalog;

internal sealed class UnavailablePharmacyDbContextFactory : IDbContextFactory<PharmacyDbContext>
{
    public PharmacyDbContext CreateDbContext()
    {
        throw new InvalidOperationException("La connexion Catalogue SQL n’est pas configurée.");
    }

    public Task<PharmacyDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<PharmacyDbContext>(
            new InvalidOperationException("La connexion Catalogue SQL n’est pas configurée."));
    }
}
