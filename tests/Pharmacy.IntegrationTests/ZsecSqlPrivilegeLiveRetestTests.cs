using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Pharmacy.IntegrationTests;

/// <summary>
/// Live SQL-PILOT-SEC-02 regression.  The committed Windows provisioning and
/// read-only verifier run against a guarded disposable database.  Behavioral
/// probes then use an ephemeral SQL login only as an authorization surrogate;
/// its random password is held in memory, never logged or persisted, and the
/// login is removed in the finally block.  No production database is named.
/// </summary>
[Collection("Sprint 09 persistence serial")]
public sealed class ZsecSqlPrivilegeLiveRetestTests
{
    private const string RestoreRoleName = "PharmacyAppRestoreTest";
    private const string RuntimeRoleName = "PharmacyAppRuntime";
    private const string RestorePrefix = "Pharmacy_RestoreTest_ZSEC02_";

    [Fact]
    public async Task LiveProvisioningAndAuthorizationBoundaryHold()
    {
        var token = TestContext.Current.CancellationToken;
        var server = Zc04.BuildServerConnection();
        var serverConnection = server.ConnectionString;
        var databaseName = Zc04.NewDatabaseName();
        var restoreDatabaseName = RestorePrefix + Guid.NewGuid().ToString("N")[..12];
        var deploymentLogin = $"{Environment.UserDomainName}\\{Environment.UserName}";
        var runtimeLogin = await SelectNonAdminSqlServiceLoginAsync(serverConnection, token);
        var backupRoot = Environment.GetEnvironmentVariable("PHARMACY_BACKUP_LOCAL_DIRECTORY");
        if (string.IsNullOrWhiteSpace(backupRoot))
        {
            throw new InvalidOperationException("PHARMACY_BACKUP_LOCAL_DIRECTORY is required for the guarded live retest.");
        }

        backupRoot = Path.GetFullPath(backupRoot);
        Directory.CreateDirectory(backupRoot);
        var backupPath = Path.Combine(backupRoot, $"zsec-sec02-{Guid.NewGuid():N}.bak");
        if (!backupPath.StartsWith(backupRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Backup path escaped the guarded test directory.");
        }

        Assert.NotEqual(runtimeLogin, deploymentLogin, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(0, await ScalarAsync<int>(serverConnection,
            "SELECT IS_SRVROLEMEMBER(N'sysadmin', @login);",
            token, new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = runtimeLogin }));
        var restoreRoleWasPresent = await ScalarAsync<int>(serverConnection,
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'PharmacyAppRestoreTest' AND type=N'R') THEN 1 ELSE 0 END;",
            token) == 1;
        Assert.False(restoreRoleWasPresent, "A pre-existing test role would not be safe to mutate.");

        var deploymentWasDbcreator = await ScalarAsync<int>(serverConnection,
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.server_role_members m JOIN sys.server_principals r ON r.principal_id=m.role_principal_id JOIN sys.server_principals l ON l.principal_id=m.member_principal_id WHERE r.name=N'dbcreator' AND l.name=@login) THEN 1 ELSE 0 END;",
            token, new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = deploymentLogin }) == 1;
        var databaseConnection = new SqlConnectionStringBuilder(serverConnection)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Pharmacy.ZSEC.SEC02",
            Pooling = false
        }.ConnectionString;
        var surrogateLogin = "ZsecSec02Surrogate_" + Guid.NewGuid().ToString("N")[..12];
        var deniedLogin = "ZsecSec02Denied_" + Guid.NewGuid().ToString("N")[..12];
        var deniedLoginCreated = false;
        var runtimeDirectPermissions = await QueryStringsAsync(serverConnection,
            "SELECT p.permission_name FROM sys.server_permissions p JOIN sys.server_principals s ON s.principal_id=p.grantee_principal_id WHERE s.name=@login AND p.state IN ('G','W') AND p.permission_name NOT IN (N'CONNECT SQL',N'VIEW ANY DATABASE');",
            token, new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = runtimeLogin });
        var runtimeServerRoles = await QueryStringsAsync(serverConnection,
            "SELECT r.name FROM sys.server_role_members m JOIN sys.server_principals r ON r.principal_id=m.role_principal_id JOIN sys.server_principals l ON l.principal_id=m.member_principal_id WHERE l.name=@login;",
            token, new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = runtimeLogin });
        Assert.DoesNotContain(RestoreRoleName, runtimeServerRoles, StringComparer.OrdinalIgnoreCase);
        foreach (var permission in runtimeDirectPermissions)
            await ExecuteAsync(serverConnection, $"REVOKE {permission} FROM {Bracket(runtimeLogin)};", token);
        var runtimeViewGrantWasPresent = await ScalarAsync<int>(serverConnection,
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.server_permissions p JOIN sys.server_principals s ON s.principal_id=p.grantee_principal_id WHERE s.name=@login AND p.permission_name=N'VIEW ANY DATABASE' AND p.state IN ('G','W')) THEN 1 ELSE 0 END;",
            token, new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = runtimeLogin }) == 1;

        await Zc04.CreateDatabaseAsync(serverConnection, databaseName);
        try
        {
            // Keep the deployment identity distinct from dbo so the committed
            // script can create its explicit database user as intended.
            await ExecuteAsync(serverConnection, $"ALTER AUTHORIZATION ON DATABASE::{Bracket(databaseName)} TO [sa];", token);
            await Zc04.MigrateAsync(databaseConnection);

            var provisioning = ReadSql("database", "scripts", "008_pilot_least_privilege_idempotent.sql");
            provisioning = Substitute(provisioning, runtimeLogin, deploymentLogin, databaseName);
            await ExecuteAsync(serverConnection, provisioning, token);
            await AssertVerifierPassAsync(serverConnection, runtimeLogin, deploymentLogin, databaseName, token);

            // Existing server objects and database memberships must be safe to replay.
            await ExecuteAsync(serverConnection, provisioning, token);
            await AssertVerifierPassAsync(serverConnection, runtimeLogin, deploymentLogin, databaseName, token);

            // Model the old partial state: retain the server principals, remove only
            // the database-side runtime user/role, and prove convergence on replay.
            await ExecuteAsync(databaseConnection, $"ALTER ROLE [db_datareader] DROP MEMBER {Bracket(runtimeLogin)}; ALTER ROLE [db_datawriter] DROP MEMBER {Bracket(runtimeLogin)}; ALTER ROLE {Bracket(RuntimeRoleName)} DROP MEMBER {Bracket(runtimeLogin)}; DROP ROLE {Bracket(RuntimeRoleName)}; DROP USER {Bracket(runtimeLogin)};", token);
            await ExecuteAsync(serverConnection, provisioning, token);
            await AssertVerifierPassAsync(serverConnection, runtimeLogin, deploymentLogin, databaseName, token);

            await ExecuteAsync(databaseConnection, "IF SCHEMA_ID(N'reporting') IS NULL EXEC(N'CREATE SCHEMA reporting;'); CREATE TABLE dbo.ZsecSec02Probe(Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ZsecSec02Probe PRIMARY KEY, Value nvarchar(100) NOT NULL);", token);
            await ExecuteAsync(databaseConnection, "CREATE VIEW reporting.ZsecSec02ProbeView AS SELECT Id, Value FROM dbo.ZsecSec02Probe;", token);

            var surrogatePassword = GenerateSecret();
            await ExecuteAsync(serverConnection, $"CREATE LOGIN {Bracket(surrogateLogin)} WITH PASSWORD={Literal(surrogatePassword)}, CHECK_POLICY=OFF, CHECK_EXPIRATION=OFF; ALTER SERVER ROLE [PharmacyAppRestoreTest] ADD MEMBER {Bracket(surrogateLogin)};", token);
            await ExecuteAsync(databaseConnection, $"CREATE USER {Bracket(surrogateLogin)} FOR LOGIN {Bracket(surrogateLogin)}; ALTER ROLE [db_datareader] ADD MEMBER {Bracket(surrogateLogin)}; ALTER ROLE [db_datawriter] ADD MEMBER {Bracket(surrogateLogin)}; ALTER ROLE [PharmacyAppRuntime] ADD MEMBER {Bracket(surrogateLogin)};", token);

            Assert.Equal(0, await ScalarAsync<int>(serverConnection, "SELECT IS_SRVROLEMEMBER(N'sysadmin', @login);", token, new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = surrogateLogin }));
            Assert.Equal(0, await ScalarAsync<int>(serverConnection, "SELECT IS_SRVROLEMEMBER(N'dbcreator', @login);", token, new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = surrogateLogin }));
            Assert.Equal(1, await ScalarAsync<int>(serverConnection, "SELECT IS_SRVROLEMEMBER(N'PharmacyAppRestoreTest', @login);", token, new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = surrogateLogin }));
            Assert.Equal(1, await ScalarAsync<int>(serverConnection, "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.server_permissions p JOIN sys.server_principals s ON s.principal_id=p.grantee_principal_id WHERE s.name=@login AND p.permission_name=N'CONNECT SQL' AND p.state IN ('G','W')) THEN 1 ELSE 0 END;", token, new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = surrogateLogin }));
            Assert.Equal(0, await ScalarAsync<int>(databaseConnection, "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.database_role_members m JOIN sys.database_principals r ON r.principal_id=m.role_principal_id WHERE m.member_principal_id=USER_ID(@login) AND r.name=N'db_owner') THEN 1 ELSE 0 END;", token, new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = surrogateLogin }));

            await using (var serverSession = new SqlConnection(serverConnection))
            {
                await serverSession.OpenAsync(token);
                await ExecuteAsync(serverSession, $"EXECUTE AS LOGIN={Literal(surrogateLogin)};", token);
                try
                {
                    Assert.Equal(1, await ScalarAsync<int>(serverSession, "SELECT CASE WHEN EXISTS (SELECT 1 FROM fn_my_permissions(NULL,N'SERVER') WHERE permission_name=N'CREATE ANY DATABASE') THEN 1 ELSE 0 END;", token));
                    await ExecuteAsync(serverSession, $"BACKUP DATABASE {Bracket(databaseName)} TO DISK={Literal(backupPath)} WITH COPY_ONLY, CHECKSUM;", token);
                    Assert.True(File.Exists(backupPath));

                    var logicalFiles = await FileListAsync(serverSession, backupPath, token);
                    var dataPath = await ScalarAsync<string>(serverSession, "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultDataPath'));", token);
                    var logPath = await ScalarAsync<string>(serverSession, "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultLogPath'));", token);
                    var dataFile = EnsureTrailingSlash(dataPath) + restoreDatabaseName + ".mdf";
                    var logFile = EnsureTrailingSlash(logPath) + restoreDatabaseName + "_log.ldf";
                    await ExecuteAsync(serverSession, $"RESTORE DATABASE {Bracket(restoreDatabaseName)} FROM DISK={Literal(backupPath)} WITH RECOVERY, CHECKSUM, MOVE {Literal(logicalFiles.Data)} TO {Literal(dataFile)}, MOVE {Literal(logicalFiles.Log)} TO {Literal(logFile)};", token);

                    await AssertDeniedAsync(serverSession, "RESTORE WITH REPLACE existing", $"RESTORE DATABASE {Bracket(databaseName)} FROM DISK={Literal(backupPath)} WITH REPLACE;", token);
                    await AssertDeniedAsync(serverSession, "ALTER SERVER ROLE", $"ALTER SERVER ROLE [dbcreator] ADD MEMBER {Bracket(surrogateLogin)};", token);
                    await AssertDeniedAsync(serverSession, "ALTER LOGIN", $"ALTER LOGIN {Bracket(surrogateLogin)} WITH NAME={Bracket(deniedLogin)};", token);
                    await AssertDeniedAsync(serverSession, "CREATE LOGIN", $"CREATE LOGIN {Bracket(deniedLogin)} WITH PASSWORD={Literal(GenerateSecret())}, CHECK_POLICY=OFF;", token, () => deniedLoginCreated = true);
                    await AssertDeniedAsync(serverSession, "self-sysadmin", $"ALTER SERVER ROLE [sysadmin] ADD MEMBER {Bracket(surrogateLogin)};", token);
                }
                finally
                {
                    await ExecuteAsync(serverSession, "REVERT;", token);
                }
            }

            await using (var dbSession = new SqlConnection(databaseConnection))
            {
                await dbSession.OpenAsync(token);
                await ExecuteAsync(dbSession, $"EXECUTE AS LOGIN={Literal(surrogateLogin)}; USE {Bracket(databaseName)};", token);
                try
                {
                    Assert.Equal(0, await ScalarAsync<int>(dbSession, "SELECT IS_MEMBER(N'db_owner');", token));
                    await ExecuteAsync(dbSession, "SELECT TOP 1 Id, Value FROM reporting.ZsecSec02ProbeView; INSERT dbo.ZsecSec02Probe(Value) VALUES(N'insert'); UPDATE dbo.ZsecSec02Probe SET Value=N'update' WHERE Value=N'insert'; DELETE dbo.ZsecSec02Probe WHERE Value=N'update';", token);
                    await AssertDeniedAsync(dbSession, "CREATE TABLE", "CREATE TABLE dbo.ZsecSec02Denied(Id int NOT NULL);", token);
                    await AssertDeniedAsync(dbSession, "ALTER TABLE", "ALTER TABLE dbo.ZsecSec02Probe ADD DeniedColumn int NULL;", token);
                    await AssertDeniedAsync(dbSession, "DROP TABLE", "DROP TABLE dbo.ZsecSec02Probe;", token);
                }
                finally
                {
                    await ExecuteAsync(dbSession, "REVERT;", token);
                }
            }

            Assert.Equal(1, await ScalarAsync<int>(serverConnection, "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.server_permissions p JOIN sys.server_principals s ON s.principal_id=p.grantee_principal_id WHERE s.name=@login AND p.permission_name=N'CONNECT SQL' AND p.state IN ('G','W')) THEN 1 ELSE 0 END;", token, new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = runtimeLogin }));
            Assert.Equal(0, await ScalarAsync<int>(serverConnection, "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.server_permissions p JOIN sys.server_principals s ON s.principal_id=p.grantee_principal_id WHERE s.name=@login AND p.permission_name NOT IN (N'CONNECT SQL',N'VIEW ANY DATABASE') AND p.state IN ('G','W')) THEN 1 ELSE 0 END;", token, new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = runtimeLogin }));
            Assert.Equal(1, await ScalarAsync<int>(serverConnection, "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.server_permissions p JOIN sys.server_principals s ON s.principal_id=p.grantee_principal_id WHERE s.name=@login AND p.permission_name=N'VIEW ANY DATABASE' AND p.state IN ('G','W')) THEN 1 ELSE 0 END;", token, new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = runtimeLogin }));
            Assert.Equal(0, await ScalarAsync<int>(serverConnection, "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.server_permissions p JOIN sys.server_principals s ON s.principal_id=p.grantee_principal_id WHERE s.name=@login AND p.permission_name=N'CONTROL SERVER' AND p.state IN ('G','W')) THEN 1 ELSE 0 END;", token, new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = runtimeLogin }));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await SafeExecuteAsync(serverConnection, $"IF DB_ID({Literal(restoreDatabaseName)}) IS NOT NULL BEGIN ALTER DATABASE {Bracket(restoreDatabaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {Bracket(restoreDatabaseName)}; END;", token);
            await SafeExecuteAsync(serverConnection, $"IF DB_ID({Literal(databaseName)}) IS NOT NULL BEGIN ALTER DATABASE {Bracket(databaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {Bracket(databaseName)}; END;", token);
            await SafeExecuteAsync(serverConnection, $"IF SUSER_ID({Literal(surrogateLogin)}) IS NOT NULL AND EXISTS (SELECT 1 FROM sys.server_role_members m WHERE m.member_principal_id=SUSER_ID({Literal(surrogateLogin)}) AND m.role_principal_id=(SELECT principal_id FROM sys.server_principals WHERE name=N'{RestoreRoleName}')) ALTER SERVER ROLE [{RestoreRoleName}] DROP MEMBER {Bracket(surrogateLogin)};", token);
            await SafeExecuteAsync(serverConnection, $"IF SUSER_ID({Literal(surrogateLogin)}) IS NOT NULL DROP LOGIN {Bracket(surrogateLogin)};", token);
            if (deniedLoginCreated)
                await SafeExecuteAsync(serverConnection, $"IF SUSER_ID({Literal(deniedLogin)}) IS NOT NULL DROP LOGIN {Bracket(deniedLogin)};", token);
            if (!restoreRoleWasPresent)
            {
                await SafeExecuteAsync(serverConnection, $"IF SUSER_ID({Literal(runtimeLogin)}) IS NOT NULL AND EXISTS (SELECT 1 FROM sys.server_role_members WHERE member_principal_id=SUSER_ID({Literal(runtimeLogin)}) AND role_principal_id=(SELECT principal_id FROM sys.server_principals WHERE name=N'{RestoreRoleName}')) ALTER SERVER ROLE [{RestoreRoleName}] DROP MEMBER {Bracket(runtimeLogin)};", token);
                await SafeExecuteAsync(serverConnection, $"IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'{RestoreRoleName}' AND type=N'R') DROP SERVER ROLE [{RestoreRoleName}];", token);
            }
            if (!runtimeViewGrantWasPresent)
                await SafeExecuteAsync(serverConnection, $"REVOKE VIEW ANY DATABASE FROM {Bracket(runtimeLogin)};", token);
            foreach (var role in runtimeServerRoles)
                await SafeExecuteAsync(serverConnection, $"IF SUSER_ID({Literal(runtimeLogin)}) IS NOT NULL AND EXISTS (SELECT 1 FROM sys.server_principals WHERE name={Literal(role)} AND type=N'R') ALTER SERVER ROLE {Bracket(role)} ADD MEMBER {Bracket(runtimeLogin)};", token);
            foreach (var permission in runtimeDirectPermissions)
                await SafeExecuteAsync(serverConnection, $"GRANT {permission} TO {Bracket(runtimeLogin)};", token);
            if (!deploymentWasDbcreator)
                await SafeExecuteAsync(serverConnection, $"IF IS_SRVROLEMEMBER(N'dbcreator')=1 ALTER SERVER ROLE [dbcreator] DROP MEMBER {Bracket(deploymentLogin)};", token);
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }

        Assert.False(File.Exists(backupPath));
        Assert.Equal(0, await ScalarAsync<int>(serverConnection, "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.server_principals WHERE name=@login) OR EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'PharmacyAppRestoreTest') OR DB_ID(@databaseName) IS NOT NULL OR DB_ID(@restoreDatabaseName) IS NOT NULL THEN 1 ELSE 0 END;", token,
            new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = surrogateLogin },
            new SqlParameter("@databaseName", SqlDbType.NVarChar, 128) { Value = databaseName },
            new SqlParameter("@restoreDatabaseName", SqlDbType.NVarChar, 128) { Value = restoreDatabaseName }));
    }

    private static async Task AssertVerifierPassAsync(string serverConnection, string runtime, string deployment, string database, CancellationToken token)
    {
        var verifier = Substitute(ReadSql("database", "verification", "verify_pilot_least_privilege.sql"), runtime, deployment, database);
        await using var connection = new SqlConnection(serverConnection);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = verifier;
        await using var reader = await command.ExecuteReaderAsync(token);
        string? result = null;
        do
        {
            while (await reader.ReadAsync(token))
            {
                for (var i = 0; i < reader.FieldCount; i++)
                    if (string.Equals(reader.GetName(i), "Result", StringComparison.Ordinal)) result = Convert.ToString(reader.GetValue(i));
            }
        } while (await reader.NextResultAsync(token));
        Assert.Equal("PASS", result);
    }

    private static async Task AssertDeniedAsync(SqlConnection connection, string label, string sql, CancellationToken token, Action? succeeded = null)
    {
        try
        {
            await ExecuteAsync(connection, sql, token);
            succeeded?.Invoke();
            Assert.Fail($"{label} unexpectedly succeeded.");
        }
        catch (SqlException)
        {
        }
    }

    private static async Task<string> SelectNonAdminSqlServiceLoginAsync(string serverConnection, CancellationToken token) =>
        await ScalarAsync<string>(serverConnection,
            "SELECT TOP (1) name FROM sys.server_principals WHERE type IN ('U','G') AND name=N'NT SERVICE\\SQLTELEMETRY' AND ISNULL(IS_SRVROLEMEMBER(N'sysadmin',name),0)=0;",
            token) ?? throw new InvalidOperationException("No non-admin SQL Server service Windows login is available for the disposable provisioning proof.");

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql, CancellationToken token, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(token);
        return await ScalarAsync<T>(connection, sql, token, parameters);
    }

    private static async Task<T> ScalarAsync<T>(SqlConnection connection, string sql, CancellationToken token, params SqlParameter[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync(token) ?? throw new InvalidOperationException("Expected scalar SQL result was missing.");
        return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<string>> QueryStringsAsync(string connectionString, string sql, CancellationToken token, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        await using var reader = await command.ExecuteReaderAsync(token);
        var values = new List<string>();
        while (await reader.ReadAsync(token)) values.Add(reader.GetString(0));
        return values;
    }

    private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken token)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(token);
        await ExecuteAsync(connection, sql, token);
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 300;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task SafeExecuteAsync(string connectionString, string sql, CancellationToken token)
    {
        try { await ExecuteAsync(connectionString, sql, token); } catch { }
    }

    private static async Task<(string Data, string Log)> FileListAsync(SqlConnection connection, string path, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "RESTORE FILELISTONLY FROM DISK=@path;";
        command.Parameters.Add(new SqlParameter("@path", SqlDbType.NVarChar, 4000) { Value = path });
        await using var reader = await command.ExecuteReaderAsync(token);
        string? data = null;
        string? log = null;
        while (await reader.ReadAsync(token))
        {
            var logical = reader.GetString(reader.GetOrdinal("LogicalName"));
            if (reader.GetString(reader.GetOrdinal("Type")) == "D") data = logical;
            if (reader.GetString(reader.GetOrdinal("Type")) == "L") log = logical;
        }
        return (data ?? throw new InvalidOperationException("Backup data logical name missing."), log ?? throw new InvalidOperationException("Backup log logical name missing."));
    }

    private static string Substitute(string script, string runtime, string deployment, string database) => script
        .Replace("$(RuntimeLoginName)", runtime, StringComparison.Ordinal)
        .Replace("$(DeploymentLoginName)", deployment, StringComparison.Ordinal)
        .Replace("$(PharmacyDatabaseName)", database, StringComparison.Ordinal)
        .Replace("$(RuntimeRoleName)", RuntimeRoleName, StringComparison.Ordinal);

    private static string ReadSql(params string[] parts) => File.ReadAllText(parts.Aggregate(FindRepositoryRoot(), Path.Combine));

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Pharmacy.slnx"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static string GenerateSecret()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return "Zs9!" + Convert.ToBase64String(bytes).Replace("+", "A", StringComparison.Ordinal).Replace("/", "B", StringComparison.Ordinal).Replace("=", "C", StringComparison.Ordinal) + "aA1!";
    }

    private static string Literal(string value) => "N'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    private static string Bracket(string value) => "[" + value.Replace("]", "]]", StringComparison.Ordinal) + "]";
    private static string EnsureTrailingSlash(string value) => value.EndsWith("\\", StringComparison.Ordinal) ? value : value + "\\";
}
