using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Catalog.Contracts;
using Pharmacy.Application.Catalog.Errors;
using Pharmacy.Application.Catalog.Models;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Catalog;

public sealed class SqlCatalogDiagnosticsReader : ICatalogDiagnosticsReader
{
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;

    public SqlCatalogDiagnosticsReader(IDbContextFactory<PharmacyDbContext> contextFactory)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<CatalogDiagnosticsView> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var databaseName = context.Database.GetDbConnection().Database;
            var reachable = await context.Database.CanConnectAsync(cancellationToken);

            if (!reachable)
            {
                return Unavailable(databaseName, true);
            }

            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
            var migrationStatus = pending.Length == 0
                ? $"À jour — {applied.LastOrDefault() ?? "aucune migration"}"
                : $"{pending.Length} migration(s) en attente";
            var demoCount = await context.ProductLocalSettings
                .AsNoTracking()
                .CountAsync(settings => settings.Product.IsActive
                    && settings.IsActive
                    && settings.InternalCode != null
                    && settings.InternalCode.StartsWith("DEMO-"),
                    cancellationToken);

            return new CatalogDiagnosticsView(
                "SQL Server",
                databaseName,
                true,
                true,
                migrationStatus,
                demoCount,
                null);
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            return Unavailable("Non configurée", false);
        }
    }

    private static CatalogDiagnosticsView Unavailable(string databaseName, bool configured) =>
        new(
            "SQL Server",
            databaseName,
            configured,
            false,
            "Indisponible",
            0,
            CatalogApplicationErrors.DatabaseUnavailable.Code);
}
