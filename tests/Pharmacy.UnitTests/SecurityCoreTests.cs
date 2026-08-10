using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class SecurityCoreTests
{
    private static readonly DateTime Now = new(2026, 8, 10, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void LocalPasswordPolicyDefinesBoundariesAndNormalizesLogin()
    {
        Assert.False(LocalSecurityRules.IsPasswordLengthValid("short"));
        Assert.True(LocalSecurityRules.IsPasswordLengthValid(new string('a', 15)));
        Assert.True(LocalSecurityRules.IsPasswordLengthValid(new string('a', 128)));
        Assert.False(LocalSecurityRules.IsPasswordLengthValid(new string('a', 129)));
        Assert.Equal("OWNER.LOCAL", LocalSecurityRules.NormalizeLogin(" owner.local "));
        Assert.Equal(5, LocalSecurityRules.MaximumFailedLoginCount);
        Assert.Equal(TimeSpan.FromMinutes(5), LocalSecurityRules.AccountLockoutDuration);
    }

    [Fact]
    public void SessionLocksAfterTenMinutesAndCanBeExplicitlyUnlocked()
    {
        var session = new CurrentSecuritySession();
        session.Establish(User(SecurityPermissions.SalesCreate), Now);
        Assert.False(session.LockIfInactive(Now.AddMinutes(9).AddSeconds(59)));
        Assert.True(session.LockIfInactive(Now.AddMinutes(10)));
        Assert.True(session.IsLocked);
        session.Unlock(Now.AddMinutes(10));
        Assert.False(session.IsLocked);
    }

    [Fact]
    public void AuthorizationIsDenyByDefaultAndSensitiveActionRequiresRecentReauthentication()
    {
        var session = new CurrentSecuritySession();
        var clock = new FixedClock(Now);
        var authorizer = new PermissionAuthorizer(session, clock);
        Assert.Equal("security.authorization.authentication_required", authorizer.Authorize(SecurityPermissions.SalesCreate).Error.Code);

        session.Establish(User(SecurityPermissions.CatalogChangePrice), Now.AddMinutes(-6));
        Assert.Equal("security.authorization.denied", authorizer.Authorize(SecurityPermissions.SalesCreate).Error.Code);
        Assert.Equal("security.authorization.reauthentication_required", authorizer.Authorize(SecurityPermissions.CatalogChangePrice, true).Error.Code);
        session.MarkReauthenticated(Now);
        Assert.True(authorizer.Authorize(SecurityPermissions.CatalogChangePrice, true).IsSuccess);
    }

    [Fact]
    public async Task DirectBackendReturnCallIsDeniedWithoutRecentReauthentication()
    {
        var session = new CurrentSecuritySession();
        session.Establish(User(SecurityPermissions.SalesReturn, SecurityPermissions.SalesRefund), Now.AddMinutes(-6));
        var handler = new ReturnSaleHandler(
            new NeverCalledReturnStore(),
            new PermissionAuthorizer(session, new FixedClock(Now)),
            new NoOpAuditRecorder());
        var result = await handler.HandleAsync(new ReturnSaleRequest(
            Guid.NewGuid(), 1, new[] { new ReturnSaleLineRequest(1, 1, ReturnStockDestination.RestockApproved) }, ReturnRefundMethod.Cash),
            TestContext.Current.CancellationToken);
        Assert.Equal("security.authorization.reauthentication_required", result.Error.Code);
    }

    private static UserSessionView User(params string[] permissions) => new(
        1, "owner", "Propriétaire", new[] { "OWNER" }, permissions.ToHashSet(StringComparer.Ordinal),
        Now.AddMinutes(-6), Now.AddMinutes(-6));

    private sealed class FixedClock(DateTime now) : Pharmacy.Application.Security.Contracts.ISecurityClock
    {
        public DateTime UtcNow { get; } = now;
    }

    private sealed class NeverCalledReturnStore : ISaleReturnStore
    {
        public Task<Result<SaleReturnView>> ReturnAsync(ReturnSaleRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Le store ne doit pas être appelé après un refus RBAC.");
    }

    private sealed class NoOpAuditRecorder : IBusinessAuditRecorder
    {
        public Task RecordSuccessAsync(string module, string entityType, string? entityId, string action, Guid? operationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
