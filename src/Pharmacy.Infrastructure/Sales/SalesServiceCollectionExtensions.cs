using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Customers.Contracts;
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
        services.AddTransient<SqlCustomerCreditCashStore>();
        services.AddTransient<ICustomerStore>(provider => provider.GetRequiredService<SqlCustomerCreditCashStore>());
        services.AddTransient<ICreditPaymentStore>(provider => provider.GetRequiredService<SqlCustomerCreditCashStore>());
        services.AddTransient<ISuspendedSaleStore>(provider => provider.GetRequiredService<SqlCustomerCreditCashStore>());
        services.AddTransient<ISaleReturnStore>(provider => provider.GetRequiredService<SqlCustomerCreditCashStore>());
        return services;
    }
}
