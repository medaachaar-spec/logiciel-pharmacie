using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Sales.Contracts;

namespace Pharmacy.Infrastructure.Inventory;

public static class InventoryServiceCollectionExtensions
{
    public static IServiceCollection AddSqlInventoryAdapters(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IInventoryDateProvider, SystemInventoryDateProvider>();
        services.AddTransient<SqlInventoryReader>();
        services.AddTransient<IInventoryReader>(provider => provider.GetRequiredService<SqlInventoryReader>());
        services.AddTransient<SqlInventoryStore>();
        services.AddTransient<IInventoryStore>(provider => provider.GetRequiredService<SqlInventoryStore>());
        services.AddTransient<IStockAvailabilityReader, SqlInventoryStockAvailabilityReader>();
        return services;
    }
}
