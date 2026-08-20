using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Desktop.Components.Security;
using Pharmacy.Domain.Common;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class SecurityBootstrapWorkflowTests
{
    [Fact]
    public async Task ZeroUsersSelectsBootstrapAndExistingUsersSelectLoginAfterRestart()
    {
        var empty = CreateCoordinator(new StubAuthentication(Result<bool>.Success(false)), new StubBootstrap());
        await empty.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(empty.IsInitialized);
        Assert.True(empty.RequiresBootstrap);
        Assert.Equal(SecurityEntryMode.BootstrapRequired, empty.EntryMode);

        var restarted = CreateCoordinator(new StubAuthentication(Result<bool>.Success(true)), new StubBootstrap());
        await restarted.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(restarted.IsInitialized);
        Assert.False(restarted.RequiresBootstrap);
        Assert.Equal(SecurityEntryMode.LoginRequired, restarted.EntryMode);
    }

    [Fact]
    public async Task DatabaseUnavailableBlocksBothBootstrapAndLoginFallbackThenAllowsRetry()
    {
        var authentication = new SequencedAuthentication(
            Result<bool>.Failure(SecurityErrors.DatabaseUnavailable),
            Result<bool>.Success(false));
        var bootstrap = new StubBootstrap();
        var coordinator = CreateCoordinator(authentication, bootstrap);

        await coordinator.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(coordinator.IsUnavailable);
        Assert.False(coordinator.RequiresBootstrap);
        Assert.Equal(SecurityEntryMode.Unavailable, coordinator.EntryMode);
        Assert.Equal(SecurityErrors.DatabaseUnavailable.Message, coordinator.Message);

        await coordinator.BootstrapAsync("owner", "Owner", "Owner-Strong-Password-2026!", TestContext.Current.CancellationToken);
        Assert.Equal(0, bootstrap.CallCount);

        await coordinator.RetryInitializationAsync(TestContext.Current.CancellationToken);
        Assert.Equal(SecurityEntryMode.BootstrapRequired, coordinator.EntryMode);
        Assert.True(coordinator.RequiresBootstrap);
    }

    [Fact]
    public async Task DoubleSubmitCallsBootstrapOnceAndTransitionsToLogin()
    {
        var bootstrap = new BlockingBootstrap();
        var coordinator = CreateCoordinator(new StubAuthentication(Result<bool>.Success(false)), bootstrap);
        await coordinator.InitializeAsync(TestContext.Current.CancellationToken);

        var first = coordinator.BootstrapAsync(
            "owner.local", "Propriétaire", "Owner-Strong-Password-2026!", TestContext.Current.CancellationToken);
        await bootstrap.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var second = coordinator.BootstrapAsync(
            "owner.local", "Propriétaire", "Owner-Strong-Password-2026!", TestContext.Current.CancellationToken);
        bootstrap.Release.TrySetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, bootstrap.CallCount);
        Assert.Equal(SecurityEntryMode.LoginRequired, coordinator.EntryMode);
        Assert.False(coordinator.RequiresBootstrap);
        Assert.Equal(2, coordinator.RecoveryCodes.Count);
    }

    [Fact]
    public void SecurityEntryHasDedicatedUnavailableStateWithoutLoginFallback()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "Pharmacy.Desktop", "Components", "Security", "SecurityEntry.razor"));
        Assert.Contains("@if (Security.IsUnavailable)", source, StringComparison.Ordinal);
        Assert.Contains("Réessayer la détection", source, StringComparison.Ordinal);
        Assert.Contains("else if (Security.RequiresBootstrap)", source, StringComparison.Ordinal);
    }

    private static SecurityCoordinator CreateCoordinator(
        IAuthenticationService authentication,
        ISecurityBootstrapService bootstrap) =>
        new(authentication, bootstrap, new StubAdministration(), new StubAuditReader(), new CurrentSecuritySession());

    private sealed class StubAuthentication(Result<bool> hasUsers) : IAuthenticationService
    {
        public Task<Result<bool>> HasUsersAsync(CancellationToken cancellationToken = default) => Task.FromResult(hasUsers);
        public Task<Result<AuthenticationResultView>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<AuthenticationResultView>> UnlockAsync(LoginRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> LockAsync(Guid correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> LogoutAsync(Guid correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> ReauthenticateAsync(string password, Guid correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> ChangePasswordAsync(string currentPassword, string newPassword, Guid correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> RecoverPasswordAsync(string login, string recoveryCode, string newPassword, Guid correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class SequencedAuthentication(params Result<bool>[] results) : IAuthenticationService
    {
        private int index;
        public Task<Result<bool>> HasUsersAsync(CancellationToken cancellationToken = default)
        {
            var current = Math.Min(Interlocked.Increment(ref index) - 1, results.Length - 1);
            return Task.FromResult(results[current]);
        }
        public Task<Result<AuthenticationResultView>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<AuthenticationResultView>> UnlockAsync(LoginRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> LockAsync(Guid correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> LogoutAsync(Guid correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> ReauthenticateAsync(string password, Guid correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> ChangePasswordAsync(string currentPassword, string newPassword, Guid correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> RecoverPasswordAsync(string login, string recoveryCode, string newPassword, Guid correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private class StubBootstrap : ISecurityBootstrapService
    {
        public int CallCount { get; protected set; }
        public virtual Task<Result<RecoveryCodeBatchView>> BootstrapOwnerAsync(BootstrapOwnerRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Result<RecoveryCodeBatchView>.Failure(SecurityErrors.BootstrapAlreadyCompleted));
        }
    }

    private sealed class BlockingBootstrap : StubBootstrap
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async Task<Result<RecoveryCodeBatchView>> BootstrapOwnerAsync(BootstrapOwnerRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return Result<RecoveryCodeBatchView>.Success(new(
                1, new[] { "RECOVERY-0001", "RECOVERY-0002" }, DateTime.UtcNow, DateTime.UtcNow.AddDays(365)));
        }
    }

    private sealed class StubAdministration : ISecurityAdministrationService
    {
        public Task<Result<IReadOnlyList<SecurityUserSummaryView>>> GetUsersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<IReadOnlyList<SecurityRoleView>>> GetRolesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<IReadOnlyList<SecurityPermissionView>>> GetPermissionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<SecurityUserSummaryView>> CreateUserAsync(CreateSecurityUserRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<SecurityUserSummaryView>> SetUserActiveAsync(SetUserActiveRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<SecurityUserSummaryView>> SetUserRolesAsync(SetUserRolesRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<RecoveryCodeBatchView>> GenerateRecoveryCodesAsync(long userId, Guid operationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<SecurityRoleView>> CreateRoleAsync(CreateSecurityRoleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<SecurityRoleView>> UpdateRoleAsync(UpdateSecurityRoleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubAuditReader : IAuditReader
    {
        public Task<Result<IReadOnlyList<AuditEventView>>> SearchAsync(AuditSearchQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Pharmacy.slnx"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Racine du dépôt introuvable.");
    }
}
