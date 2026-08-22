using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Domain.Common;

namespace Pharmacy.UnitTests;

internal sealed class TestPermissionAuthorizer(params string[] permissions) : IPermissionAuthorizer
{
    private readonly HashSet<string> allowed = new(permissions, StringComparer.Ordinal);

    public static IPermissionAuthorizer AllowAll { get; } =
        new TestPermissionAuthorizer(SecurityPermissions.All.ToArray());

    public Result Authorize(string permission, bool requireRecentReauthentication = false) =>
        allowed.Contains(permission)
            ? Result.Success()
            : Result.Failure(SecurityErrors.PermissionDenied);
}
