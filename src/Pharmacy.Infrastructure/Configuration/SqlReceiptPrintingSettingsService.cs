using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Configuration;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Audit;
using Pharmacy.Infrastructure.Persistence.Entities.Configuration;
using Pharmacy.Infrastructure.Security;

namespace Pharmacy.Infrastructure.Configuration;

public sealed class SqlReceiptPrintingSettingsService : IReceiptPrintingSettingsService
{
    private const long SettingsId = 1;
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;
    private readonly IPermissionAuthorizer authorizer;
    private readonly ICurrentSecuritySession session;
    private readonly ISecurityClock clock;

    public SqlReceiptPrintingSettingsService(
        IDbContextFactory<PharmacyDbContext> contextFactory,
        IPermissionAuthorizer authorizer,
        ICurrentSecuritySession session,
        ISecurityClock clock)
    {
        this.contextFactory = contextFactory;
        this.authorizer = authorizer;
        this.session = session;
        this.clock = clock;
    }

    public async Task<Result<ReceiptPrintingSettingsView>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var entity = await context.ApplicationSettings.AsNoTracking()
                .SingleAsync(item => item.Id == SettingsId, cancellationToken);
            return Result<ReceiptPrintingSettingsView>.Success(Map(entity));
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<ReceiptPrintingSettingsView>.Failure(
                ReceiptPrintingSettingsErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<ReceiptPrintingSettingsView>> UpdateAsync(
        UpdateReceiptPrintingSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = authorizer.Authorize(SecurityPermissions.DevicesConfigure);
        if (authorization.IsFailure)
        {
            return Result<ReceiptPrintingSettingsView>.Failure(authorization.Error);
        }

        if (request.OperationId == Guid.Empty || request.RowVersion.Length == 0)
        {
            return Result<ReceiptPrintingSettingsView>.Failure(
                ReceiptPrintingSettingsErrors.ConcurrencyConflict);
        }

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var entity = await context.ApplicationSettings.SingleAsync(
                item => item.Id == SettingsId,
                cancellationToken);
            context.Entry(entity).Property(item => item.RowVersion).OriginalValue =
                request.RowVersion.ToArray();

            var auditEvent = SecurityPersistenceSupport.AuditEvent(
                "Configuration",
                "application-settings",
                "ApplicationSettings",
                SettingsId.ToString(),
                "receipt-printing.update",
                session.User!.UserId,
                session.User.DisplayName,
                "Success",
                clock.UtcNow,
                request.OperationId,
                request.OperationId);
            auditEvent.Details.Add(new AuditEventDetailEntity
            {
                FieldName = "ReceiptPrintingEnabled",
                OldValueRedacted = entity.ReceiptPrintingEnabled.ToString(),
                NewValueRedacted = request.ReceiptPrintingEnabled.ToString()
            });

            entity.ReceiptPrintingEnabled = request.ReceiptPrintingEnabled;
            entity.UpdatedAtUtc = clock.UtcNow;
            context.AuditEvents.Add(auditEvent);
            await context.SaveChangesAsync(cancellationToken);
            return Result<ReceiptPrintingSettingsView>.Success(Map(entity));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ReceiptPrintingSettingsView>.Failure(
                ReceiptPrintingSettingsErrors.ConcurrencyConflict);
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<ReceiptPrintingSettingsView>.Failure(
                ReceiptPrintingSettingsErrors.DatabaseUnavailable);
        }
    }

    private static ReceiptPrintingSettingsView Map(ApplicationSettingsEntity entity) => new(
        entity.ReceiptPrintingEnabled,
        entity.UpdatedAtUtc,
        entity.RowVersion.ToArray());
}
