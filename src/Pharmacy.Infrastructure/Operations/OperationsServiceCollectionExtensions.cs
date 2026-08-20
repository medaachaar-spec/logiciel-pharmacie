using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Operations.Contracts;

namespace Pharmacy.Infrastructure.Operations;

public static class OperationsServiceCollectionExtensions
{
    public static IServiceCollection AddOperationalServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<SqlBackupRecoveryService>();
        services.AddTransient<IBackupRecoveryService>(p => p.GetRequiredService<SqlBackupRecoveryService>());
        services.AddTransient<SqlControlledImportService>();
        services.AddTransient<IControlledImportService>(p => p.GetRequiredService<SqlControlledImportService>());
        services.AddScoped<IReceiptPrinter, SimulatedReceiptPrinter>();
        services.AddSingleton<IManualPaymentTerminal, ManualPaymentTerminal>();
        services.AddTransient<IDeviceDiagnostics, LocalDeviceDiagnostics>();
        return services;
    }
}
