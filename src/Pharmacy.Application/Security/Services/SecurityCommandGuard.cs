using Pharmacy.Application.Security.Contracts;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Security.Services;

internal static class SecurityCommandGuard
{
    public static Result<T>? Denied<T>(IPermissionAuthorizer? authorizer, string permission, bool recent = false)
    {
        if (authorizer is null) return null;
        var authorization = authorizer.Authorize(permission, recent);
        return authorization.IsFailure ? Result<T>.Failure(authorization.Error) : null;
    }
}
