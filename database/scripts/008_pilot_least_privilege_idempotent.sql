/*
   Pilot V1 SQL least-privilege provisioning.

   Run this file in SQLCMD mode as an elevated deployment identity, after the
   Pharmacy database exists and before the first pilot launch.  The values are
   Windows users or groups; SQL authentication and passwords are intentionally
   not supported.

   Example:
     sqlcmd -S .\PHARMACY -E -d master -b -i 008_pilot_least_privilege_idempotent.sql `
       -v RuntimeLoginName="CONTOSO\PharmacyStaff" `
          DeploymentLoginName="CONTOSO\PharmacyInstall" `
          PharmacyDatabaseName="Pharmacy" `
          RuntimeRoleName="PharmacyAppRuntime"

   Re-running the file is safe. It converges existing principals to the
   intended memberships and never grants sysadmin, dbcreator or db_owner to
   runtime. The custom server role grants CREATE ANY DATABASE only; unlike
   dbcreator, it cannot restore over an existing live database.
*/
-- Required SQLCMD variables: RuntimeLoginName, DeploymentLoginName,
-- PharmacyDatabaseName and RuntimeRoleName.

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @RuntimeLoginName sysname = NULLIF(LTRIM(RTRIM(N'$(RuntimeLoginName)')), N'');
DECLARE @DeploymentLoginName sysname = NULLIF(LTRIM(RTRIM(N'$(DeploymentLoginName)')), N'');
DECLARE @DatabaseName sysname = NULLIF(LTRIM(RTRIM(N'$(PharmacyDatabaseName)')), N'');
DECLARE @RuntimeRoleName sysname = NULLIF(LTRIM(RTRIM(N'$(RuntimeRoleName)')), N'');

IF @RuntimeLoginName IS NULL OR @DeploymentLoginName IS NULL OR @DatabaseName IS NULL OR @RuntimeRoleName IS NULL
    THROW 51000, N'RuntimeLoginName, DeploymentLoginName, PharmacyDatabaseName and RuntimeRoleName are required.', 1;

IF @RuntimeLoginName = @DeploymentLoginName
    THROW 51001, N'Runtime and deployment identities must be different Windows principals.', 1;

IF LEN(@RuntimeLoginName) > 128 OR LEN(@DeploymentLoginName) > 128 OR LEN(@DatabaseName) > 128 OR LEN(@RuntimeRoleName) > 128
    THROW 51002, N'A principal or database name exceeds the SQL Server identifier limit.', 1;

IF @RuntimeLoginName LIKE N'%]%' OR @RuntimeLoginName LIKE N'%''%' OR
   @DeploymentLoginName LIKE N'%]%' OR @DeploymentLoginName LIKE N'%''%' OR
   @DatabaseName LIKE N'%]%' OR @DatabaseName LIKE N'%''%' OR
   @RuntimeRoleName LIKE N'%]%' OR @RuntimeRoleName LIKE N'%''%'
    THROW 51003, N'Names must be valid Windows/SQL identifiers and may not contain ] or apostrophe.', 1;

IF @DatabaseName LIKE N'%[^A-Za-z0-9_]%' OR @DatabaseName LIKE N'[0-9]%' OR
   @RuntimeRoleName LIKE N'%[^A-Za-z0-9_]%' OR @RuntimeRoleName LIKE N'[0-9]%'
    THROW 51004, N'DatabaseName and RuntimeRoleName must be simple SQL identifiers.', 1;

IF DB_ID(@DatabaseName) IS NULL
    THROW 51005, N'The target Pharmacy database must exist before provisioning.', 1;

DECLARE @RuntimeLoginId int = SUSER_ID(@RuntimeLoginName);
DECLARE @DynamicSql nvarchar(max);
IF @RuntimeLoginId IS NULL
BEGIN
    SET @DynamicSql = N'CREATE LOGIN ' + QUOTENAME(@RuntimeLoginName) + N' FROM WINDOWS;';
    EXEC sys.sp_executesql @DynamicSql;
    SET @RuntimeLoginId = SUSER_ID(@RuntimeLoginName);
END;
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE principal_id = @RuntimeLoginId AND type IN ('U', 'G'))
    THROW 51006, N'RuntimeLoginName must resolve to a Windows user or group login.', 1;

DECLARE @DeploymentLoginId int = SUSER_ID(@DeploymentLoginName);
IF @DeploymentLoginId IS NULL
BEGIN
    SET @DynamicSql = N'CREATE LOGIN ' + QUOTENAME(@DeploymentLoginName) + N' FROM WINDOWS;';
    EXEC sys.sp_executesql @DynamicSql;
    SET @DeploymentLoginId = SUSER_ID(@DeploymentLoginName);
END;
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE principal_id = @DeploymentLoginId AND type IN ('U', 'G'))
    THROW 51007, N'DeploymentLoginName must resolve to a Windows user or group login.', 1;

/* A custom role is used instead of dbcreator: dbcreator can restore any existing database. */
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'PharmacyAppRestoreTest')
    EXEC (N'CREATE SERVER ROLE [PharmacyAppRestoreTest];');
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'PharmacyAppRestoreTest' AND type = 'R')
    THROW 51012, N'PharmacyAppRestoreTest must be a server role.', 1;
GRANT CREATE ANY DATABASE TO [PharmacyAppRestoreTest];
IF EXISTS
(
    SELECT 1 FROM sys.server_permissions
    WHERE grantee_principal_id = SUSER_ID(N'PharmacyAppRestoreTest')
      AND state IN ('G','W')
      AND permission_name <> N'CREATE ANY DATABASE'
)
    THROW 51013, N'PharmacyAppRestoreTest has an unexpected server permission.', 1;
DECLARE @RestoreParentRole sysname;
DECLARE RestoreParentRoles CURSOR LOCAL FAST_FORWARD FOR
    SELECT rolePrincipal.name
    FROM sys.server_role_members AS membership
    JOIN sys.server_principals AS rolePrincipal ON rolePrincipal.principal_id = membership.role_principal_id
    WHERE membership.member_principal_id = SUSER_ID(N'PharmacyAppRestoreTest');
OPEN RestoreParentRoles;
FETCH NEXT FROM RestoreParentRoles INTO @RestoreParentRole;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @DynamicSql = N'ALTER SERVER ROLE ' + QUOTENAME(@RestoreParentRole) + N' DROP MEMBER [PharmacyAppRestoreTest];';
    EXEC sys.sp_executesql @DynamicSql;
    FETCH NEXT FROM RestoreParentRoles INTO @RestoreParentRole;
END;
CLOSE RestoreParentRoles;
DEALLOCATE RestoreParentRoles;

DECLARE @ServerRoleName sysname;
DECLARE ServerRoles CURSOR LOCAL FAST_FORWARD FOR
    SELECT rolePrincipal.name
    FROM sys.server_role_members AS membership
    JOIN sys.server_principals AS rolePrincipal ON rolePrincipal.principal_id = membership.role_principal_id
    WHERE membership.member_principal_id = @RuntimeLoginId
      AND rolePrincipal.name <> N'PharmacyAppRestoreTest';
OPEN ServerRoles;
FETCH NEXT FROM ServerRoles INTO @ServerRoleName;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @DynamicSql = N'ALTER SERVER ROLE ' + QUOTENAME(@ServerRoleName) + N' DROP MEMBER ' + QUOTENAME(@RuntimeLoginName) + N';';
    EXEC sys.sp_executesql @DynamicSql;
    FETCH NEXT FROM ServerRoles INTO @ServerRoleName;
END;
CLOSE ServerRoles;
DEALLOCATE ServerRoles;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.server_role_members AS membership
    JOIN sys.server_principals AS rolePrincipal ON rolePrincipal.principal_id = membership.role_principal_id
    WHERE membership.member_principal_id = @RuntimeLoginId AND rolePrincipal.name = N'PharmacyAppRestoreTest'
)
BEGIN
    SET @DynamicSql = N'ALTER SERVER ROLE [PharmacyAppRestoreTest] ADD MEMBER ' + QUOTENAME(@RuntimeLoginName) + N';';
    EXEC sys.sp_executesql @DynamicSql;
END;

SET @DynamicSql = N'GRANT VIEW ANY DATABASE TO ' + QUOTENAME(@RuntimeLoginName) + N';';
EXEC sys.sp_executesql @DynamicSql;
IF EXISTS
(
    SELECT 1 FROM sys.server_permissions
    WHERE grantee_principal_id = @RuntimeLoginId
      AND state IN ('G','W')
      AND permission_name NOT IN (N'CONNECT SQL', N'VIEW ANY DATABASE')
)
    THROW 51014, N'Runtime login has an unexpected direct server permission.', 1;

/* Deployment can create the database and run EF/rollback, but is not granted sysadmin here. */
IF NOT EXISTS
(
    SELECT 1
    FROM sys.server_role_members AS membership
    JOIN sys.server_principals AS rolePrincipal ON rolePrincipal.principal_id = membership.role_principal_id
    WHERE membership.member_principal_id = @DeploymentLoginId AND rolePrincipal.name = N'dbcreator'
)
BEGIN
    SET @DynamicSql = N'ALTER SERVER ROLE [dbcreator] ADD MEMBER ' + QUOTENAME(@DeploymentLoginName) + N';';
    EXEC sys.sp_executesql @DynamicSql;
END;

DECLARE @DatabaseBatch nvarchar(max) = N'
USE ' + QUOTENAME(@DatabaseName) + N';
DECLARE @runtimeUserId int = USER_ID(@runtimeLoginName);
DECLARE @deploymentUserId int = USER_ID(@deploymentLoginName);
DECLARE @runtimeRoleId int = USER_ID(@runtimeRoleName);
DECLARE @dynamicSql nvarchar(max);

IF @runtimeUserId IS NULL
BEGIN
    SET @dynamicSql = N''CREATE USER '' + QUOTENAME(@runtimeLoginName) + N'' FOR LOGIN '' + QUOTENAME(@runtimeLoginName) + N'';'';
    EXEC sys.sp_executesql @dynamicSql;
    SET @runtimeUserId = USER_ID(@runtimeLoginName);
END;
IF @runtimeUserId IS NULL OR NOT EXISTS
(
    SELECT 1 FROM sys.database_principals
    WHERE principal_id = @runtimeUserId AND type IN (''U'', ''G'') AND sid = SUSER_SID(@runtimeLoginName)
)
    THROW 51008, N''Runtime user is not a Windows user mapped to the requested login.'', 1;

IF @runtimeRoleId IS NULL
BEGIN
    SET @dynamicSql = N''CREATE ROLE '' + QUOTENAME(@runtimeRoleName) + N'';'';
    EXEC sys.sp_executesql @dynamicSql;
    SET @runtimeRoleId = USER_ID(@runtimeRoleName);
END;
IF @runtimeRoleId IS NULL OR EXISTS
(
    SELECT 1 FROM sys.database_principals
    WHERE principal_id = @runtimeRoleId AND type <> ''R''
)
    THROW 51009, N''RuntimeRoleName must identify a database role.'', 1;
IF EXISTS
(
    SELECT 1 FROM sys.database_permissions
    WHERE grantee_principal_id IN (@runtimeUserId, @runtimeRoleId)
      AND state IN (''G'',''W'')
      AND permission_name IN (N''CONTROL'',N''ALTER'',N''ALTER ANY SCHEMA'',N''ALTER ANY USER'',N''ALTER ANY ROLE'',N''TAKE OWNERSHIP'',N''IMPERSONATE'')
)
    THROW 51015, N''Runtime user or role already has a DDL or ownership grant.'', 1;

/* Remove every pre-existing role membership except the three intended roles. */
DECLARE @roleName sysname;
DECLARE RuntimeUserRoles CURSOR LOCAL FAST_FORWARD FOR
    SELECT rolePrincipal.name
    FROM sys.database_role_members AS membership
    JOIN sys.database_principals AS rolePrincipal ON rolePrincipal.principal_id = membership.role_principal_id
    WHERE membership.member_principal_id = @runtimeUserId
      AND rolePrincipal.name NOT IN (N''db_datareader'', N''db_datawriter'', @runtimeRoleName);
OPEN RuntimeUserRoles;
FETCH NEXT FROM RuntimeUserRoles INTO @roleName;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @dynamicSql = N''ALTER ROLE '' + QUOTENAME(@roleName) + N'' DROP MEMBER '' + QUOTENAME(@runtimeLoginName) + N'';'';
    EXEC sys.sp_executesql @dynamicSql;
    FETCH NEXT FROM RuntimeUserRoles INTO @roleName;
END;
CLOSE RuntimeUserRoles;
DEALLOCATE RuntimeUserRoles;

DECLARE RuntimeRoleParents CURSOR LOCAL FAST_FORWARD FOR
    SELECT rolePrincipal.name
    FROM sys.database_role_members AS membership
    JOIN sys.database_principals AS rolePrincipal ON rolePrincipal.principal_id = membership.role_principal_id
    WHERE membership.member_principal_id = @runtimeRoleId
      AND rolePrincipal.name NOT IN (N''db_datareader'', N''db_datawriter'');
OPEN RuntimeRoleParents;
FETCH NEXT FROM RuntimeRoleParents INTO @roleName;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @dynamicSql = N''ALTER ROLE '' + QUOTENAME(@roleName) + N'' DROP MEMBER '' + QUOTENAME(@runtimeRoleName) + N'';'';
    EXEC sys.sp_executesql @dynamicSql;
    FETCH NEXT FROM RuntimeRoleParents INTO @roleName;
END;
CLOSE RuntimeRoleParents;
DEALLOCATE RuntimeRoleParents;

IF NOT EXISTS (SELECT 1 FROM sys.database_role_members WHERE role_principal_id = DATABASE_PRINCIPAL_ID(N''db_datareader'') AND member_principal_id = @runtimeUserId)
BEGIN
    SET @dynamicSql = N''ALTER ROLE [db_datareader] ADD MEMBER '' + QUOTENAME(@runtimeLoginName) + N'';'';
    EXEC sys.sp_executesql @dynamicSql;
END;
IF NOT EXISTS (SELECT 1 FROM sys.database_role_members WHERE role_principal_id = DATABASE_PRINCIPAL_ID(N''db_datawriter'') AND member_principal_id = @runtimeUserId)
BEGIN
    SET @dynamicSql = N''ALTER ROLE [db_datawriter] ADD MEMBER '' + QUOTENAME(@runtimeLoginName) + N'';'';
    EXEC sys.sp_executesql @dynamicSql;
END;
IF NOT EXISTS (SELECT 1 FROM sys.database_role_members WHERE role_principal_id = @runtimeRoleId AND member_principal_id = @runtimeUserId)
BEGIN
    SET @dynamicSql = N''ALTER ROLE '' + QUOTENAME(@runtimeRoleName) + N'' ADD MEMBER '' + QUOTENAME(@runtimeLoginName) + N'';'';
    EXEC sys.sp_executesql @dynamicSql;
END;

GRANT BACKUP DATABASE TO ' + QUOTENAME(@RuntimeRoleName) + N';
GRANT BACKUP LOG TO ' + QUOTENAME(@RuntimeRoleName) + N';

IF @deploymentUserId IS NULL
BEGIN
    SET @dynamicSql = N''CREATE USER '' + QUOTENAME(@deploymentLoginName) + N'' FOR LOGIN '' + QUOTENAME(@deploymentLoginName) + N'';'';
    EXEC sys.sp_executesql @dynamicSql;
    SET @deploymentUserId = USER_ID(@deploymentLoginName);
END;
IF @deploymentUserId IS NULL OR NOT EXISTS
(
    SELECT 1 FROM sys.database_principals
    WHERE principal_id = @deploymentUserId AND type IN (''U'', ''G'') AND sid = SUSER_SID(@deploymentLoginName)
)
    THROW 51010, N''Deployment user is not a Windows user mapped to the requested login.'', 1;
IF NOT EXISTS (SELECT 1 FROM sys.database_role_members WHERE role_principal_id = DATABASE_PRINCIPAL_ID(N''db_owner'') AND member_principal_id = @deploymentUserId)
BEGIN
    SET @dynamicSql = N''ALTER ROLE [db_owner] ADD MEMBER '' + QUOTENAME(@deploymentLoginName) + N'';'';
    EXEC sys.sp_executesql @dynamicSql;
END;

SELECT
    DB_NAME() AS DatabaseName,
    @runtimeLoginName AS RuntimeLoginName,
    @runtimeRoleName AS RuntimeRoleName,
    IS_SRVROLEMEMBER(N''sysadmin'', @runtimeLoginName) AS RuntimeIsSysadmin,
    IS_SRVROLEMEMBER(N''dbcreator'', @runtimeLoginName) AS RuntimeIsDbcreator,
    IS_SRVROLEMEMBER(N''PharmacyAppRestoreTest'', @runtimeLoginName) AS RuntimeRestoreTestRole,
    IS_MEMBER(N''db_owner'') AS ExecutingUserIsDbOwner,
    CASE WHEN EXISTS (SELECT 1 FROM sys.database_role_members WHERE role_principal_id = @runtimeRoleId AND member_principal_id = @runtimeUserId) THEN 1 ELSE 0 END AS RuntimeRoleMapped,
    CASE WHEN EXISTS (SELECT 1 FROM sys.database_role_members WHERE role_principal_id = DATABASE_PRINCIPAL_ID(N''db_owner'') AND member_principal_id = @runtimeUserId) THEN 1 ELSE 0 END AS RuntimeDirectDbOwner,
    @deploymentLoginName AS DeploymentLoginName;
';

EXEC sys.sp_executesql
    @DatabaseBatch,
    N'@runtimeLoginName sysname, @deploymentLoginName sysname, @runtimeRoleName sysname',
    @runtimeLoginName = @RuntimeLoginName,
    @deploymentLoginName = @DeploymentLoginName,
    @runtimeRoleName = @RuntimeRoleName;

IF IS_SRVROLEMEMBER(N'sysadmin', @RuntimeLoginName) <> 0 OR IS_SRVROLEMEMBER(N'dbcreator', @RuntimeLoginName) <> 0 OR IS_SRVROLEMEMBER(N'PharmacyAppRestoreTest', @RuntimeLoginName) <> 1
    THROW 51011, N'Runtime identity must be neither sysadmin nor dbcreator and must be in PharmacyAppRestoreTest.', 1;

PRINT N'Pilot least-privilege provisioning completed. Runtime uses Windows authentication, CREATE ANY DATABASE only for guarded restore-test copies, PharmacyAppRuntime DML/backup permissions, and no dbcreator/db_owner/sysadmin.';
