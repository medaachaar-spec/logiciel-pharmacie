using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Catalog.Contracts;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Infrastructure.Deployment;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Catalog;

public static class CatalogServiceCollectionExtensions
{
    public static IServiceCollection AddSqlCatalogAdaptersFromEnvironment(
        this IServiceCollection services)
        => services.AddSqlCatalogAdapters(DesktopConnectionResolver.Resolve());

    public static IServiceCollection AddSqlCatalogAdapters(
        this IServiceCollection services,
        string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddPharmacySqlServerPersistence(connectionString!);
        }
        else
        {
            services.AddSingleton<IDbContextFactory<PharmacyDbContext>, UnavailablePharmacyDbContextFactory>();
        }

        services.AddTransient<SqlProductCatalogReader>();
        services.AddTransient<ICatalogProductReader>(provider =>
            provider.GetRequiredService<SqlProductCatalogReader>());
        services.AddTransient<IProductCatalogReader>(provider =>
            provider.GetRequiredService<SqlProductCatalogReader>());
        services.AddTransient<SqlCatalogStore>();
        services.AddTransient<ICatalogProductStore>(provider =>
            provider.GetRequiredService<SqlCatalogStore>());
        services.AddTransient<ICatalogCategoryStore>(provider =>
            provider.GetRequiredService<SqlCatalogStore>());
        services.AddTransient<ICatalogCategoryReader, SqlCatalogCategoryReader>();
        services.AddTransient<ICatalogDiagnosticsReader, SqlCatalogDiagnosticsReader>();
        return services;
    }

}
