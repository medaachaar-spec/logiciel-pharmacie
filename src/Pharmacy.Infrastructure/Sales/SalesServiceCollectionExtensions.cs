using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Sales.Contracts;

namespace Pharmacy.Infrastructure.Sales;

public static class SalesServiceCollectionExtensions
{
    public static IServiceCollection AddSqlSalesAdapters(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ISalesClock, SystemSalesClock>();
        services.AddTransient<SqlSaleFinalizer>();
        services.AddTransient<ISaleFinalizer>(provider => provider.GetRequiredService<SqlSaleFinalizer>());
        services.AddTransient<SqlSalesReader>();
        services.AddTransient<ISalesReader>(provider => provider.GetRequiredService<SqlSalesReader>());
        return services;
    }
}
