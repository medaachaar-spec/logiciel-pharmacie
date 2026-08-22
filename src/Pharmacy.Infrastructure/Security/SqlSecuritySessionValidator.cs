using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Security;

internal sealed class SqlSecuritySessionValidator : ISecuritySessionValidator
{
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;

    public SqlSecuritySessionValidator(IDbContextFactory<PharmacyDbContext> contextFactory)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public Result ValidateActive(long userId)
    {
        if (userId <= 0)
        {
            return Result.Failure(SecurityErrors.PermissionDenied);
        }

        try
        {
            using var context = contextFactory.CreateDbContext();
            var isActive = context.UserAccounts
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => (bool?)user.IsActive)
                .SingleOrDefault();

            return isActive is true
                ? Result.Success()
                : Result.Failure(SecurityErrors.PermissionDenied);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure(SecurityErrors.DatabaseUnavailable);
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }
}
