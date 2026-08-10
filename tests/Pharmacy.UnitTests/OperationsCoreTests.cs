using Pharmacy.Application.Operations.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Domain.Common;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class OperationsCoreTests
{
    private static readonly DateTime Now = new(2026, 8, 10, 19, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void HidScannerCompletesOnEnterAndSuppressesImmediateDoubleScan()
    {
        var scanner = new HidScannerBuffer(TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(500));
        var now = new DateTime(2026, 8, 10, 19, 0, 0, DateTimeKind.Utc);

        Assert.Null(scanner.Push('6', now));
        Assert.Null(scanner.Push('1', now.AddMilliseconds(20)));
        Assert.Equal("61", scanner.Push('\n', now.AddMilliseconds(40)));
        scanner.Push('6', now.AddMilliseconds(100));
        scanner.Push('1', now.AddMilliseconds(120));
        Assert.Null(scanner.Push('\n', now.AddMilliseconds(140)));
        scanner.Push('6', now.AddMilliseconds(700));
        scanner.Push('2', now.AddMilliseconds(900));
        Assert.Equal("2", scanner.Push('\n', now.AddMilliseconds(920)));
    }

    [Fact]
    public void OperationsPermissionsAreExplicitAndSensitiveActionsAreGated()
    {
        Assert.Contains(SecurityPermissions.BackupView, SecurityPermissions.All);
        Assert.Contains(SecurityPermissions.BackupCreate, SecurityPermissions.All);
        Assert.Contains(SecurityPermissions.BackupRestore, SecurityPermissions.All);
        Assert.Contains(SecurityPermissions.ImportView, SecurityPermissions.All);
        Assert.Contains(SecurityPermissions.ImportExecute, SecurityPermissions.All);
        Assert.Contains(SecurityPermissions.DevicesConfigure, SecurityPermissions.All);
        Assert.DoesNotContain(SecurityPermissions.BackupView, SecurityPermissions.Sensitive);
        Assert.DoesNotContain(SecurityPermissions.BackupCreate, SecurityPermissions.Sensitive);
        Assert.Contains(SecurityPermissions.BackupRestore, SecurityPermissions.Sensitive);
        Assert.Contains(SecurityPermissions.ImportExecute, SecurityPermissions.Sensitive);
        Assert.Contains(SecurityPermissions.DevicesConfigure, SecurityPermissions.Sensitive);
    }

    [Fact]
    public async Task SensitiveFailureAuditStoresOnlyTheReasonCode()
    {
        var session = new CurrentSecuritySession();
        session.Establish(new UserSessionView(
            17, "operator", "Opérateur", ["OWNER"],
            new HashSet<string>([SecurityPermissions.BackupRestore]), Now, Now), Now);
        var writer = new CapturingAuditWriter();
        var recorder = new BusinessAuditRecorder(writer, session, new FixedClock());

        await recorder.RecordFailureAsync(
            "backup", "BackupOperation", null, "backup.restore.test", Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "security.authorization.reauthentication_required", TestContext.Current.CancellationToken);

        Assert.NotNull(writer.Request);
        Assert.Equal("Failure", writer.Request.Result);
        Assert.Equal("security.authorization.reauthentication_required", writer.Request.ReasonCode);
        Assert.Null(writer.Request.EntityId);
    }

    private sealed class FixedClock : ISecurityClock { public DateTime UtcNow => Now; }
    private sealed class CapturingAuditWriter : IAuditWriter
    {
        public AppendAuditEventRequest? Request { get; private set; }
        public Task<Result> AppendAsync(AppendAuditEventRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(Result.Success());
        }
    }
}
