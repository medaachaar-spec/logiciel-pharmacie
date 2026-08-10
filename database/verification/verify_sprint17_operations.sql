SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'Pharmacy_Development'
    THROW 51700, 'Ce script cible uniquement Pharmacy_Development.', 1;

IF SCHEMA_ID(N'operations') IS NULL
    THROW 51701, 'Schéma operations absent.', 1;
IF OBJECT_ID(N'operations.BackupRecords', N'U') IS NULL OR OBJECT_ID(N'operations.ImportJobs', N'U') IS NULL
    THROW 51702, 'Tables operations incomplètes.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE N'%_BackupImportDeviceOperationsV01')
    THROW 51703, 'Migration Sprint 17 absente.', 1;
IF (SELECT COUNT(*) FROM security.Permissions WHERE Code IN (N'backup.view',N'backup.create',N'backup.restore',N'import.view',N'import.execute',N'devices.configure')) <> 6
    THROW 51704, 'Permissions Sprint 17 incomplètes.', 1;
IF EXISTS (SELECT 1 FROM security.Permissions WHERE Code IN (N'backup.restore',N'import.execute',N'devices.configure') AND IsSensitive = 0)
    THROW 51705, 'Une permission sensible Sprint 17 ne l’est pas en SQL.', 1;

SELECT N'PASS' AS Sprint17OperationsVerification,
       (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory) AS AppliedMigrations,
       (SELECT COUNT(*) FROM security.Permissions) AS PermissionCount;
