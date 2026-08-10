using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Purchasing.Contracts;

namespace Pharmacy.Infrastructure.Purchasing;

public static class PurchasingServiceCollectionExtensions
{
    public static IServiceCollection AddSqlPurchasingAdapters(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IPurchasingClock, SystemPurchasingClock>();
        services.AddTransient<SqlPurchasingReader>();
        services.AddTransient<IPurchasingReader>(provider => provider.GetRequiredService<SqlPurchasingReader>());
        services.AddTransient<SqlPurchasingStore>();
        services.AddTransient<IPurchasingStore>(provider => provider.GetRequiredService<SqlPurchasingStore>());
        return services;
    }
}
