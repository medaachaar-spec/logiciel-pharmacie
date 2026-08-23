/* Read-only verification for 008_pilot_least_privilege_idempotent.sql.
   Run in SQLCMD mode with the same three identity variables. */
-- Required SQLCMD variables: RuntimeLoginName, DeploymentLoginName,
-- PharmacyDatabaseName and RuntimeRoleName.

SET NOCOUNT ON;

DECLARE @RuntimeLoginName sysname = NULLIF(LTRIM(RTRIM(N'$(RuntimeLoginName)')), N'');
DECLARE @DeploymentLoginName sysname = NULLIF(LTRIM(RTRIM(N'$(DeploymentLoginName)')), N'');
DECLARE @DatabaseName sysname = NULLIF(LTRIM(RTRIM(N'$(PharmacyDatabaseName)')), N'');
DECLARE @RuntimeRoleName sysname = NULLIF(LTRIM(RTRIM(N'$(RuntimeRoleName)')), N'');
IF @RuntimeLoginName IS NULL OR @DeploymentLoginName IS NULL OR @DatabaseName IS NULL OR @RuntimeRoleName IS NULL
    THROW 51100, N'All identity and database variables are required.', 1;
IF DB_ID(@DatabaseName) IS NULL
    THROW 51101, N'The target database does not exist.', 1;
IF @RuntimeLoginName = @DeploymentLoginName
    THROW 51102, N'Runtime and deployment identities must be different.', 1;
IF SUSER_ID(@RuntimeLoginName) IS NULL OR NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE principal_id=SUSER_ID(@RuntimeLoginName) AND type IN ('U','G'))
    THROW 51103, N'Runtime Windows login is missing.', 1;
IF SUSER_ID(@DeploymentLoginName) IS NULL OR NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE principal_id=SUSER_ID(@DeploymentLoginName) AND type IN ('U','G'))
    THROW 51104, N'Deployment Windows login is missing.', 1;
IF IS_SRVROLEMEMBER(N'sysadmin', @RuntimeLoginName) <> 0
    THROW 51105, N'Runtime identity is sysadmin.', 1;
IF IS_SRVROLEMEMBER(N'dbcreator', @RuntimeLoginName) <> 0
    THROW 51106, N'Runtime identity must not be dbcreator because dbcreator can restore an existing database.', 1;
IF IS_SRVROLEMEMBER(N'PharmacyAppRestoreTest', @RuntimeLoginName) <> 1
    THROW 51120, N'Runtime identity lacks the guarded restore-test server role.', 1;
IF NOT EXISTS
(
    SELECT 1
    FROM sys.server_permissions
    WHERE grantee_principal_id = SUSER_ID(N'PharmacyAppRestoreTest')
      AND permission_name = N'CREATE ANY DATABASE'
      AND state IN ('G','W')
)
    THROW 51121, N'PharmacyAppRestoreTest lacks CREATE ANY DATABASE.', 1;
IF EXISTS
(
    SELECT 1 FROM sys.server_permissions
    WHERE grantee_principal_id = SUSER_ID(N'PharmacyAppRestoreTest')
      AND state IN ('G','W')
      AND permission_name <> N'CREATE ANY DATABASE'
)
    THROW 51122, N'PharmacyAppRestoreTest has an unexpected server permission.', 1;
IF EXISTS
(
    SELECT 1 FROM sys.server_permissions
    WHERE grantee_principal_id = SUSER_ID(@RuntimeLoginName)
      AND state IN ('G','W')
      AND permission_name <> N'VIEW ANY DATABASE'
)
    THROW 51123, N'Runtime login has an unexpected direct server permission.', 1;
IF IS_SRVROLEMEMBER(N'dbcreator', @DeploymentLoginName) <> 1
    THROW 51107, N'Deployment identity lacks dbcreator.', 1;
IF NOT EXISTS
(
    SELECT 1 FROM sys.server_permissions
    WHERE grantee_principal_id = SUSER_ID(@RuntimeLoginName)
      AND permission_name = N'VIEW ANY DATABASE'
      AND state IN ('G','W')
)
    THROW 51108, N'Runtime identity lacks VIEW ANY DATABASE.', 1;

DECLARE @DatabaseBatch nvarchar(max) = N'
USE ' + QUOTENAME(@DatabaseName) + N';
DECLARE @runtimeUserId int = USER_ID(@runtimeLoginName);
DECLARE @runtimeRoleId int = USER_ID(@runtimeRoleName);
DECLARE @deploymentUserId int = USER_ID(@deploymentLoginName);
IF @runtimeUserId IS NULL OR @runtimeRoleId IS NULL OR @deploymentUserId IS NULL
    THROW 51109, N''One or more Pharmacy database users/roles are missing.'', 1;
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE principal_id=@runtimeUserId AND type IN (''U'',''G'') AND sid=SUSER_SID(@runtimeLoginName))
    THROW 51110, N''Runtime database user is not mapped to the Windows login.'', 1;
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE principal_id=@runtimeRoleId AND type=''R'')
    THROW 51111, N''PharmacyAppRuntime is not a database role.'', 1;
IF NOT EXISTS (SELECT 1 FROM sys.database_role_members WHERE role_principal_id=@runtimeRoleId AND member_principal_id=@runtimeUserId)
    THROW 51112, N''Runtime user is not a member of PharmacyAppRuntime.'', 1;
IF EXISTS
(
    SELECT 1 FROM sys.database_role_members AS membership
    JOIN sys.database_principals AS rolePrincipal ON rolePrincipal.principal_id=membership.role_principal_id
    WHERE membership.member_principal_id=@runtimeUserId
      AND rolePrincipal.name NOT IN (N''db_datareader'',N''db_datawriter'',@runtimeRoleName)
)
    THROW 51113, N''Runtime user has an unexpected database role membership.'', 1;
IF EXISTS
(
    SELECT 1 FROM sys.database_role_members AS membership
    JOIN sys.database_principals AS rolePrincipal ON rolePrincipal.principal_id=membership.role_principal_id
    WHERE membership.member_principal_id=@runtimeRoleId
      AND rolePrincipal.name IN (N''db_owner'',N''db_ddladmin'',N''db_securityadmin'')
)
    THROW 51114, N''PharmacyAppRuntime is nested in an elevated database role.'', 1;
IF EXISTS
(
    SELECT 1 FROM sys.database_permissions
    WHERE grantee_principal_id=@runtimeRoleId
      AND state IN (''G'',''W'')
      AND permission_name IN (N''CONTROL'',N''ALTER'',N''ALTER ANY SCHEMA'',N''ALTER ANY USER'',N''ALTER ANY ROLE'',N''TAKE OWNERSHIP'',N''IMPERSONATE'')
)
    THROW 51115, N''PharmacyAppRuntime has a DDL or ownership grant.'', 1;
IF NOT EXISTS (SELECT 1 FROM sys.database_permissions WHERE grantee_principal_id=@runtimeRoleId AND permission_name=N''BACKUP DATABASE'' AND state IN (''G'',''W''))
    THROW 51116, N''Runtime role lacks BACKUP DATABASE.'', 1;
IF NOT EXISTS (SELECT 1 FROM sys.database_permissions WHERE grantee_principal_id=@runtimeRoleId AND permission_name=N''BACKUP LOG'' AND state IN (''G'',''W''))
    THROW 51117, N''Runtime role lacks BACKUP LOG.'', 1;
IF EXISTS (SELECT 1 FROM sys.database_role_members WHERE role_principal_id=DATABASE_PRINCIPAL_ID(N''db_owner'') AND member_principal_id=@runtimeUserId)
    THROW 51118, N''Runtime user is db_owner.'', 1;
IF NOT EXISTS (SELECT 1 FROM sys.database_role_members WHERE role_principal_id=DATABASE_PRINCIPAL_ID(N''db_owner'') AND member_principal_id=@deploymentUserId)
    THROW 51119, N''Deployment user lacks db_owner on Pharmacy.'', 1;
SELECT
    DB_NAME() AS DatabaseName,
    @runtimeLoginName AS RuntimeLoginName,
    @runtimeRoleName AS RuntimeRoleName,
    N''PASS'' AS Result,
    N''DML/reporting via db_datareader/db_datawriter; BACKUP DATABASE/LOG; guarded restore-test via CREATE ANY DATABASE only; no runtime DDL/db_owner/live restore.'' AS RuntimeBoundary,
    @deploymentLoginName AS DeploymentLoginName,
    N''dbcreator + db_owner for install/migrate/rollback/live restore only'' AS DeploymentBoundary;
';
EXEC sys.sp_executesql
    @DatabaseBatch,
    N'@runtimeLoginName sysname, @deploymentLoginName sysname, @runtimeRoleName sysname',
    @runtimeLoginName=@RuntimeLoginName,
    @deploymentLoginName=@DeploymentLoginName,
    @runtimeRoleName=@RuntimeRoleName;
