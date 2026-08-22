using System.Data;
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
/// No production code is modified.
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

            // INVARIANT: when the caller receives SUCCESS for a finalized sale, the audit
            // trail for that sale must exist. RED = sale committed + success returned +
            // audit silently lost (ZC-017 silent variant).
            if (finalization.IsSuccess && salesCount == 1 && auditRows == 0)
            {
                Assert.Fail(
                    "ZC-017 REPRODUCED (silent loss): sale committed and handler returned SUCCESS, " +
                    "but the post-commit audit write failed with security.database_unavailable " +
                    "and the failure was silently swallowed by BusinessAuditRecorder.");
            }

            Assert.True(finalization.IsSuccess, finalization.IsFailure ? finalization.Error.Code : null);
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
            var (factory, clock, owner, seeded) = await PrepareSaleEnvironmentAsync(databaseConnection, "A08B", token);
            var throwingWriter = new FaultingAuditWriter(
                mode: FaultMode.Throw,
                delegateToRealStore: false,
                realStore: null,
                sessionFactory: () => owner.Session,
                clock: clock);
            var recorder = new BusinessAuditRecorder(throwingWriter, owner.Session, clock);

            Exception? thrown = null;
            Result<FinalizedSaleView>? finalization = null;
            try
            {
                finalization = await SeededSaleFactory.FinalizeSaleAsSessionAsync(
                    factory, clock, owner.Session, seeded, 1, "A08B", token, recorder);
            }
            catch (Exception exception)
            {
                thrown = exception;
            }

            var salesCount = await Zc04.ScalarAsync<int>(databaseConnection, "SELECT COUNT(*) FROM sales.Sales;");
            var paymentsCount = await Zc04.ScalarAsync<int>(databaseConnection, "SELECT COUNT(*) FROM sales.Payments;");
            var movementsCount = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM inventory.StockMovements WHERE MovementType=N'Sale';");

            // INVARIANT: if the caller receives a failure (exception or error result), the
            // business transaction must not be committed — otherwise the operator will
            // reasonably retry an already-committed financial transaction.
            // RED = exception surfaced to caller while the sale fully committed.
            var callerSawFailure = thrown is not null || finalization?.IsFailure == true;
            if (callerSawFailure && salesCount == 1)
            {
                Assert.Fail(
                    "ZC-017 REPRODUCED (retry risk): the audit writer threw AFTER the sale committed; " +
                    $"the caller received {(thrown is not null ? $"an exception ({thrown.GetType().Name})" : $"an error result ({finalization!.Error.Code})")} " +
                    $"while sales.Sales={salesCount}, sales.Payments={paymentsCount}, Sale stock movements={movementsCount}. " +
                    "An operator would retry this already-committed financial transaction.");
            }

            Assert.False(callerSawFailure, "Caller must not observe failure for a committed sale.");
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
            var (factory, clock, owner, seeded) = await PrepareSaleEnvironmentAsync(databaseConnection, "A08C", token);
            var realRecorder = new BusinessAuditRecorder(
                new SqlAuditStore(factory, owner.Authorizer), owner.Session, clock);

            var finalization = await SeededSaleFactory.FinalizeSaleAsSessionAsync(
                factory, clock, owner.Session, seeded, 1, "A08C", token, realRecorder);

            Assert.True(finalization.IsSuccess, finalization.IsFailure ? finalization.Error.Code : null);
            Assert.Equal(1, await Zc04.ScalarAsync<int>(databaseConnection, "SELECT COUNT(*) FROM sales.Sales;"));
            Assert.Equal(1, await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM audit.AuditEvents WHERE Action=N'sale.finalize' AND Result=N'Success';"));
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
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
    /// security.database_unavailable (caught by the store) or letting a
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
}
