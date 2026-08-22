using System.ComponentModel;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Operations.Contracts;
using Pharmacy.Application.Operations.Errors;
using Pharmacy.Application.Operations.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Operations;

namespace Pharmacy.Infrastructure.Operations;

public sealed class SqlBackupRecoveryService : IBackupRecoveryService
{
    private const string LocalDirectoryVariable = "PHARMACY_BACKUP_LOCAL_DIRECTORY";
    private const string ExternalDirectoryVariable = "PHARMACY_BACKUP_EXTERNAL_DIRECTORY";
    private const string ExternalEncryptionVariable = "PHARMACY_BACKUP_EXTERNAL_ENCRYPTED";
    private static readonly Regex DatabaseNamePattern = new("^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant);
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;
    private readonly IPermissionAuthorizer authorizer;
    private readonly IBusinessAuditRecorder audit;
    private readonly ISecurityClock clock;

    public SqlBackupRecoveryService(IDbContextFactory<PharmacyDbContext> contextFactory, IPermissionAuthorizer authorizer, IBusinessAuditRecorder audit, ISecurityClock clock)
    {
        this.contextFactory = contextFactory; this.authorizer = authorizer; this.audit = audit; this.clock = clock;
    }

    public async Task<Result<BackupConfigurationView>> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var denied = authorizer.Authorize(SecurityPermissions.BackupView);
        if (denied.IsFailure) return Result<BackupConfigurationView>.Failure(denied.Error);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var connection = (SqlConnection)context.Database.GetDbConnection();
            var builder = new SqlConnectionStringBuilder(connection.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            var defaultBackup = await ScalarStringAsync(connection, "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultBackupPath'));", cancellationToken);
            var edition = await ScalarStringAsync(connection, "SELECT CONVERT(nvarchar(256), SERVERPROPERTY('Edition'));", cancellationToken);
            var recovery = await ScalarStringAsync(connection, "SELECT recovery_model_desc FROM sys.databases WHERE name=DB_NAME();", cancellationToken);
            var local = Environment.GetEnvironmentVariable(LocalDirectoryVariable) ?? defaultBackup;
            var external = Environment.GetEnvironmentVariable(ExternalDirectoryVariable);
            var encrypted = string.Equals(Environment.GetEnvironmentVariable(ExternalEncryptionVariable), "true", StringComparison.OrdinalIgnoreCase);
            var note = edition.Contains("Express", StringComparison.OrdinalIgnoreCase)
                ? "SQL Server Express exécute BACKUP/RESTORE mais ne fournit pas SQL Server Agent ; planifier via le Planificateur de tâches Windows."
                : "SQL Server Developer détecté ; le pilote Express devra utiliser le Planificateur de tâches Windows, sans dépendre de SQL Server Agent.";
            return Result<BackupConfigurationView>.Success(new BackupConfigurationView(
                builder.InitialCatalog, edition, recovery, local, external,
                !string.IsNullOrWhiteSpace(external), encrypted, "Windows Task Scheduler", note));
        }
        catch (Exception ex) when (IsUnavailable(ex)) { return Result<BackupConfigurationView>.Failure(OperationsErrors.DatabaseUnavailable); }
    }

    public async Task<Result<IReadOnlyList<BackupFileView>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await GetConfigurationAsync(cancellationToken);
        if (configuration.IsFailure) return Result<IReadOnlyList<BackupFileView>>.Failure(configuration.Error);
        var directory = configuration.Value.LocalDirectory;
        if (!Directory.Exists(directory)) return Result<IReadOnlyList<BackupFileView>>.Success(Array.Empty<BackupFileView>());
        var prefix = $"Pharmacy_{configuration.Value.DatabaseName}_";
        var files = Directory.EnumerateFiles(directory, $"{prefix}*.*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetExtension(path) is ".bak" or ".trn")
            .Select(path => new FileInfo(path)).OrderByDescending(file => file.LastWriteTimeUtc).Take(200)
            .Select(file => new BackupFileView(file.Name, file.FullName, TypeFromName(file.Name), file.Length,
                file.LastWriteTimeUtc, null, "Non vérifié dans cette liste", "Inconnu"))
            .ToArray();
        return Result<IReadOnlyList<BackupFileView>>.Success(Array.AsReadOnly(files));
    }

    public async Task<Result<BackupExecutionView>> CreateAsync(CreateBackupRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OperationId == Guid.Empty) return Result<BackupExecutionView>.Failure(OperationsErrors.InvalidOperationId);
        var denied = authorizer.Authorize(SecurityPermissions.BackupCreate);
        if (denied.IsFailure) { await AuditFailureQuietlyAsync("backup.create", request.OperationId, denied.Error.Code, cancellationToken); return Result<BackupExecutionView>.Failure(denied.Error); }
        var configuration = await GetConfigurationForOperationAsync(cancellationToken);
        if (configuration.IsFailure) return Result<BackupExecutionView>.Failure(configuration.Error);
        var started = clock.UtcNow;
        var extension = request.Type == SqlBackupType.TransactionLog ? ".trn" : ".bak";
        var safeName = $"Pharmacy_{configuration.Value.DatabaseName}_{request.Type}_{started:yyyyMMdd_HHmmss}_{request.OperationId:N}"[..Math.Min(120, $"Pharmacy_{configuration.Value.DatabaseName}_{request.Type}_{started:yyyyMMdd_HHmmss}_{request.OperationId:N}".Length)] + extension;
        var pathResult = SafeCombine(configuration.Value.LocalDirectory, safeName);
        if (pathResult is null) return Result<BackupExecutionView>.Failure(OperationsErrors.BackupPathInvalid);
        Directory.CreateDirectory(configuration.Value.LocalDirectory);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.BackupRecords.SingleOrDefaultAsync(
            candidate => candidate.OperationId == request.OperationId,
            cancellationToken);
        if (existing is not null)
        {
            return ReplayCompletedBackup(existing, request, configuration.Value);
        }

        var record = new BackupRecordEntity
        {
            OperationId = request.OperationId,
            BackupType = request.Type.ToString(),
            Status = "Running",
            FileName = safeName,
            VerificationStatus = "Pending",
            ExternalCopyStatus = "NotRequested",
            CreatedAtUtc = started
        };
        context.BackupRecords.Add(record);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            var connectionString = context.Database.GetConnectionString()!;
            await ExecuteBackupAsync(connectionString, configuration.Value.DatabaseName, pathResult, request.Type, cancellationToken);
            await VerifySqlAsync(connectionString, pathResult, cancellationToken);
            var info = new FileInfo(pathResult);
            if (!info.Exists || info.Length <= 0) throw new IOException("Le fichier SQL de sauvegarde est vide ou absent.");
            var hash = await HashAsync(pathResult, cancellationToken);
            var externalStatus = "NotRequested";
            if (request.CopyToExternal)
            {
                if (string.IsNullOrWhiteSpace(configuration.Value.ExternalDirectory)) externalStatus = "Unavailable";
                else if (!configuration.Value.EncryptionConfigured) externalStatus = "BlockedEncryptionNotAttested";
                else
                {
                    Directory.CreateDirectory(configuration.Value.ExternalDirectory);
                    var externalPath = SafeCombine(configuration.Value.ExternalDirectory, safeName)
                        ?? throw new IOException("Chemin externe invalide.");
                    File.Copy(pathResult, externalPath, overwrite: false);
                    externalStatus = await HashAsync(externalPath, cancellationToken) == hash
                        ? "CopiedEncryptedVolume"
                        : "HashMismatch";
                    if (externalStatus == "HashMismatch") throw new IOException("La copie externe ne correspond pas au hash source.");
                }
            }
            record.Status = "Completed"; record.FileSize = info.Length; record.FileHash = hash;
            record.VerificationStatus = "VerifyOnlyPassed"; record.ExternalCopyStatus = externalStatus; record.CompletedAtUtc = clock.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            await audit.RecordSuccessAsync("backup", "BackupRecord", record.Id.ToString(), "backup.create", request.OperationId, cancellationToken);
            var view = new BackupFileView(safeName, pathResult, request.Type, info.Length, info.LastWriteTimeUtc, hash, record.VerificationStatus, externalStatus);
            return Result<BackupExecutionView>.Success(new BackupExecutionView(request.OperationId, view, started, record.CompletedAtUtc.Value));
        }
        catch (Exception ex)
        {
            record.Status = "Failed"; record.ErrorCode = FailureCode(ex); record.CompletedAtUtc = clock.UtcNow;
            try { await context.SaveChangesAsync(cancellationToken); } catch { }
            await AuditFailureQuietlyAsync("backup.create", request.OperationId, record.ErrorCode, cancellationToken);
            return Result<BackupExecutionView>.Failure(new Error(record.ErrorCode, "La sauvegarde n’a pas abouti ; aucune réussite n’a été déclarée."));
        }
    }

    private static Result<BackupExecutionView> ReplayCompletedBackup(
        BackupRecordEntity record,
        CreateBackupRequest request,
        BackupConfigurationView configuration)
    {
        var existingCopyToExternal = !string.Equals(record.ExternalCopyStatus, "NotRequested", StringComparison.Ordinal);
        if (!string.Equals(record.BackupType, request.Type.ToString(), StringComparison.Ordinal)
            || existingCopyToExternal != request.CopyToExternal
            || !string.Equals(record.Status, "Completed", StringComparison.Ordinal)
            || record.CompletedAtUtc is null
            || record.FileSize is null
            || string.IsNullOrWhiteSpace(record.FileHash)
            || !string.Equals(record.VerificationStatus, "VerifyOnlyPassed", StringComparison.Ordinal))
        {
            return Result<BackupExecutionView>.Failure(OperationsErrors.BackupOperationConflict);
        }

        var fullPath = SafeCombine(configuration.LocalDirectory, record.FileName);
        if (fullPath is null || !File.Exists(fullPath))
            return Result<BackupExecutionView>.Failure(OperationsErrors.BackupOperationConflict);

        var file = new FileInfo(fullPath);
        if (file.Length != record.FileSize.Value)
            return Result<BackupExecutionView>.Failure(OperationsErrors.BackupOperationConflict);

        var view = new BackupFileView(record.FileName, fullPath, request.Type, file.Length,
            file.LastWriteTimeUtc, record.FileHash, record.VerificationStatus, record.ExternalCopyStatus);
        return Result<BackupExecutionView>.Success(new BackupExecutionView(
            request.OperationId, view, record.CreatedAtUtc, record.CompletedAtUtc.Value));
    }

    public async Task<Result<BackupFileView>> VerifyAsync(VerifyBackupRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OperationId == Guid.Empty) return Result<BackupFileView>.Failure(OperationsErrors.InvalidOperationId);
        var denied = authorizer.Authorize(SecurityPermissions.BackupCreate);
        if (denied.IsFailure) { await AuditFailureQuietlyAsync("backup.verify", request.OperationId, denied.Error.Code, cancellationToken); return Result<BackupFileView>.Failure(denied.Error); }
        var configuration = await GetConfigurationForOperationAsync(cancellationToken);
        if (configuration.IsFailure) return Result<BackupFileView>.Failure(configuration.Error);
        if (!IsAllowedFile(request.FullPath, configuration.Value)) { await AuditFailureQuietlyAsync("backup.verify", request.OperationId, OperationsErrors.BackupPathInvalid.Code, cancellationToken); return Result<BackupFileView>.Failure(OperationsErrors.BackupPathInvalid); }
        if (!File.Exists(request.FullPath)) { await AuditFailureQuietlyAsync("backup.verify", request.OperationId, OperationsErrors.BackupNotFound.Code, cancellationToken); return Result<BackupFileView>.Failure(OperationsErrors.BackupNotFound); }
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await VerifySqlAsync(context.Database.GetConnectionString()!, request.FullPath, cancellationToken);
            var file = new FileInfo(request.FullPath);
            var hash = await HashAsync(request.FullPath, cancellationToken);
            await audit.RecordSuccessAsync("backup", "BackupFile", file.Name, "backup.verify", request.OperationId, cancellationToken);
            return Result<BackupFileView>.Success(new BackupFileView(file.Name, file.FullName, TypeFromName(file.Name), file.Length, file.LastWriteTimeUtc, hash, "VerifyOnlyPassed", "Inconnu"));
        }
        catch (Exception ex) { var code = FailureCode(ex); await AuditFailureQuietlyAsync("backup.verify", request.OperationId, code, cancellationToken); return Result<BackupFileView>.Failure(new Error(code, "La vérification SQL du fichier a échoué.")); }
    }

    public async Task<Result<RestoreTestView>> RestoreTestAsync(RestoreTestRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OperationId == Guid.Empty) return Result<RestoreTestView>.Failure(OperationsErrors.InvalidOperationId);
        var denied = authorizer.Authorize(SecurityPermissions.BackupRestore, true);
        if (denied.IsFailure) { await AuditFailureQuietlyAsync("backup.restore.test", request.OperationId, denied.Error.Code, cancellationToken); return Result<RestoreTestView>.Failure(denied.Error); }
        var configuration = await GetConfigurationForOperationAsync(cancellationToken);
        if (configuration.IsFailure) return Result<RestoreTestView>.Failure(configuration.Error);
        if (!IsAllowedFile(request.FullPath, configuration.Value) || !File.Exists(request.FullPath))
        {
            await AuditFailureQuietlyAsync("backup.restore.test", request.OperationId, OperationsErrors.BackupPathInvalid.Code, cancellationToken);
            return Result<RestoreTestView>.Failure(OperationsErrors.BackupPathInvalid);
        }
        var tempName = "Pharmacy_RestoreTest_" + request.OperationId.ToString("N")[..12];
        var cleanup = false;
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var source = new SqlConnectionStringBuilder(context.Database.GetConnectionString()!);
            var master = new SqlConnectionStringBuilder(source.ConnectionString) { InitialCatalog = "master", Pooling = false };
            await using var connection = new SqlConnection(master.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await VerifySqlAsync(master.ConnectionString, request.FullPath, cancellationToken);
            var files = await ReadBackupFilesAsync(connection, request.FullPath, cancellationToken);
            var dataPath = await ScalarStringAsync(connection, "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultDataPath'));", cancellationToken);
            var logPath = await ScalarStringAsync(connection, "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultLogPath'));", cancellationToken);
            var moves = files.Select((file, index) => new { file.LogicalName, Path = Path.Combine(file.Type == "L" ? logPath : dataPath, $"{tempName}_{index}{(file.Type == "L" ? ".ldf" : ".mdf")}") }).ToArray();
            await using (var restore = connection.CreateCommand())
            {
                restore.CommandTimeout = 300;
                restore.CommandText = $"RESTORE DATABASE [{tempName}] FROM DISK=@path WITH " + string.Join(", ", moves.Select((_, i) => $"MOVE @logical{i} TO @physical{i}")) + ", RECOVERY, CHECKSUM;";
                restore.Parameters.AddWithValue("@path", request.FullPath);
                for (var i = 0; i < moves.Length; i++) { restore.Parameters.AddWithValue($"@logical{i}", moves[i].LogicalName); restore.Parameters.AddWithValue($"@physical{i}", moves[i].Path); }
                await restore.ExecuteNonQueryAsync(cancellationToken);
            }
            var restored = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = tempName };
            await using var health = new SqlConnection(restored.ConnectionString);
            await health.OpenAsync(cancellationToken);
            var migrations = await ScalarIntAsync(health, "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;", cancellationToken);
            var coreCritical = await ScalarIntAsync(health, "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE (s.name=N'catalog' AND t.name=N'Products') OR (s.name=N'inventory' AND t.name=N'StockBalances') OR (s.name=N'sales' AND t.name=N'Sales') OR (s.name=N'security' AND t.name=N'Permissions');", cancellationToken);
            var brandingTable = await ScalarIntAsync(health, "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'configuration' AND t.name=N'PharmacyBranding';", cancellationToken);
            var settingsTable = await ScalarIntAsync(health, "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'configuration' AND t.name=N'ApplicationSettings';", cancellationToken);
            var critical = coreCritical + brandingTable + settingsTable;
            string? pharmacyName = null;
            string? primaryColor = null;
            string? logoSha256 = null;
            bool? receiptPrintingEnabled = null;
            if (brandingTable == 1)
            {
                pharmacyName = await ScalarStringAsync(health, "SELECT PharmacyName FROM configuration.PharmacyBranding WHERE Id=1;", cancellationToken);
                primaryColor = await ScalarStringAsync(health, "SELECT PrimaryColor FROM configuration.PharmacyBranding WHERE Id=1;", cancellationToken);
                logoSha256 = await ScalarStringAsync(health, "SELECT COALESCE(CONVERT(varchar(64), HASHBYTES('SHA2_256', LogoContent), 2), '') FROM configuration.PharmacyBranding WHERE Id=1;", cancellationToken);
            }
            if (settingsTable == 1)
            {
                receiptPrintingEnabled = await ScalarIntAsync(health, "SELECT CONVERT(int, ReceiptPrintingEnabled) FROM configuration.ApplicationSettings WHERE Id=1;", cancellationToken) == 1;
            }
            if (migrations <= 0 || coreCritical != 4 || brandingTable != 1 || settingsTable != 1
                || brandingTable == 1 && (string.IsNullOrWhiteSpace(pharmacyName) || string.IsNullOrWhiteSpace(primaryColor)))
                throw new InvalidOperationException("Le health check de restauration est incomplet.");
            await health.CloseAsync();
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(connection, tempName, cancellationToken); cleanup = true;
            await audit.RecordSuccessAsync("backup", "RestoreTest", tempName, "backup.restore.test", request.OperationId, cancellationToken);
            return Result<RestoreTestView>.Success(new RestoreTestView(
                request.OperationId, tempName, migrations, critical, true, true, clock.UtcNow,
                pharmacyName, primaryColor, string.IsNullOrEmpty(logoSha256) ? null : logoSha256,
                receiptPrintingEnabled));
        }
        catch (Exception ex)
        {
            try
            {
                SqlConnection.ClearAllPools();
                await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
                var master = new SqlConnectionStringBuilder(context.Database.GetConnectionString()!) { InitialCatalog = "master", Pooling = false };
                await using var connection = new SqlConnection(master.ConnectionString); await connection.OpenAsync(cancellationToken);
                await DropDatabaseAsync(connection, tempName, cancellationToken); cleanup = true;
            }
            catch { }
            var code = FailureCode(ex);
            await AuditFailureQuietlyAsync("backup.restore.test", request.OperationId, code, cancellationToken);
            return Result<RestoreTestView>.Failure(new Error(code, cleanup ? "La restauration de test a échoué ; la base temporaire a été nettoyée." : "La restauration de test a échoué et le nettoyage doit être vérifié."));
        }
    }

    private async Task<Result<BackupConfigurationView>> GetConfigurationForOperationAsync(CancellationToken token)
    {
        // Bypass the view permission because create/restore already carry their own stronger permission.
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(token);
            var connection = (SqlConnection)context.Database.GetDbConnection(); var cs = new SqlConnectionStringBuilder(connection.ConnectionString);
            await connection.OpenAsync(token);
            var local = Environment.GetEnvironmentVariable(LocalDirectoryVariable) ?? await ScalarStringAsync(connection, "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultBackupPath'));", token);
            if (string.IsNullOrWhiteSpace(local) || !Path.IsPathRooted(local)) return Result<BackupConfigurationView>.Failure(OperationsErrors.BackupPathNotConfigured);
            var external = Environment.GetEnvironmentVariable(ExternalDirectoryVariable);
            if (!string.IsNullOrWhiteSpace(external) && !Path.IsPathRooted(external)) return Result<BackupConfigurationView>.Failure(OperationsErrors.BackupPathInvalid);
            var edition = await ScalarStringAsync(connection, "SELECT CONVERT(nvarchar(256), SERVERPROPERTY('Edition'));", token);
            var recovery = await ScalarStringAsync(connection, "SELECT recovery_model_desc FROM sys.databases WHERE name=DB_NAME();", token);
            return Result<BackupConfigurationView>.Success(new(cs.InitialCatalog, edition, recovery, Path.GetFullPath(local), string.IsNullOrWhiteSpace(external) ? null : Path.GetFullPath(external), !string.IsNullOrWhiteSpace(external), string.Equals(Environment.GetEnvironmentVariable(ExternalEncryptionVariable), "true", StringComparison.OrdinalIgnoreCase), "Windows Task Scheduler", "Sauvegarde SQL contrôlée."));
        }
        catch (Exception ex) when (IsUnavailable(ex)) { return Result<BackupConfigurationView>.Failure(OperationsErrors.DatabaseUnavailable); }
    }

    private static async Task ExecuteBackupAsync(string connectionString, string database, string path, SqlBackupType type, CancellationToken token)
    {
        if (!DatabaseNamePattern.IsMatch(database)) throw new InvalidOperationException("Nom de base non autorisé.");
        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master", Pooling = false };
        await using var connection = new SqlConnection(master.ConnectionString); await connection.OpenAsync(token);
        await using var command = connection.CreateCommand(); command.CommandTimeout = 300;
        var options = type switch { SqlBackupType.Full => "COPY_ONLY, CHECKSUM, INIT", SqlBackupType.Differential => "DIFFERENTIAL, CHECKSUM, INIT", SqlBackupType.TransactionLog => "CHECKSUM, INIT", _ => throw new ArgumentOutOfRangeException(nameof(type)) };
        command.CommandText = type == SqlBackupType.TransactionLog
            ? $"BACKUP LOG [{database}] TO DISK=@path WITH {options};"
            : $"BACKUP DATABASE [{database}] TO DISK=@path WITH {options};";
        command.Parameters.AddWithValue("@path", path); await command.ExecuteNonQueryAsync(token);
    }

    private static async Task VerifySqlAsync(string connectionString, string path, CancellationToken token)
    {
        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master", Pooling = false };
        await using var connection = new SqlConnection(master.ConnectionString); await connection.OpenAsync(token);
        await using var command = connection.CreateCommand(); command.CommandTimeout = 300;
        command.CommandText = "RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM;"; command.Parameters.AddWithValue("@path", path);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<IReadOnlyList<(string LogicalName, string Type)>> ReadBackupFilesAsync(SqlConnection connection, string path, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.CommandText = "RESTORE FILELISTONLY FROM DISK=@path;"; command.Parameters.AddWithValue("@path", path);
        await using var reader = await command.ExecuteReaderAsync(token); var values = new List<(string, string)>();
        var logical = reader.GetOrdinal("LogicalName"); var type = reader.GetOrdinal("Type");
        while (await reader.ReadAsync(token)) values.Add((reader.GetString(logical), reader.GetString(type)));
        return values;
    }

    private static async Task DropDatabaseAsync(SqlConnection connection, string name, CancellationToken token)
    {
        if (!name.StartsWith("Pharmacy_RestoreTest_", StringComparison.Ordinal) || !DatabaseNamePattern.IsMatch(name)) throw new InvalidOperationException("Cible de nettoyage interdite.");
        await using var command = connection.CreateCommand(); command.CommandText = $"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END;"; await command.ExecuteNonQueryAsync(token);
    }

    private static string? SafeCombine(string root, string name)
    {
        if (!Path.IsPathRooted(root) || Path.GetFileName(name) != name) return null;
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(normalizedRoot, name));
        return candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) ? candidate : null;
    }

    private static bool IsAllowedFile(string path, BackupConfigurationView configuration)
    {
        if (!Path.IsPathRooted(path) || Path.GetExtension(path) is not (".bak" or ".trn")) return false;
        var full = Path.GetFullPath(path);
        return IsWithin(full, configuration.LocalDirectory) || (!string.IsNullOrWhiteSpace(configuration.ExternalDirectory) && IsWithin(full, configuration.ExternalDirectory));
    }
    private static bool IsWithin(string path, string root) => path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static SqlBackupType TypeFromName(string name) => name.Contains("TransactionLog", StringComparison.OrdinalIgnoreCase) ? SqlBackupType.TransactionLog : name.Contains("Differential", StringComparison.OrdinalIgnoreCase) ? SqlBackupType.Differential : SqlBackupType.Full;
    private static async Task<string> HashAsync(string path, CancellationToken token) { await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true); return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)); }
    private static string FailureCode(Exception ex) => ex is IOException or UnauthorizedAccessException ? "backup.file_io" : IsUnavailable(ex) ? "backup.database.unavailable" : "backup.operation.failed";
    private static bool IsUnavailable(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is InvalidOperationException or TimeoutException
                || current is Win32Exception { NativeErrorCode: 258 or 10054 or 10060 }
                || current is SqlException { Number: -2 or 2 or 20 or 40 or 53 or 64 or 233 or 258 or 4060 or 10054 or 10060 or 18456 })
            {
                return true;
            }
        }

        return false;
    }
    private static async Task<string> ScalarStringAsync(SqlConnection c, string sql, CancellationToken t) { await using var cmd = c.CreateCommand(); cmd.CommandText = sql; return Convert.ToString(await cmd.ExecuteScalarAsync(t), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty; }
    private static async Task<int> ScalarIntAsync(SqlConnection c, string sql, CancellationToken t) { await using var cmd = c.CreateCommand(); cmd.CommandText = sql; return Convert.ToInt32(await cmd.ExecuteScalarAsync(t), System.Globalization.CultureInfo.InvariantCulture); }
    private async Task AuditFailureQuietlyAsync(string action, Guid operationId, string reasonCode, CancellationToken token) { try { await audit.RecordFailureAsync("backup", "BackupOperation", null, action, operationId, reasonCode, token); } catch { } }
}
