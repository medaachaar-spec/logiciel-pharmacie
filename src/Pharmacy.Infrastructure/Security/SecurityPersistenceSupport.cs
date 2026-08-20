using System.Security.Cryptography;
using System.Text;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Application.Security.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Security;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Audit;
using Pharmacy.Infrastructure.Persistence.Entities.Security;

namespace Pharmacy.Infrastructure.Security;

internal static class SecurityPersistenceSupport
{
    internal readonly record struct TransactionOutcome<T>(T Result, bool Commit);
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "passwordpassword",
        "motdepassemotdepasse",
        "123456789012345",
        "azertyazertyazerty",
        "qwertyqwertyqwerty",
        "adminadminadmin"
    };

    public static Result ValidatePassword(string? password)
    {
        if (!LocalSecurityRules.IsPasswordLengthValid(password)
            || CommonPasswords.Contains(password!))
        {
            return Result.Failure(SecurityErrors.PasswordPolicyFailed);
        }
        return Result.Success();
    }

    public static string HashRecoveryCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var normalized = code.Replace("-", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    public static string HashLoginAttempt(string normalizedLogin)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedLogin)));
    }

    public static string WorkstationId { get; } = CreateWorkstationId();

    public static IQueryable<UserAccountEntity> UserGraph(PharmacyDbContext context)
    {
        return context.UserAccounts
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .ThenInclude(role => role.RolePermissions)
            .ThenInclude(rolePermission => rolePermission.Permission);
    }

    public static UserSessionView MapSession(UserAccountEntity user, DateTime nowUtc)
    {
        var roles = user.UserRoles
            .Where(userRole => userRole.Role.IsActive)
            .Select(userRole => userRole.Role.Code)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var permissions = user.UserRoles
            .Where(userRole => userRole.Role.IsActive)
            .SelectMany(userRole => userRole.Role.RolePermissions)
            .Select(rolePermission => rolePermission.Permission)
            .Where(permission => permission.Code.Length > 0)
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.Ordinal);
        return new UserSessionView(
            user.Id,
            user.Login,
            user.DisplayName,
            Array.AsReadOnly(roles),
            permissions,
            nowUtc,
            nowUtc);
    }

    public static AuthenticationEventEntity AuthenticationEvent(
        string eventType,
        long? userId,
        string normalizedLogin,
        DateTime nowUtc,
        bool success,
        string? failureReason,
        Guid correlationId)
    {
        return new AuthenticationEventEntity
        {
            EventType = eventType,
            UserAccountId = userId,
            UsernameAttemptHash = HashLoginAttempt(normalizedLogin),
            WorkstationId = WorkstationId,
            OccurredAtUtc = nowUtc,
            WasSuccessful = success,
            FailureReasonCode = failureReason,
            CorrelationId = correlationId
        };
    }

    public static AuditEventEntity AuditEvent(
        string eventType,
        string module,
        string entityType,
        string? entityId,
        string action,
        long? actorUserId,
        string? actorDisplayName,
        string result,
        DateTime nowUtc,
        Guid? operationId,
        Guid correlationId,
        string? reasonCode = null)
    {
        return new AuditEventEntity
        {
            EventType = eventType,
            Module = module,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorUserId = actorUserId,
            ActorDisplayName = actorDisplayName,
            WorkstationId = WorkstationId,
            Result = result,
            OccurredAtUtc = nowUtc,
            OperationId = operationId,
            CorrelationId = correlationId,
            ReasonCode = reasonCode
        };
    }

    public static bool IsDatabaseUnavailable(Exception exception)
    {
        return Sales.SalesSqlFailure.IsDatabaseUnavailable(exception);
    }

    public static bool IsUniqueViolation(Exception exception)
    {
        return Sales.SalesSqlFailure.IsUniqueViolation(exception);
    }

    public static bool IsBootstrapLockUnavailable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is SqlException { Number: 51000 }) return true;
        }
        return false;
    }

    public static async Task<T> ExecuteSerializableAsync<T>(
        IDbContextFactory<PharmacyDbContext> factory,
        Func<PharmacyDbContext, Task<TransactionOutcome<T>>> operation,
        CancellationToken cancellationToken)
    {
        await using var strategyContext = await factory.CreateDbContextAsync(cancellationToken);
        var strategy = strategyContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var outcome = await operation(context);
            if (outcome.Commit) await transaction.CommitAsync(cancellationToken);
            else await transaction.RollbackAsync(cancellationToken);
            return outcome.Result;
        });
    }

    private static string CreateWorkstationId()
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName)));
        return $"LOCAL-{hash[..16]}";
    }
}
