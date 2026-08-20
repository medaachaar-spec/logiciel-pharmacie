using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class SaleReturnApplicationTests
{
    [Fact]
    public async Task RET002_17_BackendRefusesReturnPermissionBeforeAnyMutation()
    {
        var store = new RecordingStore();
        var handler = new ReturnSaleHandler(
            store,
            new PermissionSetAuthorizer(SecurityPermissions.SalesRefund),
            new RecordingAudit());

        var result = await handler.HandleAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(SecurityErrors.PermissionDenied.Code, result.Error.Code);
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task RET002_18_BackendChecksRefundPermissionSeparatelyBeforeAnyMutation()
    {
        var store = new RecordingStore();
        var handler = new ReturnSaleHandler(
            store,
            new PermissionSetAuthorizer(SecurityPermissions.SalesReturn),
            new RecordingAudit());

        var result = await handler.HandleAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(SecurityErrors.PermissionDenied.Code, result.Error.Code);
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task RET002_19_SuccessfulReturnRecordsTraceableBusinessAuditWithoutSecrets()
    {
        var store = new RecordingStore();
        var audit = new RecordingAudit();
        var request = Request();
        var handler = new ReturnSaleHandler(
            store,
            new PermissionSetAuthorizer(
                SecurityPermissions.SalesReturn,
                SecurityPermissions.SalesRefund),
            audit);

        var result = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, store.CallCount);
        var recorded = Assert.Single(audit.Events);
        Assert.Equal(("sales", "SaleReturn", "sale.return_refund", request.OperationId), recorded);
        Assert.DoesNotContain("password", string.Join('|', recorded), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("card", string.Join('|', recorded), StringComparison.OrdinalIgnoreCase);
    }

    private static ReturnSaleRequest Request() => new(
        Guid.NewGuid(),
        42,
        new[] { new ReturnSaleLineRequest(7, 1, ReturnStockDestination.RestockApproved) },
        ReturnRefundMethod.Cash);

    private sealed class PermissionSetAuthorizer(params string[] permissions) : IPermissionAuthorizer
    {
        private readonly HashSet<string> granted = permissions.ToHashSet(StringComparer.Ordinal);

        public Result Authorize(string permission, bool requireRecentReauthentication = false) =>
            granted.Contains(permission)
                ? Result.Success()
                : Result.Failure(SecurityErrors.PermissionDenied);
    }

    private sealed class RecordingStore : ISaleReturnStore
    {
        public int CallCount { get; private set; }

        public Task<Result<SaleReturnView>> ReturnAsync(
            ReturnSaleRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var view = new SaleReturnView(
                11,
                "RET-TEST-001",
                request.SaleId,
                Money.Create(25m).Value,
                request.RefundMethod,
                new DateTime(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc),
                Array.Empty<SaleReturnLineView>());
            return Task.FromResult(Result<SaleReturnView>.Success(view));
        }
    }

    private sealed class RecordingAudit : IBusinessAuditRecorder
    {
        public List<(string Module, string EntityType, string Action, Guid? OperationId)> Events { get; } = new();

        public Task RecordSuccessAsync(
            string module,
            string entityType,
            string? entityId,
            string action,
            Guid? operationId,
            CancellationToken cancellationToken = default)
        {
            Events.Add((module, entityType, action, operationId));
            return Task.CompletedTask;
        }
    }
}
