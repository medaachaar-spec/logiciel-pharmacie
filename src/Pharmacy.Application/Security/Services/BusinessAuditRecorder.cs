using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;

namespace Pharmacy.Application.Security.Services;

public sealed class BusinessAuditRecorder : IBusinessAuditRecorder
{
    private readonly IAuditWriter writer;
    private readonly ICurrentSecuritySession session;
    private readonly ISecurityClock clock;

    public BusinessAuditRecorder(IAuditWriter writer, ICurrentSecuritySession session, ISecurityClock clock)
    {
        this.writer = writer;
        this.session = session;
        this.clock = clock;
    }

    public async Task RecordSuccessAsync(
        string module,
        string entityType,
        string? entityId,
        string action,
        Guid? operationId,
        CancellationToken cancellationToken = default)
    {
        var actor = session.User;
        await writer.AppendAsync(new AppendAuditEventRequest(
            "Business", module, entityType, entityId, action,
            actor?.UserId, actor?.DisplayName, "Success", clock.UtcNow,
            operationId, Guid.NewGuid()), cancellationToken);
    }

    public async Task RecordFailureAsync(
        string module,
        string entityType,
        string? entityId,
        string action,
        Guid? operationId,
        string reasonCode,
        CancellationToken cancellationToken = default)
    {
        var actor = session.User;
        await writer.AppendAsync(new AppendAuditEventRequest(
            "Business", module, entityType, entityId, action,
            actor?.UserId, actor?.DisplayName, "Failure", clock.UtcNow,
            operationId, Guid.NewGuid(), reasonCode), cancellationToken);
    }
}
