using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Branding.Contracts;

namespace Pharmacy.Infrastructure.Branding;

public static class BrandingServiceCollectionExtensions
{
    public static IServiceCollection AddSqlPharmacyBranding(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<SqlPharmacyBrandingService>();
        services.AddTransient<IPharmacyBrandingService>(provider =>
            provider.GetRequiredService<SqlPharmacyBrandingService>());
        return services;
    }
}
