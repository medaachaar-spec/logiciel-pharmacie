using System.Diagnostics;
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
        var occurredAtUtc = clock.UtcNow;
        var result = await writer.AppendAsync(new AppendAuditEventRequest(
            "Business", module, entityType, entityId, action,
            actor?.UserId, actor?.DisplayName, "Success", occurredAtUtc,
            operationId, Guid.NewGuid()), cancellationToken);

        if (result.IsFailure)
        {
            Trace.TraceWarning(
                "Business audit write failed non-fatally. OperationType={0}; OperationId={1}; OccurredAtUtc={2:O}; FailureClass={3}.",
                action,
                operationId?.ToString("D") ?? "none",
                occurredAtUtc,
                ClassifyFailure(result.Error.Code));
        }
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

    private static string ClassifyFailure(string errorCode) => errorCode switch
    {
        "security.database.unavailable" => "DatabaseUnavailable",
        "security.authorization.denied" => "PermissionDenied",
        _ => "WriterRejected"
    };
}
