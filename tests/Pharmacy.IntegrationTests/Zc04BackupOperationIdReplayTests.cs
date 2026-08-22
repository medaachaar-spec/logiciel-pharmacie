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
/// the SAME OperationId and the SAME logical request must return that completed
/// operation. A conflicting request with the same OperationId is rejected.
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
            Assert.True(second.IsSuccess, second.IsFailure ? second.Error.Code : null);
            Assert.Equal(first.Value.Backup.FullPath, second.Value.Backup.FullPath);
            Assert.Equal(first.Value.Backup.Sha256, second.Value.Backup.Sha256);

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

            Assert.Equal(1, records);
            Assert.Equal(1, completedRecords);
            Assert.Equal(0, failedRecords);
            Assert.Single(backupFiles);

            var conflicting = await backups.CreateAsync(
                request with { Type = SqlBackupType.Differential }, token);
            Assert.True(conflicting.IsFailure);
            Assert.Equal("backup.operation.conflict", conflicting.Error.Code);
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
