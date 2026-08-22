using System.Data;
using System.Diagnostics;
using System.Text;
using Microsoft.Data.SqlClient;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Application.Sales.Finalization.Models;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Security;
using Xunit;

namespace Pharmacy.IntegrationTests;

/// <summary>
/// ZCODE-04A / ZC-017: post-commit audit failure on sale finalization.
/// The business transaction (sale, payment, allocations, stock, cash/credit)
/// commits inside FinalizeAsync; the audit write happens AFTER the commit.
/// Fault injection is done at the IAuditWriter seam with test-local adapters:
/// one returning Result.Failure (exactly what SqlAuditStore.AppendAsync returns
/// when the database is unavailable) and one throwing a non-SQL exception
/// (which SqlAuditStore does not catch and would propagate).
/// The production boundary must preserve committed-success semantics while
/// emitting a sanitized non-fatal diagnostic for either failure shape.
/// </summary>
[Collection("Sprint 09 persistence serial")]
public sealed class Zc04PostCommitAuditFailureTests
{
    private const string OwnerPassword = "Zc04-Owner-Password-2026!";

    [Fact]
    public async Task ZC04A08A_AuditWriterReturningDatabaseUnavailableAfterCommitMustNotBeSilentlySwallowed()
    {
        var token = TestContext.Current.CancellationToken;
        var server = Zc04.BuildServerConnection();
        var databaseName = Zc04.NewDatabaseName();
        var databaseConnection = Zc04.BuildDatabaseConnection(server.ConnectionString, databaseName);
        await Zc04.CreateDatabaseAsync(server.ConnectionString, databaseName);

        try
        {
            using var diagnostics = new TraceCaptureScope();
            var (factory, clock, owner, seeded) = await PrepareSaleEnvironmentAsync(databaseConnection, "A08A", token);
            var failingWriter = new FaultingAuditWriter(
                mode: FaultMode.ReturnDatabaseUnavailable,
                delegateToRealStore: false,
                realStore: null,
                sessionFactory: () => owner.Session,
                clock: clock);
            var recorder = new BusinessAuditRecorder(failingWriter, owner.Session, clock);

            var finalization = await SeededSaleFactory.FinalizeSaleAsSessionAsync(
                factory, clock, owner.Session, seeded, 1, "A08A", token, recorder);

            var salesCount = await Zc04.ScalarAsync<int>(databaseConnection, "SELECT COUNT(*) FROM sales.Sales;");
            var auditRows = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM audit.AuditEvents WHERE Action=N'sale.finalize';");

            Assert.True(finalization.IsSuccess, finalization.IsFailure ? finalization.Error.Code : null);
            Assert.Equal(1, salesCount);
            Assert.Equal(0, auditRows);
            await AssertCommittedSaleStateAsync(databaseConnection, seeded);
            AssertSafeDiagnostic(
                diagnostics.Text,
                finalization.Value.OperationId,
                "DatabaseUnavailable");
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task ZC04A08B_AuditWriterThrowingAfterCommitMustNotSignalFailureForACommittedSale()
    {
        var token = TestContext.Current.CancellationToken;
        var server = Zc04.BuildServerConnection();
        var databaseName = Zc04.NewDatabaseName();
        var databaseConnection = Zc04.BuildDatabaseConnection(server.ConnectionString, databaseName);
        await Zc04.CreateDatabaseAsync(server.ConnectionString, databaseName);

        try
        {
            using var diagnostics = new TraceCaptureScope();
            var (factory, clock, owner, seeded) = await PrepareSaleEnvironmentAsync(databaseConnection, "A08B", token);
            var throwingWriter = new FaultingAuditWriter(
                mode: FaultMode.Throw,
                delegateToRealStore: false,
                realStore: null,
                sessionFactory: () => owner.Session,
                clock: clock);
            var recorder = new BusinessAuditRecorder(throwingWriter, owner.Session, clock);
            var operationId = Guid.NewGuid();
            var execution = await SeededSaleFactory.PrepareSaleAsSessionAsync(
                factory, clock, owner.Session, seeded, 1, operationId, token, recorder);

            var finalization = await execution.Handler.HandleAsync(execution.Command, token);
            var replay = await execution.Handler.HandleAsync(execution.Command, token);

            Assert.True(finalization.IsSuccess, finalization.IsFailure ? finalization.Error.Code : null);
            Assert.True(replay.IsSuccess, replay.IsFailure ? replay.Error.Code : null);
            Assert.Equal(finalization.Value.SaleId, replay.Value.SaleId);
            await AssertCommittedSaleStateAsync(databaseConnection, seeded);
            Assert.Equal(0, await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM audit.AuditEvents WHERE Action=N'sale.finalize';"));
            AssertSafeDiagnostic(diagnostics.Text, operationId, "InvalidOperationException");
            Assert.DoesNotContain("ZC04 fault injection", diagnostics.Text, StringComparison.Ordinal);
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task ZC04A08C_Control_RealAuditWriterRecordsFinalizedSale()
    {
        var token = TestContext.Current.CancellationToken;
        var server = Zc04.BuildServerConnection();
        var databaseName = Zc04.NewDatabaseName();
        var databaseConnection = Zc04.BuildDatabaseConnection(server.ConnectionString, databaseName);
        await Zc04.CreateDatabaseAsync(server.ConnectionString, databaseName);

        try
        {
            using var diagnostics = new TraceCaptureScope();
            var (factory, clock, owner, seeded) = await PrepareSaleEnvironmentAsync(databaseConnection, "A08C", token);
            var realRecorder = new BusinessAuditRecorder(
                new SqlAuditStore(factory, owner.Authorizer), owner.Session, clock);

            var finalization = await SeededSaleFactory.FinalizeSaleAsSessionAsync(
                factory, clock, owner.Session, seeded, 1, "A08C", token, realRecorder);

            Assert.True(finalization.IsSuccess, finalization.IsFailure ? finalization.Error.Code : null);
            await AssertCommittedSaleStateAsync(databaseConnection, seeded);
            Assert.Equal(1, await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM audit.AuditEvents WHERE Action=N'sale.finalize' AND Result=N'Success';"));
            Assert.DoesNotContain("audit failed non-fatally", diagnostics.Text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }

    private static async Task AssertCommittedSaleStateAsync(
        string databaseConnection,
        SeededSaleFactory.SeededProduct seeded)
    {
        Assert.Equal(1, await Zc04.ScalarAsync<int>(databaseConnection, "SELECT COUNT(*) FROM sales.Sales;"));
        Assert.Equal(1, await Zc04.ScalarAsync<int>(databaseConnection, "SELECT COUNT(*) FROM sales.SaleLines;"));
        Assert.Equal(1, await Zc04.ScalarAsync<int>(databaseConnection, "SELECT COUNT(*) FROM sales.SaleLineAllocations;"));
        Assert.Equal(1, await Zc04.ScalarAsync<int>(databaseConnection, "SELECT COUNT(*) FROM sales.Payments;"));
        Assert.Equal(1, await Zc04.ScalarAsync<int>(databaseConnection,
            "SELECT COUNT(*) FROM inventory.StockMovements WHERE MovementType=N'Sale';"));
        Assert.Equal(9, await Zc04.ScalarAsync<int>(databaseConnection,
            "SELECT AvailableQuantity FROM inventory.StockBalances WHERE ProductId=@productId;",
            new SqlParameter("@productId", SqlDbType.BigInt) { Value = seeded.ProductId }));
        Assert.Equal(1, await Zc04.ScalarAsync<int>(databaseConnection,
            "SELECT COUNT(*) FROM cash.CashLedgerEntries WHERE EntryType=N'SaleCashIn';"));
        Assert.Equal(10m, await Zc04.ScalarAsync<decimal>(databaseConnection,
            "SELECT COALESCE(SUM(Amount), 0) FROM cash.CashLedgerEntries WHERE EntryType=N'SaleCashIn';"));
        Assert.Equal(0, await Zc04.ScalarAsync<int>(databaseConnection,
            "SELECT COUNT(*) FROM customers.CustomerLedgerEntries;"));
    }

    private static void AssertSafeDiagnostic(
        string diagnostic,
        Guid operationId,
        string failureClass)
    {
        Assert.Contains("OperationType=sale.finalize", diagnostic, StringComparison.Ordinal);
        Assert.Contains($"OperationId={operationId:D}", diagnostic, StringComparison.Ordinal);
        Assert.Contains("OccurredAtUtc=", diagnostic, StringComparison.Ordinal);
        Assert.Contains($"FailureClass={failureClass}", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain(OwnerPassword, diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Data Source=", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Initial Catalog=", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=", diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(Zc04.Factory Factory, Zc04.Zc04Clock Clock, Zc04.OwnerContext Owner, SeededSaleFactory.SeededProduct Seeded)> PrepareSaleEnvironmentAsync(
        string databaseConnection, string tag, CancellationToken token)
    {
        await Zc04.MigrateAsync(databaseConnection);
        var factory = new Zc04.Factory(databaseConnection);
        var clock = new Zc04.Zc04Clock(new DateTime(2026, 8, 22, 13, 0, 0, DateTimeKind.Utc));
        var owner = await Zc04.BootstrapOwnerAsync(factory, clock, $"zc04.owner.{tag.ToLowerInvariant()}", OwnerPassword);
        var seeded = await SeededSaleFactory.SeedProductWithStockAsync(factory, clock, tag, token: token);
        return (factory, clock, owner, seeded);
    }

    private enum FaultMode
    {
        ReturnDatabaseUnavailable,
        Throw
    }

    /// <summary>
    /// Test-local IAuditWriter fault injection. Mirrors the two production
    /// failure shapes of SqlAuditStore.AppendAsync: returning
    /// security.database.unavailable (caught by the store) or letting a
    /// non-SQL exception propagate (not caught by the store).
    /// </summary>
    private sealed class FaultingAuditWriter : IAuditWriter
    {
        private readonly FaultMode mode;

        public FaultingAuditWriter(
            FaultMode mode,
            bool delegateToRealStore,
            SqlAuditStore? realStore,
            Func<CurrentSecuritySession> sessionFactory,
            Zc04.Zc04Clock clock)
        {
            this.mode = mode;
            _ = delegateToRealStore;
            _ = realStore;
            _ = sessionFactory;
            _ = clock;
        }

        public Task<Result> AppendAsync(AppendAuditEventRequest request, CancellationToken cancellationToken = default)
        {
            if (mode == FaultMode.Throw)
            {
                throw new InvalidOperationException(
                    "ZC04 fault injection: audit infrastructure failure after business commit.");
            }

            return Task.FromResult(Result.Failure(SecurityErrors.DatabaseUnavailable));
        }

        public Task<Result<IReadOnlyList<AuditEventView>>> SearchAsync(AuditSearchQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<IReadOnlyList<AuditEventView>>.Failure(SecurityErrors.PermissionDenied));
    }

    private sealed class TraceCaptureScope : IDisposable
    {
        private readonly CapturingTraceListener listener = new();

        public TraceCaptureScope() => Trace.Listeners.Add(listener);

        public string Text => listener.Text;

        public void Dispose()
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }

    private sealed class CapturingTraceListener : TraceListener
    {
        private readonly StringBuilder content = new();
        private readonly object syncRoot = new();

        public string Text
        {
            get
            {
                lock (syncRoot)
                {
                    return content.ToString();
                }
            }
        }

        public override void Write(string? message)
        {
            lock (syncRoot)
            {
                content.Append(message);
            }
        }

        public override void WriteLine(string? message)
        {
            lock (syncRoot)
            {
                content.AppendLine(message);
            }
        }
    }
}
