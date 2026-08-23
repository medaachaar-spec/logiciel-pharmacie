using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
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
        var actorIsOwner = CurrentActorIsOwner();
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
                    if (!actorIsOwner && ContainsOwnerExclusivePermission(permissions.Select(item => item.Code)))
                    {
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityRoleView>>(
                            Result<SecurityRoleView>.Failure(SecurityErrors.PermissionDenied), false);
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
        var actorIsOwner = CurrentActorIsOwner();
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
                    if (role.IsSystemRole || string.Equals(role.Code, SecurityRolePresets.OwnerCode, StringComparison.Ordinal))
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
                    if (!actorIsOwner && ContainsOwnerExclusivePermission(permissions.Select(item => item.Code)))
                    {
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityRoleView>>(
                            Result<SecurityRoleView>.Failure(SecurityErrors.PermissionDenied), false);
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
        var actorIsOwner = CurrentActorIsOwner();
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
                    var roles = await context.Roles
                        .Include(role => role.RolePermissions)
                        .ThenInclude(join => join.Permission)
                        .Where(role => roleIds.Contains(role.Id) && role.IsActive)
                        .ToArrayAsync(cancellationToken);
                    if (roles.Length != roleIds.Length)
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                            Result<SecurityUserSummaryView>.Failure(SecurityErrors.InvalidRoleSelection), false);
                    if (roles.Any(role => role.Code == SecurityRolePresets.OwnerCode))
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                            Result<SecurityUserSummaryView>.Failure(SecurityErrors.SystemRoleProtected), false);
                    if (!actorIsOwner && roles.Any(IsOwnerEquivalentRole))
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                            Result<SecurityUserSummaryView>.Failure(SecurityErrors.PermissionDenied), false);
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
                    var audit = SecurityPersistenceSupport.AuditEvent(
                        "Administration", "security", "UserAccount", user.Id.ToString(), "user.create",
                        session.User!.UserId, session.User.DisplayName, "Success", now, request.OperationId, request.OperationId);
                    audit.Details.Add(PermissionAuditDetail(
                        "RolesAssigned", null, PermissionCodes(roles.Select(role => role.Code).Order(StringComparer.Ordinal))));
                    context.AuditEvents.Add(audit);
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
        var actorIsOwner = CurrentActorIsOwner();
        if (!request.IsActive && request.UserId == session.User?.UserId)
            return Result<SecurityUserSummaryView>.Failure(SecurityErrors.PermissionDenied);
        if (request.OperationId == Guid.Empty || request.UserId <= 0 || request.RowVersion is not { Length: 8 })
            return Result<SecurityUserSummaryView>.Failure(SecurityErrors.InvalidUser);
        try
        {
            return await SecurityPersistenceSupport.ExecuteSerializableAsync(
                contextFactory,
                async context =>
                {
                    var user = await SecurityPersistenceSupport.UserGraph(context)
                        .SingleOrDefaultAsync(candidate => candidate.Id == request.UserId, cancellationToken);
                    if (user is null)
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                            Result<SecurityUserSummaryView>.Failure(SecurityErrors.UserNotFound), false);
                    context.Entry(user).Property(entity => entity.RowVersion).OriginalValue = request.RowVersion;
                    if (request.IsActive && !actorIsOwner && IsOwnerEquivalentUser(user))
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                            Result<SecurityUserSummaryView>.Failure(SecurityErrors.PermissionDenied), false);
                    if (!request.IsActive && user.UserRoles.Any(item => item.Role.Code == SecurityRolePresets.OwnerCode))
                    {
                        var activeOwnerCount = await context.UserRoles.CountAsync(item =>
                            item.Role.Code == SecurityRolePresets.OwnerCode && item.UserAccount.IsActive, cancellationToken);
                        if (activeOwnerCount <= 1)
                            return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                                Result<SecurityUserSummaryView>.Failure(SecurityErrors.LastOwnerProtected), false);
                    }
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

    public async Task<Result<SecurityUserSummaryView>> SetUserRolesAsync(
        SetUserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = authorizer.Authorize(SecurityPermissions.SecurityManageRoles, true);
        if (authorization.IsFailure) return Result<SecurityUserSummaryView>.Failure(authorization.Error);
        var actorIsOwner = CurrentActorIsOwner();
        if (request.OperationId == Guid.Empty || request.UserId <= 0
            || request.RowVersion is not { Length: 8 } || request.RoleIds is null || request.RoleIds.Count == 0)
            return Result<SecurityUserSummaryView>.Failure(SecurityErrors.InvalidRoleSelection);
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
                    var roles = await context.Roles
                        .Include(role => role.RolePermissions)
                        .ThenInclude(join => join.Permission)
                        .Where(role => roleIds.Contains(role.Id) && role.IsActive)
                        .ToArrayAsync(cancellationToken);
                    if (roles.Length != roleIds.Length || request.UserId == session.User?.UserId && roles.All(role => role.Code != SecurityRolePresets.OwnerCode))
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                            Result<SecurityUserSummaryView>.Failure(SecurityErrors.PermissionDenied), false);
                    var previousRoleCodes = user.UserRoles.Select(item => item.Role.Code).ToHashSet(StringComparer.Ordinal);
                    var ownerAssignmentChanged = previousRoleCodes.Contains(SecurityRolePresets.OwnerCode)
                        != roles.Any(role => role.Code == SecurityRolePresets.OwnerCode);
                    if (ownerAssignmentChanged)
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                            Result<SecurityUserSummaryView>.Failure(SecurityErrors.SystemRoleProtected), false);
                    if (!actorIsOwner && roles.Any(IsOwnerEquivalentRole))
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                            Result<SecurityUserSummaryView>.Failure(SecurityErrors.PermissionDenied), false);
                    if (previousRoleCodes.Contains(SecurityRolePresets.OwnerCode)
                        && roles.All(role => role.Code != SecurityRolePresets.OwnerCode))
                    {
                        var activeOwnerCount = await context.UserRoles.CountAsync(item =>
                            item.Role.Code == SecurityRolePresets.OwnerCode && item.UserAccount.IsActive, cancellationToken);
                        if (activeOwnerCount <= 1)
                            return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                                Result<SecurityUserSummaryView>.Failure(SecurityErrors.LastOwnerProtected), false);
                    }
                    context.UserRoles.RemoveRange(user.UserRoles);
                    user.UserRoles.Clear();
                    var now = clock.UtcNow;
                    foreach (var role in roles)
                        user.UserRoles.Add(new UserRoleEntity { Role = role, AssignedAtUtc = now, AssignedByUserId = session.User!.UserId });
                    user.SecurityStamp = Guid.NewGuid();
                    user.UpdatedAtUtc = now;
                    var audit = SecurityPersistenceSupport.AuditEvent(
                        "Administration", "security", "UserAccount", user.Id.ToString(), "user.roles.set",
                        session.User!.UserId, session.User.DisplayName, "Success", now, request.OperationId, request.OperationId);
                    audit.Details.Add(PermissionAuditDetail("RolesBefore", PermissionCodes(previousRoleCodes.Order(StringComparer.Ordinal)), null));
                    audit.Details.Add(PermissionAuditDetail("RolesAfter", null, PermissionCodes(roles.Select(role => role.Code).Order(StringComparer.Ordinal))));
                    context.AuditEvents.Add(audit);
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

    public async Task<Result<SecurityUserSummaryView>> SetUserPermissionOverridesAsync(
        SetUserPermissionOverridesRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = authorizer.Authorize(SecurityPermissions.SecurityManageRoles, true);
        if (authorization.IsFailure) return Result<SecurityUserSummaryView>.Failure(authorization.Error);
        var currentUser = session.User!;
        var actorIsOwner = CurrentActorIsOwner();
        if (request.UserId == currentUser.UserId
            && !currentUser.Roles.Contains(SecurityRolePresets.OwnerCode, StringComparer.Ordinal))
        {
            return Result<SecurityUserSummaryView>.Failure(SecurityErrors.PermissionDenied);
        }
        if (request.OperationId == Guid.Empty || request.UserId <= 0
            || request.RowVersion is not { Length: 8 } || request.Overrides is null
            || request.Overrides.Any(item => item.PermissionId <= 0 || !Enum.IsDefined(item.Effect))
            || request.Overrides.Select(item => item.PermissionId).Distinct().Count() != request.Overrides.Count)
        {
            return Result<SecurityUserSummaryView>.Failure(SecurityErrors.InvalidPermissionSelection);
        }

        try
        {
            return await SecurityPersistenceSupport.ExecuteSerializableAsync(
                contextFactory,
                async context =>
                {
                    var user = await SecurityPersistenceSupport.UserGraph(context)
                        .SingleOrDefaultAsync(candidate => candidate.Id == request.UserId, cancellationToken);
                    if (user is null)
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                            Result<SecurityUserSummaryView>.Failure(SecurityErrors.UserNotFound), false);
                    context.Entry(user).Property(entity => entity.RowVersion).OriginalValue = request.RowVersion;

                    var permissionIds = request.Overrides.Select(item => item.PermissionId).ToArray();
                    var permissions = await context.Permissions.Where(item => permissionIds.Contains(item.Id))
                        .ToDictionaryAsync(item => item.Id, cancellationToken);
                    if (permissions.Count != permissionIds.Length)
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                            Result<SecurityUserSummaryView>.Failure(SecurityErrors.InvalidPermissionSelection), false);

                    if (!actorIsOwner)
                    {
                        var roleGrants = user.UserRoles.Where(join => join.Role.IsActive)
                            .SelectMany(join => join.Role.RolePermissions)
                            .Select(join => join.Permission.Code);
                        var previousOverrides = user.PermissionOverrides.Select(item =>
                            new KeyValuePair<string, SecurityPermissionOverrideEffect>(
                                item.Permission.Code,
                                Enum.Parse<SecurityPermissionOverrideEffect>(item.Effect, ignoreCase: false)));
                        var requestedOverrides = request.Overrides.Select(item =>
                            new KeyValuePair<string, SecurityPermissionOverrideEffect>(
                                permissions[item.PermissionId].Code,
                                item.Effect));
                        var previousEffective = EffectivePermissionResolver.Resolve(roleGrants, previousOverrides);
                        var requestedEffective = EffectivePermissionResolver.Resolve(roleGrants, requestedOverrides);
                        var explicitlyAllowsOwnerExclusive = request.Overrides.Any(item =>
                            item.Effect == SecurityPermissionOverrideEffect.Allow
                            && SecurityPermissions.OwnerExclusive.Contains(permissions[item.PermissionId].Code));
                        var exposesOwnerExclusive = SecurityPermissions.OwnerExclusive.Any(code =>
                            !previousEffective.Contains(code) && requestedEffective.Contains(code));
                        if (explicitlyAllowsOwnerExclusive || exposesOwnerExclusive)
                        {
                            return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                                Result<SecurityUserSummaryView>.Failure(SecurityErrors.PermissionDenied), false);
                        }
                    }

                    var isOwner = user.UserRoles.Any(item => item.Role.Code == SecurityRolePresets.OwnerCode);
                    var protectedPermissionIds = permissions.Values
                        .Where(item => item.Code is SecurityPermissions.SecurityManageUsers or SecurityPermissions.SecurityManageRoles)
                        .Select(item => item.Id).ToHashSet();
                    if (isOwner && request.Overrides.Any(item =>
                        item.Effect == SecurityPermissionOverrideEffect.Deny && protectedPermissionIds.Contains(item.PermissionId)))
                    {
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<SecurityUserSummaryView>>(
                            Result<SecurityUserSummaryView>.Failure(SecurityErrors.LastOwnerProtected), false);
                    }

                    var previous = user.PermissionOverrides
                        .Select(item => $"{item.Effect}:{item.Permission.Code}")
                        .Order(StringComparer.Ordinal).ToArray();
                    var now = clock.UtcNow;
                    var requested = request.Overrides.ToDictionary(item => item.PermissionId);
                    var removed = user.PermissionOverrides.Where(item => !requested.ContainsKey(item.PermissionId)).ToArray();
                    context.UserPermissionOverrides.RemoveRange(removed);
                    foreach (var item in removed) user.PermissionOverrides.Remove(item);
                    foreach (var selection in request.Overrides.OrderBy(item => permissions[item.PermissionId].Code, StringComparer.Ordinal))
                    {
                        var existing = user.PermissionOverrides.SingleOrDefault(item => item.PermissionId == selection.PermissionId);
                        if (existing is not null)
                        {
                            existing.Effect = selection.Effect.ToString();
                            existing.UpdatedAtUtc = now;
                            existing.UpdatedByUserId = session.User!.UserId;
                            continue;
                        }
                        user.PermissionOverrides.Add(new UserPermissionOverrideEntity
                        {
                            Permission = permissions[selection.PermissionId],
                            Effect = selection.Effect.ToString(),
                            UpdatedAtUtc = now,
                            UpdatedByUserId = session.User!.UserId
                        });
                    }
                    user.SecurityStamp = Guid.NewGuid();
                    user.UpdatedAtUtc = now;
                    var current = user.PermissionOverrides
                        .Select(item => $"{item.Effect}:{item.Permission.Code}")
                        .Order(StringComparer.Ordinal).ToArray();
                    var action = current.Length == 0 ? "user.permission_overrides.reset" : "user.permission_overrides.set";
                    var audit = SecurityPersistenceSupport.AuditEvent(
                        "Administration", "security", "UserAccount", user.Id.ToString(), action,
                        session.User!.UserId, session.User.DisplayName, "Success", now, request.OperationId, request.OperationId);
                    audit.Details.Add(PermissionAuditDetail("OverridesBefore", PermissionCodes(previous), null));
                    audit.Details.Add(PermissionAuditDetail("OverridesAfter", null, PermissionCodes(current)));
                    context.AuditEvents.Add(audit);
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
        var actorIsOwner = CurrentActorIsOwner();
        try
        {
            return await SecurityPersistenceSupport.ExecuteSerializableAsync(
                contextFactory,
                async context =>
                {
                    var user = await SecurityPersistenceSupport.UserGraph(context)
                        .Include(entity => entity.RecoveryCodes)
                        .SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);
                    if (user is null)
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<RecoveryCodeBatchView>>(
                            Result<RecoveryCodeBatchView>.Failure(SecurityErrors.UserNotFound), false);
                    if (!actorIsOwner && IsOwnerEquivalentUser(user))
                        return new SecurityPersistenceSupport.TransactionOutcome<Result<RecoveryCodeBatchView>>(
                            Result<RecoveryCodeBatchView>.Failure(SecurityErrors.PermissionDenied), false);
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
        var roleGrants = user.UserRoles.Where(join => join.Role.IsActive)
            .SelectMany(join => join.Role.RolePermissions)
            .Select(join => join.Permission.Code);
        var overridePairs = user.PermissionOverrides.Select(item => new KeyValuePair<string, SecurityPermissionOverrideEffect>(
            item.Permission.Code,
            Enum.Parse<SecurityPermissionOverrideEffect>(item.Effect, ignoreCase: false)));
        var effectivePermissions = Pharmacy.Application.Security.Services.EffectivePermissionResolver.Resolve(roleGrants, overridePairs);
        var overrides = user.PermissionOverrides.OrderBy(item => item.Permission.Code, StringComparer.Ordinal)
            .Select(item => new SecurityUserPermissionOverrideView(
                item.PermissionId,
                item.Permission.Code,
                Enum.Parse<SecurityPermissionOverrideEffect>(item.Effect, ignoreCase: false),
                item.UpdatedAtUtc,
                item.RowVersion))
            .ToArray();
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
            effectivePermissions,
            Array.AsReadOnly(overrides),
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

    private bool CurrentActorIsOwner() =>
        session.User?.Roles.Contains(SecurityRolePresets.OwnerCode, StringComparer.Ordinal) == true;

    private static bool IsOwnerEquivalentRole(RoleEntity role) =>
        role.Code == SecurityRolePresets.OwnerCode
        || ContainsOwnerExclusivePermission(role.RolePermissions.Select(join => join.Permission.Code));

    private static bool IsOwnerEquivalentUser(UserAccountEntity user) =>
        user.UserRoles.Any(join => IsOwnerEquivalentRole(join.Role))
        || user.PermissionOverrides.Any(item =>
            item.Effect == nameof(SecurityPermissionOverrideEffect.Allow)
            && SecurityPermissions.OwnerExclusive.Contains(item.Permission.Code));

    private static bool ContainsOwnerExclusivePermission(IEnumerable<string> permissionCodes) =>
        permissionCodes.Any(SecurityPermissions.OwnerExclusive.Contains);

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
