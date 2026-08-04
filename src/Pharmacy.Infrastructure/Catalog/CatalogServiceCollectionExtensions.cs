using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Catalog.Contracts;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Catalog;

public static class CatalogServiceCollectionExtensions
{
    public static IServiceCollection AddSqlCatalogAdaptersFromEnvironment(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var connectionString = Environment.GetEnvironmentVariable(
            PharmacyDbContextFactory.ConnectionEnvironmentVariable);

        if (TargetsDevelopmentDatabase(connectionString))
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

    private static bool TargetsDevelopmentDatabase(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return string.Equals(
                builder.InitialCatalog,
                "Pharmacy_Development",
                StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
