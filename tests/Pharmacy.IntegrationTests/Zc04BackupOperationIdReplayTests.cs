using System.Data;
using Microsoft.Data.SqlClient;
using Pharmacy.Application.Operations.Models;
using Pharmacy.Infrastructure.Operations;
using Xunit;

namespace Pharmacy.IntegrationTests;

/// <summary>
/// ZCODE-04A / ZC-011: backup OperationId replay.
/// Uses a disposable SQL database plus a temporary backup directory. The
/// first CreateAsync with OperationId X must succeed; the second call with
/// the SAME OperationId and the SAME logical request is observed.
/// Invariant: retrying a successfully completed operation must either succeed
/// idempotently (returning the completed state) or clearly report that the
/// operation was already completed. A misleading generic failure caused by the
/// unique OperationId index is REPRODUCED.
/// </summary>
[Collection("Sprint 09 persistence serial")]
public sealed class Zc04BackupOperationIdReplayTests
{
    private const string OwnerPassword = "Zc04-Owner-Password-2026!";
    private const string BackupDirectoryVariable = "PHARMACY_BACKUP_LOCAL_DIRECTORY";

    [Fact]
    public async Task ZC04A06_RetryingCompletedBackupWithSameOperationIdMustNotReportMisleadingFailure()
    {
        var token = TestContext.Current.CancellationToken;
        var backupDirectory = Path.Combine(
            Environment.GetEnvironmentVariable("PHARMACY_BACKUP_LOCAL_DIRECTORY")
                ?? Environment.GetEnvironmentVariable("TEMP")
                ?? Path.GetTempPath(),
            "ZC04-A06-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(backupDirectory);
        var previousBackupDirectory = Environment.GetEnvironmentVariable(BackupDirectoryVariable);

        var server = Zc04.BuildServerConnection();
        var databaseName = Zc04.NewDatabaseName();
        var databaseConnection = Zc04.BuildDatabaseConnection(server.ConnectionString, databaseName);
        await Zc04.CreateDatabaseAsync(server.ConnectionString, databaseName);

        try
        {
            await Zc04.MigrateAsync(databaseConnection);
            var factory = new Zc04.Factory(databaseConnection);
            var clock = new Zc04.Zc04Clock(new DateTime(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc));
            var owner = await Zc04.BootstrapOwnerAsync(factory, clock, "zc04.owner.bak", OwnerPassword);
            Environment.SetEnvironmentVariable(BackupDirectoryVariable, backupDirectory);

            var backups = new SqlBackupRecoveryService(
                factory, owner.Authorizer, new Zc04.NoOpAuditRecorder(), clock);

            var operationId = Guid.NewGuid();
            var request = new CreateBackupRequest(operationId, SqlBackupType.Full, CopyToExternal: false);

            var first = await backups.CreateAsync(request, token);
            Assert.True(first.IsSuccess, first.IsFailure ? first.Error.Code : null);
            Assert.Equal("VerifyOnlyPassed", first.Value.Backup.VerificationStatus);
            var firstPath = first.Value.Backup.FullPath;
            Assert.True(File.Exists(firstPath));

            var second = await backups.CreateAsync(request, token);

            var records = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM operations.BackupRecords WHERE OperationId=@operationId;",
                new SqlParameter("@operationId", SqlDbType.UniqueIdentifier) { Value = operationId });
            var completedRecords = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM operations.BackupRecords WHERE OperationId=@operationId AND Status=N'Completed';",
                new SqlParameter("@operationId", SqlDbType.UniqueIdentifier) { Value = operationId });
            var failedRecords = await Zc04.ScalarAsync<int>(databaseConnection,
                "SELECT COUNT(*) FROM operations.BackupRecords WHERE OperationId=@operationId AND Status=N'Failed';",
                new SqlParameter("@operationId", SqlDbType.UniqueIdentifier) { Value = operationId });
            var backupFiles = Directory.GetFiles(backupDirectory, "*.bak");

            // INVARIANT: an idempotent retry returns the completed state; a rejected retry
            // clearly states the operation already completed. RED = the retry of a
            // SUCCESSFUL backup is reported as a fresh failure (unique-key conflict),
            // which teaches operators to distrust legitimate retries.
            if (second.IsFailure)
            {
                var clearlyAlreadyCompleted = second.Error.Code.Contains("already", StringComparison.OrdinalIgnoreCase)
                    || second.Error.Code.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                    || second.Error.Code.Contains("completed", StringComparison.OrdinalIgnoreCase);
                if (!clearlyAlreadyCompleted)
                {
                    Assert.Fail(
                        "ZC-011 REPRODUCED: retrying a successfully completed backup with the same " +
                        $"OperationId returned a misleading failure (error code '{second.Error.Code}', " +
                        $"message '{second.Error.Message}'). BackupRecords for OperationId: {records} total, " +
                        $"{completedRecords} Completed, {failedRecords} Failed; backup files on disk: {backupFiles.Length}.");
                }
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(BackupDirectoryVariable, previousBackupDirectory);
            try
            {
                if (Directory.Exists(backupDirectory))
                {
                    Directory.Delete(backupDirectory, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await Zc04.DropDatabaseAsync(server.ConnectionString, databaseName);
        }
    }
}
