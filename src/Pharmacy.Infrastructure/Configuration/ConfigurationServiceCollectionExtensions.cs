using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Configuration;

namespace Pharmacy.Infrastructure.Configuration;

public static class ConfigurationServiceCollectionExtensions
{
    public static IServiceCollection AddSqlApplicationSettings(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<IReceiptPrintingSettingsService, SqlReceiptPrintingSettingsService>();
        return services;
    }
}
