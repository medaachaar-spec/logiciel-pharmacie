using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Services;
using Pharmacy.Infrastructure.Persistence.Entities.Security;

namespace Pharmacy.Infrastructure.Security;

public static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddLocalSqlSecurity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ISecurityClock, SystemSecurityClock>();
        services.AddSingleton<ICurrentSecuritySession, CurrentSecuritySession>();
        services.AddSingleton<IRecoveryCodeGenerator, CryptographicRecoveryCodeGenerator>();
        services.AddSingleton<IPermissionAuthorizer, PermissionAuthorizer>();
        services.AddSingleton<IPasswordHasher<UserAccountEntity>>(_ =>
            new PasswordHasher<UserAccountEntity>(Options.Create(new PasswordHasherOptions
            {
                CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
                IterationCount = 210_000
            })));
        services.AddScoped<SqlAuthenticationService>();
        services.AddScoped<IAuthenticationService>(provider => provider.GetRequiredService<SqlAuthenticationService>());
        services.AddScoped<ISecurityBootstrapService>(provider => provider.GetRequiredService<SqlAuthenticationService>());
        services.AddScoped<SqlSecurityAdministrationService>();
        services.AddScoped<ISecurityAdministrationService>(provider => provider.GetRequiredService<SqlSecurityAdministrationService>());
        services.AddScoped<SqlAuditStore>();
        services.AddScoped<IAuditReader>(provider => provider.GetRequiredService<SqlAuditStore>());
        services.AddScoped<IAuditWriter>(provider => provider.GetRequiredService<SqlAuditStore>());
        services.AddScoped<IBusinessAuditRecorder, BusinessAuditRecorder>();
        services.AddScoped<IEmployeeActivityReader, SqlEmployeeActivityReader>();
        return services;
    }
}
