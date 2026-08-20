using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Branding;
using Pharmacy.Application.Branding.Contracts;
using Pharmacy.Application.Branding.Errors;
using Pharmacy.Application.Branding.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Audit;
using Pharmacy.Infrastructure.Persistence.Entities.Configuration;
using Pharmacy.Infrastructure.Security;

namespace Pharmacy.Infrastructure.Branding;

public sealed class SqlPharmacyBrandingService : IPharmacyBrandingService
{
    private const long BrandingId = 1;
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;
    private readonly IPermissionAuthorizer authorizer;
    private readonly ICurrentSecuritySession session;
    private readonly ISecurityClock clock;

    public SqlPharmacyBrandingService(
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

    public async Task<Result<PharmacyBrandingView>> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var entity = await context.PharmacyBranding.AsNoTracking()
                .SingleAsync(item => item.Id == BrandingId, cancellationToken);
            return Result<PharmacyBrandingView>.Success(Map(entity));
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<PharmacyBrandingView>.Failure(PharmacyBrandingErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<PharmacyBrandingView>> UpdateAsync(
        UpdatePharmacyBrandingRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = authorizer.Authorize(SecurityPermissions.SecurityManageUsers);
        if (authorization.IsFailure)
            return Result<PharmacyBrandingView>.Failure(authorization.Error);

        var validation = PharmacyBrandingValidator.Validate(request);
        if (validation.IsFailure)
            return Result<PharmacyBrandingView>.Failure(validation.Error);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var entity = await context.PharmacyBranding.SingleAsync(
                item => item.Id == BrandingId,
                cancellationToken);
            context.Entry(entity).Property(item => item.RowVersion).OriginalValue = request.RowVersion.ToArray();

            var pharmacyName = request.PharmacyName.Trim();
            var primaryColor = request.PrimaryColor.Trim().ToUpperInvariant();
            var auditEvent = SecurityPersistenceSupport.AuditEvent(
                "Administration",
                "branding",
                "PharmacyBranding",
                BrandingId.ToString(),
                "branding.update",
                session.User!.UserId,
                session.User.DisplayName,
                "Success",
                clock.UtcNow,
                request.OperationId,
                request.OperationId);

            AddTextDetailIfChanged(auditEvent, "PharmacyName", entity.PharmacyName, pharmacyName);
            AddTextDetailIfChanged(auditEvent, "PrimaryColor", entity.PrimaryColor, primaryColor);
            if (request.LogoContent is not null)
            {
                AddTextDetailIfChanged(
                    auditEvent,
                    "Logo",
                    LogoMetadata(entity.LogoMimeType, entity.LogoContent?.Length),
                    LogoMetadata(request.LogoMimeType, request.LogoContent.Length));
                entity.LogoContent = request.LogoContent.ToArray();
                entity.LogoMimeType = request.LogoMimeType!.Trim().ToLowerInvariant();
                entity.LogoFileName = Path.GetFileName(request.LogoFileName!.Trim());
            }

            entity.PharmacyName = pharmacyName;
            entity.PrimaryColor = primaryColor;
            entity.UpdatedAtUtc = clock.UtcNow;
            context.AuditEvents.Add(auditEvent);
            await context.SaveChangesAsync(cancellationToken);
            return Result<PharmacyBrandingView>.Success(Map(entity));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PharmacyBrandingView>.Failure(PharmacyBrandingErrors.ConcurrencyConflict);
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<PharmacyBrandingView>.Failure(PharmacyBrandingErrors.DatabaseUnavailable);
        }
    }

    private static PharmacyBrandingView Map(PharmacyBrandingEntity entity) => new(
        entity.PharmacyName,
        entity.PrimaryColor,
        entity.LogoContent?.ToArray(),
        entity.LogoMimeType,
        entity.LogoFileName,
        entity.UpdatedAtUtc,
        entity.RowVersion.ToArray());

    private static void AddTextDetailIfChanged(
        AuditEventEntity auditEvent,
        string fieldName,
        string? previousValue,
        string? nextValue)
    {
        if (string.Equals(previousValue, nextValue, StringComparison.Ordinal)) return;
        auditEvent.Details.Add(new AuditEventDetailEntity
        {
            FieldName = fieldName,
            OldValueRedacted = previousValue,
            NewValueRedacted = nextValue
        });
    }

    private static string? LogoMetadata(string? mimeType, int? size) =>
        size.HasValue ? $"{mimeType ?? "unknown"};{size.Value} bytes" : null;
}
