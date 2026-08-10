using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Application.Security.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Security;

internal sealed class SqlAuditStore : IAuditReader, IAuditWriter
{
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;
    private readonly IPermissionAuthorizer authorizer;

    public SqlAuditStore(IDbContextFactory<PharmacyDbContext> contextFactory, IPermissionAuthorizer authorizer)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
    }

    public async Task<Result<IReadOnlyList<AuditEventView>>> SearchAsync(
        AuditSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var authorization = authorizer.Authorize(SecurityPermissions.AuditView);
        if (authorization.IsFailure) return Result<IReadOnlyList<AuditEventView>>.Failure(authorization.Error);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var rows = context.AuditEvents.AsNoTracking();
            if (query.FromUtc is not null) rows = rows.Where(audit => audit.OccurredAtUtc >= query.FromUtc.Value);
            if (query.ToUtc is not null) rows = rows.Where(audit => audit.OccurredAtUtc <= query.ToUtc.Value);
            if (!string.IsNullOrWhiteSpace(query.Module)) rows = rows.Where(audit => audit.Module == query.Module);
            if (!string.IsNullOrWhiteSpace(query.Action)) rows = rows.Where(audit => audit.Action.Contains(query.Action));
            if (query.ActorUserId is not null) rows = rows.Where(audit => audit.ActorUserId == query.ActorUserId);
            var values = await rows.OrderByDescending(audit => audit.OccurredAtUtc).ThenByDescending(audit => audit.Id)
                .Take(Math.Clamp(query.Limit, 1, 500))
                .Select(audit => new AuditEventView(
                    audit.Id, audit.EventType, audit.Module, audit.EntityType, audit.EntityId, audit.Action,
                    audit.ActorUserId, audit.ActorDisplayName, audit.Result, audit.OccurredAtUtc,
                    audit.OperationId, audit.CorrelationId, audit.ReasonCode))
                .ToArrayAsync(cancellationToken);
            return Result<IReadOnlyList<AuditEventView>>.Success(Array.AsReadOnly(values));
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<IReadOnlyList<AuditEventView>>.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result> AppendAsync(AppendAuditEventRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            context.AuditEvents.Add(SecurityPersistenceSupport.AuditEvent(
                request.EventType, request.Module, request.EntityType, request.EntityId, request.Action,
                request.ActorUserId, request.ActorDisplayName, request.Result, request.OccurredAtUtc,
                request.OperationId, request.CorrelationId, request.ReasonCode));
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }
}
