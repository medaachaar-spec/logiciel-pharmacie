[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ServerInstance,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$BackupFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$allowedRoot = [Environment]::GetEnvironmentVariable('PHARMACY_BACKUP_LOCAL_DIRECTORY')
if ([string]::IsNullOrWhiteSpace($allowedRoot) -or -not [IO.Path]::IsPathRooted($allowedRoot)) { throw 'PHARMACY_BACKUP_LOCAL_DIRECTORY est requis.' }
$allowedRoot = [IO.Path]::GetFullPath($allowedRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$backupPath = [IO.Path]::GetFullPath($BackupFile)
if (-not $backupPath.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($backupPath) -ne '.bak' -or -not [IO.File]::Exists($backupPath) -or $backupPath.Contains("'")) {
    throw 'Le restore-test accepte uniquement un .bak existant dans le dossier local autorisé.'
}

$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$temporaryDatabase = "Pharmacy_RestoreTest_$suffix"
$restored = $false
try {
    & sqlcmd -b -r 1 -S $ServerInstance -E -C -d master -Q "RESTORE VERIFYONLY FROM DISK=N'$backupPath' WITH CHECKSUM;"
    if ($LASTEXITCODE -ne 0) { throw 'RESTORE VERIFYONLY a échoué.' }

    $fileRows = & sqlcmd -b -S $ServerInstance -E -C -d master -h -1 -s '|' -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK=N'$backupPath';"
    if ($LASTEXITCODE -ne 0) { throw 'RESTORE FILELISTONLY a échoué.' }
    $files = @($fileRows | ForEach-Object {
        $parts = $_ -split '\|'
        if ($parts.Count -ge 3 -and $parts[2].Trim() -in @('D', 'L')) {
            [pscustomobject]@{ LogicalName = $parts[0].Trim(); Type = $parts[2].Trim() }
        }
    })
    if ($files.Count -eq 0) { throw 'Aucun fichier logique SQL exploitable dans la sauvegarde.' }

    $dataPath = (& sqlcmd -b -S $ServerInstance -E -C -d master -h -1 -W -Q "SET NOCOUNT ON; SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath'));").Trim()
    $logPath = (& sqlcmd -b -S $ServerInstance -E -C -d master -h -1 -W -Q "SET NOCOUNT ON; SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultLogPath'));").Trim()
    $moves = for ($index = 0; $index -lt $files.Count; $index++) {
        $logical = $files[$index].LogicalName.Replace("'", "''")
        $root = if ($files[$index].Type -eq 'L') { $logPath } else { $dataPath }
        $extension = if ($files[$index].Type -eq 'L') { '.ldf' } else { '.mdf' }
        $physical = [IO.Path]::Combine($root, "${temporaryDatabase}_${index}${extension}").Replace("'", "''")
        "MOVE N'$logical' TO N'$physical'"
    }
    $restoreSql = "RESTORE DATABASE [$temporaryDatabase] FROM DISK=N'$backupPath' WITH $($moves -join ', '), RECOVERY, CHECKSUM;"
    & sqlcmd -b -r 1 -S $ServerInstance -E -C -d master -Q $restoreSql
    if ($LASTEXITCODE -ne 0) { throw 'La restauration isolée a échoué.' }
    $restored = $true

    $health = (& sqlcmd -b -S $ServerInstance -E -C -d $temporaryDatabase -h -1 -W -Q "SET NOCOUNT ON; SELECT CONCAT((SELECT COUNT(*) FROM dbo.__EFMigrationsHistory),'|',(SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE (s.name=N'catalog' AND t.name=N'Products') OR (s.name=N'inventory' AND t.name=N'StockBalances') OR (s.name=N'sales' AND t.name=N'Sales') OR (s.name=N'security' AND t.name=N'Permissions')));").Trim()
    $parts = $health -split '\|'
    if ($parts.Count -ne 2 -or [int]$parts[0] -lt 1 -or [int]$parts[1] -ne 4) { throw "Health check invalide : $health" }

    [pscustomobject]@{ TemporaryDatabase = $temporaryDatabase; AppliedMigrations = [int]$parts[0]; CriticalTables = [int]$parts[1]; Health = 'PASS'; CompletedAtUtc = (Get-Date).ToUniversalTime().ToString('o') }
}
finally {
    $cleanupSql = "IF DB_ID(N'$temporaryDatabase') IS NOT NULL BEGIN ALTER DATABASE [$temporaryDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$temporaryDatabase]; END;"
    & sqlcmd -b -r 1 -S $ServerInstance -E -C -d master -Q $cleanupSql
    if ($LASTEXITCODE -ne 0) { Write-Error "Nettoyage obligatoire de $temporaryDatabase en échec." }
}
