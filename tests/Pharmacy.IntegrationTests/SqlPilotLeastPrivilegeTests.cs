using Xunit;

namespace Pharmacy.IntegrationTests;

/// <summary>
/// Contract tests for the pilot SQL permission boundary.  The provisioning and
/// verifier scripts are executed against the real pilot/disposable instance by
/// the deployment runbook; these tests keep the checked-in contract from
/// silently drifting between releases.
/// </summary>
public sealed class SqlPilotLeastPrivilegeTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ProvisioningIsIntegratedWindowsOnlyAndIdempotent()
    {
        var script = Read("database", "scripts", "008_pilot_least_privilege_idempotent.sql");

        Assert.Contains("RuntimeLoginName, DeploymentLoginName", script, StringComparison.Ordinal);
        Assert.Contains("$(RuntimeLoginName)", script, StringComparison.Ordinal);
        Assert.Contains("CREATE LOGIN ", script, StringComparison.Ordinal);
        Assert.Equal(2, Count(script, "FROM WINDOWS"));
        Assert.DoesNotContain("WITH PASSWORD", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE LOGIN [$(RuntimeLoginName)]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER SERVER ROLE [sysadmin] ADD MEMBER", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Re-running the file is safe", script, StringComparison.Ordinal);
        Assert.Contains("IF NOT EXISTS", script, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeContractCoversDmlReportingBackupAndGuardedRestoreWithoutDdl()
    {
        var script = Read("database", "scripts", "008_pilot_least_privilege_idempotent.sql");
        var verifier = Read("database", "verification", "verify_pilot_least_privilege.sql");

        Assert.Contains("db_datareader", script, StringComparison.Ordinal);
        Assert.Contains("db_datawriter", script, StringComparison.Ordinal);
        Assert.Contains("GRANT BACKUP DATABASE", script, StringComparison.Ordinal);
        Assert.Contains("GRANT BACKUP LOG", script, StringComparison.Ordinal);
        Assert.Contains("PharmacyAppRestoreTest", script, StringComparison.Ordinal);
        Assert.Contains("GRANT CREATE ANY DATABASE TO [PharmacyAppRestoreTest]", script, StringComparison.Ordinal);
        Assert.Contains("unexpected server permission", script, StringComparison.Ordinal);
        Assert.Contains("NOT IN (N'CONNECT SQL', N'VIEW ANY DATABASE')", script, StringComparison.Ordinal);
        Assert.Contains("ALTER SERVER ROLE [dbcreator] ADD MEMBER", script, StringComparison.Ordinal);
        Assert.Contains("must be neither sysadmin nor dbcreator", script, StringComparison.Ordinal);
        Assert.Contains("Runtime user is db_owner", verifier, StringComparison.Ordinal);
        Assert.Contains("PharmacyAppRuntime has a DDL or ownership grant", verifier, StringComparison.Ordinal);
        Assert.Contains("Deployment user lacks db_owner", verifier, StringComparison.Ordinal);
        Assert.DoesNotContain("RESTORE DATABASE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER ROLE [db_owner] ADD MEMBER [$(Runtime", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadOnlyVerifierProvesRoleMembershipAndDeploymentBoundary()
    {
        var verifier = Read("database", "verification", "verify_pilot_least_privilege.sql");

        Assert.Contains("IS_SRVROLEMEMBER(N'sysadmin', @RuntimeLoginName) <> 0", verifier, StringComparison.Ordinal);
        Assert.Contains("IS_SRVROLEMEMBER(N'dbcreator', @RuntimeLoginName) <> 0", verifier, StringComparison.Ordinal);
        Assert.Contains("PharmacyAppRuntime is not a database role", verifier, StringComparison.Ordinal);
        Assert.Contains("Runtime user is not a member of PharmacyAppRuntime", verifier, StringComparison.Ordinal);
        Assert.Contains("Runtime role lacks BACKUP DATABASE", verifier, StringComparison.Ordinal);
        Assert.Contains("Runtime role lacks BACKUP LOG", verifier, StringComparison.Ordinal);
        Assert.Contains("Runtime identity must not be dbcreator", verifier, StringComparison.Ordinal);
        Assert.Contains("CREATE ANY DATABASE", verifier, StringComparison.Ordinal);
        Assert.Contains("unexpected direct server permission", verifier, StringComparison.Ordinal);
        Assert.Contains("NOT IN (N'CONNECT SQL', N'VIEW ANY DATABASE')", verifier, StringComparison.Ordinal);
        Assert.Contains("Deployment user lacks db_owner on Pharmacy", verifier, StringComparison.Ordinal);
        Assert.Contains("N''PASS'' AS Result", verifier, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE LOGIN", verifier, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER ROLE", verifier, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP ", verifier, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExistingRuntimeAndDeploymentFlowsMatchThePermissionBoundary()
    {
        var runtime = Read("src", "Pharmacy.Infrastructure", "Operations", "SqlBackupRecoveryService.cs");
        var deployment = Read("src", "Pharmacy.Infrastructure", "Deployment", "PilotDeploymentManager.cs");

        Assert.Contains("Pharmacy_RestoreTest_", runtime, StringComparison.Ordinal);
        Assert.Contains("RESTORE DATABASE [{tempName}]", runtime, StringComparison.Ordinal);
        Assert.Contains("DROP DATABASE", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("WITH REPLACE", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MigrateAsync", deployment, StringComparison.Ordinal);
        Assert.Contains("RESTORE DATABASE", deployment, StringComparison.Ordinal);
        Assert.Contains("WITH REPLACE", deployment, StringComparison.Ordinal);
    }

    [Fact]
    public void PilotDocumentationUsesSeparateIdentitiesAndNoSecrets()
    {
        var sqlExpress = Read("docs", "deployment", "SQL_EXPRESS_PILOT_V1.md");
        var installation = Read("docs", "deployment", "INSTALLATION_PILOT_V1.md");
        var backup = Read("docs", "operations", "BACKUP_RESTORE_V0.1.md");
        var all = sqlExpress + installation + backup;

        Assert.Contains("RuntimeLoginName", all, StringComparison.Ordinal);
        Assert.Contains("DeploymentLoginName", all, StringComparison.Ordinal);
        Assert.Contains("PharmacyAppRuntime", all, StringComparison.Ordinal);
        Assert.Contains("008_pilot_least_privilege_idempotent.sql", all, StringComparison.Ordinal);
        Assert.Contains("verify_pilot_least_privilege.sql", all, StringComparison.Ordinal);
        Assert.Contains("13 migrations", all, StringComparison.Ordinal);
        Assert.Contains("aucun login SQL", sqlExpress, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ne contient ni login, ni mot de passe, ni secret", installation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ne peut donc", backup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("base live", backup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ajouter le compte pilote comme administrateur SQL", sqlExpress, StringComparison.OrdinalIgnoreCase);
    }

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static string Read(params string[] parts) =>
        File.ReadAllText(parts.Aggregate(RepositoryRoot, Path.Combine));

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Pharmacy.slnx"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Racine du dépôt introuvable.");
    }
}
