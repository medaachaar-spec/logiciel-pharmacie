using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Reporting.Contracts;

namespace Pharmacy.Infrastructure.Reporting;

public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddSqlReporting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<SqlReportingService>();
        services.AddTransient<IReportingService>(provider => provider.GetRequiredService<SqlReportingService>());
        return services;
    }
}
