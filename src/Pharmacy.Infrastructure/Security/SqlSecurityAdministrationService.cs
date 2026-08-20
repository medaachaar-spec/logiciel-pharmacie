using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Application.Security.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Security;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Audit;
using Pharmacy.Infrastructure.Persistence.Entities.Security;

namespace Pharmacy.Infrastructure.Security;

internal sealed class SqlSecurityAdministrationService : ISecurityAdministrationService
{
    private const int RecoveryCodeCount = 8;
    private static readonly TimeSpan RecoveryCodeLifetime = TimeSpan.FromDays(365);
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;
    private readonly IPasswordHasher<UserAccountEntity> passwordHasher;
    private readonly IRecoveryCodeGenerator recoveryCodeGenerator;
    private readonly IPermissionAuthorizer authorizer;
    private readonly ICurrentSecuritySession session;
    private readonly ISecurityClock clock;

    public SqlSecurityAdministrationService(
        IDbContextFactory<PharmacyDbContext> contextFactory,
        IPasswordHasher<UserAccountEntity> passwordHasher,
        IRecoveryCodeGenerator recoveryCodeGenerator,
        IPermissionAuthorizer authorizer,
        ICurrentSecuritySession session,
        ISecurityClock clock)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        this.recoveryCodeGenerator = recoveryCodeGenerator ?? throw new ArgumentNullException(nameof(recoveryCodeGenerator));
        this.authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<IReadOnlyList<SecurityUserSummaryView>>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var authorization = authorizer.Authorize(SecurityPermissions.SecurityManageUsers);
        if (authorization.IsFailure) return Result<IReadOnlyList<SecurityUserSummaryView>>.Failure(authorization.Error);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var users = await SecurityPersistenceSupport.UserGraph(context).AsNoTracking()
                .OrderBy(user => user.DisplayName).ThenBy(user => user.Id).ToArrayAsync(cancellationToken);
            return Result<IReadOnlyList<SecurityUserSummaryView>>.Success(
                Array.AsReadOnly(users.Select(MapUser).ToArray()));
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<IReadOnlyList<SecurityUserSummaryView>>.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<IReadOnlyList<SecurityRoleView>>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var authorization = authorizer.Authorize(SecurityPermissions.SecurityManageRoles);
        if (authorization.IsFailure) return Result<IReadOnlyList<SecurityRoleView>>.Failure(authorization.Error);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var roles = await context.Roles.AsNoTracking()
                .Include(role => role.RolePermissions).ThenInclude(join => join.Permission)
                .OrderBy(role => role.Name).ThenBy(role => role.Id).ToArrayAsync(cancellationToken);
            return Result<IReadOnlyList<SecurityRoleView>>.Success(
                Array.AsReadOnly(roles.Select(MapRole).ToArray()));
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<IReadOnlyList<SecurityRoleView>>.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<IReadOnlyList<SecurityPermissionView>>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var authorization = authorizer.Authorize(SecurityPermissions.SecurityManageRoles);
        if (authorization.IsFailure) return Result<IReadOnlyList<SecurityPermissionView>>.Failure(authorization.Error);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var permissions = await context.Permissions.AsNoTracking()
                .OrderBy(permission => permission.Module).ThenBy(permission => permission.Code)
                .Select(permission => new SecurityPermissionView(
                    permission.Id, permission.Code, permission.Name, permission.Module,
                    permission.Description, permission.IsSensitive))
                .ToArrayAsync(cancellationToken);
            return Result<IReadOnlyList<SecurityPermissionView>>.Success(Array.AsReadOnly(permissions));
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<IReadOnlyList<SecurityPermissionView>>.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<SecurityRoleView>> CreateRoleAsync(
        CreateSecurityRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = authorizer.Authorize(SecurityPermissions.SecurityManageRoles, true);
        if (authorization.IsFailure) return Result<SecurityRoleView>.Failure(authorization.Error);
        if (request.OperationId == Guid.Empty
            || !LocalSecurityRules.IsRoleCodeValid(request.Code)
            || !LocalSecurityRules.IsRoleNameValid(request.Name))
        {
            return Result<SecurityRoleView>.Failure(SecurityErrors.InvalidRole);
        }

        var code = LocalSecurityRules.NormalizeRoleCode(request.Code);
        var name = request.Name.Trim();
        var permissionIds = request.PermissionIds.Distinct().ToArray();
        try
        {
            return await SecurityPersistenceSupport.ExecuteSerializableAsync(
                contextFactory,
                async context =>
                {
                    var permissions = await context.Permissions
                        .Where(permission => permissionIds.Contains(permission.Id))
                        .OrderBy(permission => permission.Code)
                        .ToArrayAsync(cancellationToken);
                    if (permissions.Length != permissionIds.Length)
                    {
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityRoleView>>(
                            Result<SecurityRoleView>.Failure(SecurityErrors.InvalidPermissionSelection), false);
                    }

                    var role = new RoleEntity
                    {
                        Code = code,
                        Name = name,
                        IsSystemRole = false,
                        IsActive = true
                    };
                    foreach (var permission in permissions)
                    {
                        role.RolePermissions.Add(new RolePermissionEntity { Permission = permission });
                    }

                    context.Roles.Add(role);
                    await context.SaveChangesAsync(cancellationToken);
                    var now = clock.UtcNow;
                    var audit = SecurityPersistenceSupport.AuditEvent(
                        "Administration", "security", "Role", role.Id.ToString(), "role.create",
                        session.User!.UserId, session.User.DisplayName, "Success", now,
                        request.OperationId, request.OperationId);
                    audit.Details.Add(PermissionAuditDetail("PermissionsAdded", null, PermissionCodes(permissions.Select(permission => permission.Code))));
                    context.AuditEvents.Add(audit);
                    await context.SaveChangesAsync(cancellationToken);
                    return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityRoleView>>(
                        Result<SecurityRoleView>.Success(MapRole(role)), true);
                }, cancellationToken);
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsUniqueViolation(exception))
        {
            return Result<SecurityRoleView>.Failure(SecurityErrors.RoleCodeAlreadyExists);
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<SecurityRoleView>.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<SecurityRoleView>> UpdateRoleAsync(
        UpdateSecurityRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = authorizer.Authorize(SecurityPermissions.SecurityManageRoles, true);
        if (authorization.IsFailure) return Result<SecurityRoleView>.Failure(authorization.Error);
        if (request.OperationId == Guid.Empty || request.RoleId <= 0
            || !LocalSecurityRules.IsRoleNameValid(request.Name)
            || request.RowVersion is not { Length: 8 })
        {
            return Result<SecurityRoleView>.Failure(SecurityErrors.InvalidRole);
        }

        var name = request.Name.Trim();
        var permissionIds = request.PermissionIds.Distinct().ToArray();
        try
        {
            return await SecurityPersistenceSupport.ExecuteSerializableAsync(
                contextFactory,
                async context =>
                {
                    var role = await context.Roles
                        .Include(candidate => candidate.RolePermissions)
                        .ThenInclude(join => join.Permission)
                        .SingleOrDefaultAsync(candidate => candidate.Id == request.RoleId, cancellationToken);
                    if (role is null)
                    {
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityRoleView>>(
                            Result<SecurityRoleView>.Failure(SecurityErrors.RoleNotFound), false);
                    }
                    if (role.IsSystemRole || string.Equals(role.Code, "OWNER", StringComparison.Ordinal))
                    {
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityRoleView>>(
                            Result<SecurityRoleView>.Failure(SecurityErrors.SystemRoleProtected), false);
                    }

                    context.Entry(role).Property(entity => entity.RowVersion).OriginalValue = request.RowVersion;
                    var permissions = await context.Permissions
                        .Where(permission => permissionIds.Contains(permission.Id))
                        .OrderBy(permission => permission.Code)
                        .ToArrayAsync(cancellationToken);
                    if (permissions.Length != permissionIds.Length)
                    {
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityRoleView>>(
                            Result<SecurityRoleView>.Failure(SecurityErrors.InvalidPermissionSelection), false);
                    }

                    var previousCodes = role.RolePermissions.Select(join => join.Permission.Code)
                        .ToHashSet(StringComparer.Ordinal);
                    var nextCodes = permissions.Select(permission => permission.Code)
                        .ToHashSet(StringComparer.Ordinal);
                    var addedCodes = nextCodes.Except(previousCodes, StringComparer.Ordinal).Order(StringComparer.Ordinal);
                    var removedCodes = previousCodes.Except(nextCodes, StringComparer.Ordinal).Order(StringComparer.Ordinal);

                    var removedJoins = role.RolePermissions
                        .Where(join => !nextCodes.Contains(join.Permission.Code))
                        .ToArray();
                    context.RolePermissions.RemoveRange(removedJoins);
                    foreach (var removedJoin in removedJoins) role.RolePermissions.Remove(removedJoin);
                    foreach (var permission in permissions.Where(permission => !previousCodes.Contains(permission.Code)))
                    {
                        role.RolePermissions.Add(new RolePermissionEntity { Permission = permission });
                    }
                    var previousName = role.Name;
                    role.Name = name;
                    context.Entry(role).Property(entity => entity.Name).IsModified = true;

                    var now = clock.UtcNow;
                    var audit = SecurityPersistenceSupport.AuditEvent(
                        "Administration", "security", "Role", role.Id.ToString(), "role.permissions.change",
                        session.User!.UserId, session.User.DisplayName, "Success", now,
                        request.OperationId, request.OperationId);
                    audit.Details.Add(PermissionAuditDetail("DisplayName", previousName, name));
                    audit.Details.Add(PermissionAuditDetail("PermissionsAdded", null, PermissionCodes(addedCodes)));
                    audit.Details.Add(PermissionAuditDetail("PermissionsRemoved", PermissionCodes(removedCodes), null));
                    context.AuditEvents.Add(audit);
                    await context.SaveChangesAsync(cancellationToken);
                    return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityRoleView>>(
                        Result<SecurityRoleView>.Success(MapRole(role)), true);
                }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<SecurityRoleView>.Failure(SecurityErrors.ConcurrencyConflict);
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<SecurityRoleView>.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<SecurityUserSummaryView>> CreateUserAsync(
        CreateSecurityUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = authorizer.Authorize(SecurityPermissions.SecurityManageUsers, true);
        if (authorization.IsFailure) return Result<SecurityUserSummaryView>.Failure(authorization.Error);
        var policy = SecurityPersistenceSupport.ValidatePassword(request.InitialPassword);
        if (policy.IsFailure) return Result<SecurityUserSummaryView>.Failure(policy.Error);
        if (request.OperationId == Guid.Empty
            || !LocalSecurityRules.IsLoginValid(request.Login)
            || !LocalSecurityRules.IsDisplayNameValid(request.DisplayName))
        {
            return Result<SecurityUserSummaryView>.Failure(SecurityErrors.InvalidUser);
        }

        if (request.RoleIds is null || request.RoleIds.Count == 0)
            return Result<SecurityUserSummaryView>.Failure(SecurityErrors.InvalidRoleSelection);

        try
        {
            return await SecurityPersistenceSupport.ExecuteSerializableAsync(
                contextFactory,
                async context =>
                {
                    var roleIds = request.RoleIds.Distinct().ToArray();
                    var roles = await context.Roles.Where(role => roleIds.Contains(role.Id) && role.IsActive).ToArrayAsync(cancellationToken);
                    if (roles.Length != roleIds.Length)
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                            Result<SecurityUserSummaryView>.Failure(SecurityErrors.InvalidRoleSelection), false);
                    var now = clock.UtcNow;
                    var user = new UserAccountEntity
                    {
                        Login = request.Login.Trim(),
                        NormalizedLogin = LocalSecurityRules.NormalizeLogin(request.Login),
                        DisplayName = request.DisplayName.Trim(),
                        IsActive = true,
                        MustChangePassword = true,
                        SecurityStamp = Guid.NewGuid(),
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    };
                    user.PasswordHash = passwordHasher.HashPassword(user, request.InitialPassword);
                    foreach (var role in roles)
                        user.UserRoles.Add(new UserRoleEntity { Role = role, AssignedAtUtc = now, AssignedByUserId = session.User!.UserId });
                    context.UserAccounts.Add(user);
                    await context.SaveChangesAsync(cancellationToken);
                    context.AuditEvents.Add(SecurityPersistenceSupport.AuditEvent(
                        "Administration", "security", "UserAccount", user.Id.ToString(), "user.create",
                        session.User!.UserId, session.User.DisplayName, "Success", now, request.OperationId, request.OperationId));
                    await context.SaveChangesAsync(cancellationToken);
                    return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                        Result<SecurityUserSummaryView>.Success(MapUser(user)), true);
                }, cancellationToken);
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsUniqueViolation(exception))
        {
            return Result<SecurityUserSummaryView>.Failure(SecurityErrors.LoginAlreadyExists);
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<SecurityUserSummaryView>.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<SecurityUserSummaryView>> SetUserActiveAsync(
        SetUserActiveRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = authorizer.Authorize(SecurityPermissions.SecurityManageUsers, true);
        if (authorization.IsFailure) return Result<SecurityUserSummaryView>.Failure(authorization.Error);
        if (!request.IsActive && request.UserId == session.User?.UserId)
            return Result<SecurityUserSummaryView>.Failure(SecurityErrors.PermissionDenied);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var user = await SecurityPersistenceSupport.UserGraph(context).SingleOrDefaultAsync(candidate => candidate.Id == request.UserId, cancellationToken);
            if (user is null) return Result<SecurityUserSummaryView>.Failure(SecurityErrors.UserNotFound);
            context.Entry(user).Property(entity => entity.RowVersion).OriginalValue = request.RowVersion;
            var now = clock.UtcNow;
            user.IsActive = request.IsActive;
            user.DisabledAtUtc = request.IsActive ? null : now;
            user.DisabledByUserId = request.IsActive ? null : session.User!.UserId;
            user.SecurityStamp = Guid.NewGuid();
            user.UpdatedAtUtc = now;
            context.AuditEvents.Add(SecurityPersistenceSupport.AuditEvent(
                "Administration", "security", "UserAccount", user.Id.ToString(), request.IsActive ? "user.activate" : "user.disable",
                session.User!.UserId, session.User.DisplayName, "Success", now, request.OperationId, request.OperationId));
            await context.SaveChangesAsync(cancellationToken);
            return Result<SecurityUserSummaryView>.Success(MapUser(user));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<SecurityUserSummaryView>.Failure(SecurityErrors.ConcurrencyConflict);
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<SecurityUserSummaryView>.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<SecurityUserSummaryView>> SetUserRolesAsync(
        SetUserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = authorizer.Authorize(SecurityPermissions.SecurityManageRoles, true);
        if (authorization.IsFailure) return Result<SecurityUserSummaryView>.Failure(authorization.Error);
        try
        {
            return await SecurityPersistenceSupport.ExecuteSerializableAsync(
                contextFactory,
                async context =>
                {
                    var user = await SecurityPersistenceSupport.UserGraph(context).SingleOrDefaultAsync(candidate => candidate.Id == request.UserId, cancellationToken);
                    if (user is null)
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                            Result<SecurityUserSummaryView>.Failure(SecurityErrors.UserNotFound), false);
                    context.Entry(user).Property(entity => entity.RowVersion).OriginalValue = request.RowVersion;
                    var roleIds = request.RoleIds.Distinct().ToArray();
                    var roles = await context.Roles.Where(role => roleIds.Contains(role.Id) && role.IsActive).ToArrayAsync(cancellationToken);
                    if (roles.Length != roleIds.Length || request.UserId == session.User?.UserId && roles.All(role => role.Code != "OWNER"))
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                            Result<SecurityUserSummaryView>.Failure(SecurityErrors.PermissionDenied), false);
                    context.UserRoles.RemoveRange(user.UserRoles);
                    user.UserRoles.Clear();
                    var now = clock.UtcNow;
                    foreach (var role in roles)
                        user.UserRoles.Add(new UserRoleEntity { Role = role, AssignedAtUtc = now, AssignedByUserId = session.User!.UserId });
                    user.SecurityStamp = Guid.NewGuid();
                    user.UpdatedAtUtc = now;
                    context.AuditEvents.Add(SecurityPersistenceSupport.AuditEvent(
                        "Administration", "security", "UserAccount", user.Id.ToString(), "user.roles.set",
                        session.User!.UserId, session.User.DisplayName, "Success", now, request.OperationId, request.OperationId));
                    await context.SaveChangesAsync(cancellationToken);
                    return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                        Result<SecurityUserSummaryView>.Success(MapUser(user)), true);
                }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<SecurityUserSummaryView>.Failure(SecurityErrors.ConcurrencyConflict);
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<SecurityUserSummaryView>.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<RecoveryCodeBatchView>> GenerateRecoveryCodesAsync(
        long userId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        var authorization = authorizer.Authorize(SecurityPermissions.SecurityManageUsers, true);
        if (authorization.IsFailure) return Result<RecoveryCodeBatchView>.Failure(authorization.Error);
        try
        {
            return await SecurityPersistenceSupport.ExecuteSerializableAsync(
                contextFactory,
                async context =>
                {
                    var user = await context.UserAccounts.Include(entity => entity.RecoveryCodes)
                        .SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);
                    if (user is null)
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<RecoveryCodeBatchView>>(
                            Result<RecoveryCodeBatchView>.Failure(SecurityErrors.UserNotFound), false);
                    var now = clock.UtcNow;
                    foreach (var existing in user.RecoveryCodes.Where(code => code.UsedAtUtc is null)) existing.UsedAtUtc = now;
                    var codes = recoveryCodeGenerator.Generate(RecoveryCodeCount);
                    var expiresAt = now.Add(RecoveryCodeLifetime);
                    SqlAuthenticationService.AddRecoveryCodes(context, user, codes, now, expiresAt);
                    context.AuditEvents.Add(SecurityPersistenceSupport.AuditEvent(
                        "Administration", "security", "UserAccount", user.Id.ToString(), "recovery_codes.generate",
                        session.User!.UserId, session.User.DisplayName, "Success", now, operationId, operationId));
                    await context.SaveChangesAsync(cancellationToken);
                    return new SecurityPersistenceSupport.TransactionOutcome<Result<RecoveryCodeBatchView>>(
                        Result<RecoveryCodeBatchView>.Success(new RecoveryCodeBatchView(user.Id, codes, now, expiresAt)), true);
                }, cancellationToken);
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<RecoveryCodeBatchView>.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    private static SecurityUserSummaryView MapUser(UserAccountEntity user)
    {
        return new SecurityUserSummaryView(
            user.Id,
            user.Login,
            user.DisplayName,
            user.IsActive,
            user.MustChangePassword,
            user.FailedLoginCount,
            user.LockedUntilUtc,
            user.LastLoginAtUtc,
            Array.AsReadOnly(user.UserRoles.Select(join => join.Role.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()),
            user.RowVersion);
    }

    private static SecurityRoleView MapRole(RoleEntity role)
    {
        var permissions = role.RolePermissions.Select(join => join.Permission)
            .OrderBy(permission => permission.Code)
            .Select(permission => new SecurityPermissionView(
                permission.Id, permission.Code, permission.Name, permission.Module, permission.Description, permission.IsSensitive))
            .ToArray();
        return new SecurityRoleView(
            role.Id, role.Code, role.Name, role.Description, role.IsSystemRole, role.IsActive,
            Array.AsReadOnly(permissions), role.RowVersion);
    }

    private static AuditEventDetailEntity PermissionAuditDetail(string fieldName, string? oldValue, string? newValue) => new()
    {
        FieldName = fieldName,
        OldValueRedacted = oldValue,
        NewValueRedacted = newValue
    };

    private static string? PermissionCodes(IEnumerable<string> codes)
    {
        var value = string.Join(",", codes);
        return value.Length switch
        {
            0 => null,
            <= 512 => value,
            _ => value[..509] + "..."
        };
    }
}
