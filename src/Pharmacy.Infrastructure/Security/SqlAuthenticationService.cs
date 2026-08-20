using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Application.Security.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Security;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Security;

namespace Pharmacy.Infrastructure.Security;

internal sealed class SqlAuthenticationService : IAuthenticationService, ISecurityBootstrapService
{
    private const int RecoveryCodeCount = 8;
    private static readonly TimeSpan RecoveryCodeLifetime = TimeSpan.FromDays(365);
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;
    private readonly IPasswordHasher<UserAccountEntity> passwordHasher;
    private readonly IRecoveryCodeGenerator recoveryCodeGenerator;
    private readonly ICurrentSecuritySession session;
    private readonly ISecurityClock clock;
    private readonly UserAccountEntity dummyUser = new() { Login = "timing-placeholder", NormalizedLogin = "TIMING-PLACEHOLDER" };
    private readonly string dummyHash;

    public SqlAuthenticationService(
        IDbContextFactory<PharmacyDbContext> contextFactory,
        IPasswordHasher<UserAccountEntity> passwordHasher,
        IRecoveryCodeGenerator recoveryCodeGenerator,
        ICurrentSecuritySession session,
        ISecurityClock clock)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        this.recoveryCodeGenerator = recoveryCodeGenerator ?? throw new ArgumentNullException(nameof(recoveryCodeGenerator));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        dummyHash = passwordHasher.HashPassword(dummyUser, Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
    }

    public async Task<Result<bool>> HasUsersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            return Result<bool>.Success(await context.UserAccounts.AsNoTracking().AnyAsync(cancellationToken));
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<bool>.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    public Task<Result<AuthenticationResultView>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        return AuthenticateAsync(request, "Login", false, cancellationToken);
    }

    public Task<Result<AuthenticationResultView>> UnlockAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        return AuthenticateAsync(request, "Unlock", true, cancellationToken);
    }

    public async Task<Result> LockAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        var user = session.User;
        if (user is null) return Result.Failure(SecurityErrors.AuthenticationRequired);
        session.Lock(clock.UtcNow);
        return await RecordSessionEventAsync("Lock", true, null, correlationId, cancellationToken);
    }

    public async Task<Result> LogoutAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        var user = session.User;
        if (user is null)
        {
            session.Clear();
            return Result.Success();
        }

        var result = await RecordSessionEventAsync("Logout", true, null, correlationId, cancellationToken);
        session.Clear();
        return result;
    }

    public async Task<Result> ReauthenticateAsync(string password, Guid correlationId, CancellationToken cancellationToken = default)
    {
        var current = session.User;
        if (current is null || session.IsLocked) return Result.Failure(SecurityErrors.AuthenticationRequired);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var user = await context.UserAccounts.SingleOrDefaultAsync(candidate => candidate.Id == current.UserId, cancellationToken);
            var now = clock.UtcNow;
            var locked = user?.LockedUntilUtc > now;
            var success = user is not null
                && user.IsActive
                && !locked
                && passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
            var failureReason = locked ? "locked" : "invalid_credentials";
            context.AuthenticationEvents.Add(SecurityPersistenceSupport.AuthenticationEvent(
                "Reauthentication", user?.Id, current.Login.ToUpperInvariant(), now, success,
                success ? null : failureReason, correlationId));
            context.AuditEvents.Add(SecurityPersistenceSupport.AuditEvent(
                "Security", "security", "UserAccount", current.UserId.ToString(), "reauthenticate",
                current.UserId, current.DisplayName, success ? "Success" : "Failure", now, null, correlationId,
                success ? null : failureReason));
            await context.SaveChangesAsync(cancellationToken);
            if (!success) return Result.Failure(locked ? SecurityErrors.AccountLocked : SecurityErrors.InvalidCredentials);
            session.MarkReauthenticated(now);
            return Result.Success();
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var policy = SecurityPersistenceSupport.ValidatePassword(newPassword);
        if (policy.IsFailure) return policy;
        var current = session.User;
        if (current is null || session.IsLocked) return Result.Failure(SecurityErrors.AuthenticationRequired);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var user = await context.UserAccounts.SingleOrDefaultAsync(candidate => candidate.Id == current.UserId, cancellationToken);
            if (user is null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            {
                return Result.Failure(SecurityErrors.InvalidCredentials);
            }
            var now = clock.UtcNow;
            user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
            user.MustChangePassword = false;
            user.SecurityStamp = Guid.NewGuid();
            user.UpdatedAtUtc = now;
            context.AuthenticationEvents.Add(SecurityPersistenceSupport.AuthenticationEvent(
                "PasswordChange", user.Id, user.NormalizedLogin, now, true, null, correlationId));
            context.AuditEvents.Add(SecurityPersistenceSupport.AuditEvent(
                "Security", "security", "UserAccount", user.Id.ToString(), "password.change",
                user.Id, user.DisplayName, "Success", now, null, correlationId));
            await context.SaveChangesAsync(cancellationToken);
            session.MarkReauthenticated(now);
            return Result.Success();
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result> RecoverPasswordAsync(
        string login,
        string recoveryCode,
        string newPassword,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var policy = SecurityPersistenceSupport.ValidatePassword(newPassword);
        if (policy.IsFailure) return policy;
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(recoveryCode))
            return Result.Failure(SecurityErrors.RecoveryCodeInvalid);

        try
        {
            return await SecurityPersistenceSupport.ExecuteSerializableAsync(
                contextFactory,
                async context =>
                {
                    var normalized = LocalSecurityRules.NormalizeLogin(login);
                    var user = await context.UserAccounts.SingleOrDefaultAsync(candidate => candidate.NormalizedLogin == normalized, cancellationToken);
                    var now = clock.UtcNow;
                    if (user is null)
                    {
                        passwordHasher.VerifyHashedPassword(dummyUser, dummyHash, newPassword);
                        return new SecurityPersistenceSupport.TransactionOutcome<Result>(
                            Result.Failure(SecurityErrors.RecoveryCodeInvalid), false);
                    }
                    var codeHash = SecurityPersistenceSupport.HashRecoveryCode(recoveryCode);
                    var code = await context.RecoveryCodes.SingleOrDefaultAsync(candidate =>
                        candidate.UserAccountId == user.Id && candidate.CodeHash == codeHash
                        && candidate.UsedAtUtc == null && candidate.ExpiresAtUtc > now, cancellationToken);
                    if (code is null)
                    {
                        return new SecurityPersistenceSupport.TransactionOutcome<Result>(
                            Result.Failure(SecurityErrors.RecoveryCodeInvalid), false);
                    }
                    code.UsedAtUtc = now;
                    user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
                    user.SecurityStamp = Guid.NewGuid();
                    user.FailedLoginCount = 0;
                    user.LockedUntilUtc = null;
                    user.MustChangePassword = false;
                    user.UpdatedAtUtc = now;
                    context.AuthenticationEvents.Add(SecurityPersistenceSupport.AuthenticationEvent(
                        "Recovery", user.Id, normalized, now, true, null, correlationId));
                    context.AuditEvents.Add(SecurityPersistenceSupport.AuditEvent(
                        "Security", "security", "UserAccount", user.Id.ToString(), "password.recover",
                        user.Id, user.DisplayName, "Success", now, null, correlationId));
                    await context.SaveChangesAsync(cancellationToken);
                    return new SecurityPersistenceSupport.TransactionOutcome<Result>(Result.Success(), true);
                },
                cancellationToken);
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<RecoveryCodeBatchView>> BootstrapOwnerAsync(
        BootstrapOwnerRequest request,
        CancellationToken cancellationToken = default)
    {
        var policy = SecurityPersistenceSupport.ValidatePassword(request.Password);
        if (policy.IsFailure) return Result<RecoveryCodeBatchView>.Failure(policy.Error);
        if (request.OperationId == Guid.Empty
            || !LocalSecurityRules.IsLoginValid(request.Login)
            || !LocalSecurityRules.IsDisplayNameValid(request.DisplayName))
        {
            return Result<RecoveryCodeBatchView>.Failure(SecurityErrors.InvalidUser);
        }

        try
        {
            return await SecurityPersistenceSupport.ExecuteSerializableAsync(
                contextFactory,
                async context =>
                {
                    await context.Database.ExecuteSqlRawAsync(
                        """
                        DECLARE @lockResult int;
                        EXEC @lockResult = sys.sp_getapplock
                            @Resource = N'Pharmacy.Security.OwnerBootstrap',
                            @LockMode = N'Exclusive',
                            @LockOwner = N'Transaction',
                            @LockTimeout = 30000;
                        IF @lockResult < 0
                            THROW 51000, 'Owner bootstrap lock unavailable.', 1;
                        """,
                        cancellationToken);
                    if (await context.UserAccounts.AnyAsync(cancellationToken))
                    {
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<RecoveryCodeBatchView>>(
                            Result<RecoveryCodeBatchView>.Failure(SecurityErrors.BootstrapAlreadyCompleted), false);
                    }
                    var ownerRole = await context.Roles.SingleAsync(role => role.Code == "OWNER", cancellationToken);
                    var now = clock.UtcNow;
                    var user = new UserAccountEntity
                    {
                        Login = request.Login.Trim(),
                        NormalizedLogin = LocalSecurityRules.NormalizeLogin(request.Login),
                        DisplayName = request.DisplayName.Trim(),
                        IsActive = true,
                        FailedLoginCount = 0,
                        MustChangePassword = false,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now,
                        SecurityStamp = Guid.NewGuid()
                    };
                    user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
                    user.UserRoles.Add(new UserRoleEntity { Role = ownerRole, AssignedAtUtc = now });
                    context.UserAccounts.Add(user);
                    await context.SaveChangesAsync(cancellationToken);
                    var codes = recoveryCodeGenerator.Generate(RecoveryCodeCount);
                    var expiresAt = now.Add(RecoveryCodeLifetime);
                    AddRecoveryCodes(context, user, codes, now, expiresAt);
                    context.AuditEvents.Add(SecurityPersistenceSupport.AuditEvent(
                        "Security", "security", "UserAccount", user.Id.ToString(), "bootstrap.owner",
                        user.Id, user.DisplayName, "Success", now, request.OperationId, request.OperationId));
                    await context.SaveChangesAsync(cancellationToken);
                    return new SecurityPersistenceSupport.TransactionOutcome<Result<RecoveryCodeBatchView>>(
                        Result<RecoveryCodeBatchView>.Success(new RecoveryCodeBatchView(user.Id, codes, now, expiresAt)), true);
                },
                cancellationToken);
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsUniqueViolation(exception))
        {
            return Result<RecoveryCodeBatchView>.Failure(SecurityErrors.LoginAlreadyExists);
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsBootstrapLockUnavailable(exception))
        {
            return Result<RecoveryCodeBatchView>.Failure(SecurityErrors.ConcurrencyConflict);
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<RecoveryCodeBatchView>.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    internal static void AddRecoveryCodes(
        PharmacyDbContext context,
        UserAccountEntity user,
        IReadOnlyList<string> codes,
        DateTime now,
        DateTime expiresAt)
    {
        foreach (var code in codes)
        {
            user.RecoveryCodes.Add(new RecoveryCodeEntity
            {
                CodeHash = SecurityPersistenceSupport.HashRecoveryCode(code),
                CreatedAtUtc = now,
                ExpiresAtUtc = expiresAt
            });
        }
    }

    private async Task<Result<AuthenticationResultView>> AuthenticateAsync(
        LoginRequest request,
        string eventType,
        bool unlocking,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await AuthenticateAttemptAsync(request, eventType, unlocking, cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < LocalSecurityRules.MaximumFailedLoginCount)
            {
                // A concurrent login changed the rowversion. Rebuild the context and re-evaluate
                // the persisted lockout state so failed attempts cannot be lost or bypassed.
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result<AuthenticationResultView>.Failure(SecurityErrors.AccountLocked);
            }
        }
    }

    private async Task<Result<AuthenticationResultView>> AuthenticateAttemptAsync(
        LoginRequest request,
        string eventType,
        bool unlocking,
        CancellationToken cancellationToken)
    {
        var normalized = string.IsNullOrWhiteSpace(request.Login) ? string.Empty : LocalSecurityRules.NormalizeLogin(request.Login);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var user = normalized.Length == 0
                ? null
                : await SecurityPersistenceSupport.UserGraph(context)
                    .SingleOrDefaultAsync(candidate => candidate.NormalizedLogin == normalized, cancellationToken);
            var now = clock.UtcNow;

            if (user is null || unlocking && session.User?.UserId != user.Id)
            {
                passwordHasher.VerifyHashedPassword(dummyUser, dummyHash, request.Password ?? string.Empty);
                context.AuthenticationEvents.Add(SecurityPersistenceSupport.AuthenticationEvent(
                    eventType, null, normalized, now, false, "invalid_credentials", request.CorrelationId));
                context.AuditEvents.Add(SecurityPersistenceSupport.AuditEvent(
                    "Security", "security", "UserAccount", null, eventType.ToLowerInvariant(),
                    null, null, "Failure", now, null, request.CorrelationId, "invalid_credentials"));
                await context.SaveChangesAsync(cancellationToken);
                return Result<AuthenticationResultView>.Failure(SecurityErrors.InvalidCredentials);
            }

            if (user.LockedUntilUtc > now)
            {
                context.AuthenticationEvents.Add(SecurityPersistenceSupport.AuthenticationEvent(
                    eventType, user.Id, normalized, now, false, "locked", request.CorrelationId));
                context.AuditEvents.Add(SecurityPersistenceSupport.AuditEvent(
                    "Security", "security", "UserAccount", user.Id.ToString(), eventType.ToLowerInvariant(),
                    user.Id, user.DisplayName, "Failure", now, null, request.CorrelationId, "locked"));
                await context.SaveChangesAsync(cancellationToken);
                return Result<AuthenticationResultView>.Failure(SecurityErrors.AccountLocked);
            }

            var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? string.Empty);
            if (!user.IsActive || verification == PasswordVerificationResult.Failed)
            {
                user.FailedLoginCount = Math.Min(LocalSecurityRules.MaximumFailedLoginCount, user.FailedLoginCount + 1);
                if (user.FailedLoginCount >= LocalSecurityRules.MaximumFailedLoginCount)
                    user.LockedUntilUtc = now.Add(LocalSecurityRules.AccountLockoutDuration);
                user.UpdatedAtUtc = now;
                context.AuthenticationEvents.Add(SecurityPersistenceSupport.AuthenticationEvent(
                    eventType, user.Id, normalized, now, false,
                    user.LockedUntilUtc > now ? "locked" : "invalid_credentials", request.CorrelationId));
                context.AuditEvents.Add(SecurityPersistenceSupport.AuditEvent(
                    "Security", "security", "UserAccount", user.Id.ToString(), eventType.ToLowerInvariant(),
                    user.Id, user.DisplayName, "Failure", now, null, request.CorrelationId,
                    user.LockedUntilUtc > now ? "locked" : "invalid_credentials"));
                await context.SaveChangesAsync(cancellationToken);
                return user.LockedUntilUtc > now
                    ? Result<AuthenticationResultView>.Failure(SecurityErrors.AccountLocked)
                    : Result<AuthenticationResultView>.Failure(SecurityErrors.InvalidCredentials);
            }

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
                user.PasswordHash = passwordHasher.HashPassword(user, request.Password ?? string.Empty);
            user.FailedLoginCount = 0;
            user.LockedUntilUtc = null;
            user.LastLoginAtUtc = now;
            user.UpdatedAtUtc = now;
            context.AuthenticationEvents.Add(SecurityPersistenceSupport.AuthenticationEvent(
                eventType, user.Id, normalized, now, true, null, request.CorrelationId));
            context.AuditEvents.Add(SecurityPersistenceSupport.AuditEvent(
                "Security", "security", "UserAccount", user.Id.ToString(), eventType.ToLowerInvariant(),
                user.Id, user.DisplayName, "Success", now, null, request.CorrelationId));
            await context.SaveChangesAsync(cancellationToken);

            var mapped = SecurityPersistenceSupport.MapSession(user, now);
            if (unlocking && session.IsAuthenticated)
            {
                session.Unlock(now);
                session.MarkReauthenticated(now);
            }
            else
            {
                session.Establish(mapped, now);
            }
            return Result<AuthenticationResultView>.Success(new AuthenticationResultView(mapped, user.MustChangePassword));
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<AuthenticationResultView>.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    private async Task<Result> RecordSessionEventAsync(
        string eventType,
        bool success,
        string? reason,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var user = session.User;
        if (user is null) return Result.Failure(SecurityErrors.AuthenticationRequired);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var now = clock.UtcNow;
            context.AuthenticationEvents.Add(SecurityPersistenceSupport.AuthenticationEvent(
                eventType, user.UserId, user.Login.ToUpperInvariant(), now, success, reason, correlationId));
            context.AuditEvents.Add(SecurityPersistenceSupport.AuditEvent(
                "Security", "security", "UserAccount", user.UserId.ToString(), eventType.ToLowerInvariant(),
                user.UserId, user.DisplayName, success ? "Success" : "Failure", now, null, correlationId, reason));
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }
}
