[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ServerInstance,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9_]+$')]
    [string]$DatabaseName,

    [Parameter(Mandatory)]
    [ValidateSet('Full', 'Differential', 'TransactionLog')]
    [string]$Type
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$backupRoot = [Environment]::GetEnvironmentVariable('PHARMACY_BACKUP_LOCAL_DIRECTORY')
if ([string]::IsNullOrWhiteSpace($backupRoot) -or -not [IO.Path]::IsPathRooted($backupRoot)) {
    throw 'PHARMACY_BACKUP_LOCAL_DIRECTORY doit contenir un chemin absolu accessible au compte SQL Server et au compte de tâche.'
}
$backupRoot = [IO.Path]::GetFullPath($backupRoot)
if ($backupRoot.Contains("'")) { throw 'Le chemin de sauvegarde contient un caractère interdit.' }
[IO.Directory]::CreateDirectory($backupRoot) | Out-Null

$operationId = [Guid]::NewGuid().ToString('N')
$extension = if ($Type -eq 'TransactionLog') { '.trn' } else { '.bak' }
$fileName = 'Pharmacy_{0}_{1}_{2}_{3}{4}' -f $DatabaseName, $Type, (Get-Date).ToUniversalTime().ToString('yyyyMMdd_HHmmss'), $operationId, $extension
$backupPath = [IO.Path]::GetFullPath([IO.Path]::Combine($backupRoot, $fileName))
$rootPrefix = $backupRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $backupPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Chemin de sortie hors du dossier autorisé.' }

$options = switch ($Type) {
    'Full' { 'COPY_ONLY, CHECKSUM, INIT' }
    'Differential' { 'DIFFERENTIAL, CHECKSUM, INIT' }
    'TransactionLog' { 'CHECKSUM, INIT' }
}
$statement = if ($Type -eq 'TransactionLog') {
    "BACKUP LOG [$DatabaseName] TO DISK=N'$backupPath' WITH $options;"
} else {
    "BACKUP DATABASE [$DatabaseName] TO DISK=N'$backupPath' WITH $options;"
}

& sqlcmd -b -r 1 -S $ServerInstance -E -C -d master -Q $statement
if ($LASTEXITCODE -ne 0) { throw "BACKUP SQL a échoué avec le code $LASTEXITCODE." }
& sqlcmd -b -r 1 -S $ServerInstance -E -C -d master -Q "RESTORE VERIFYONLY FROM DISK=N'$backupPath' WITH CHECKSUM;"
if ($LASTEXITCODE -ne 0) { throw "RESTORE VERIFYONLY a échoué avec le code $LASTEXITCODE." }

$hash = (Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash
$externalStatus = 'NotConfigured'
$externalRoot = [Environment]::GetEnvironmentVariable('PHARMACY_BACKUP_EXTERNAL_DIRECTORY')
if (-not [string]::IsNullOrWhiteSpace($externalRoot)) {
    if (-not [IO.Path]::IsPathRooted($externalRoot)) { throw 'PHARMACY_BACKUP_EXTERNAL_DIRECTORY doit être absolu.' }
    if (-not [string]::Equals([Environment]::GetEnvironmentVariable('PHARMACY_BACKUP_EXTERNAL_ENCRYPTED'), 'true', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Copie externe bloquée : le volume chiffré doit être explicitement attesté par PHARMACY_BACKUP_EXTERNAL_ENCRYPTED=true.'
    }
    $externalRoot = [IO.Path]::GetFullPath($externalRoot)
    [IO.Directory]::CreateDirectory($externalRoot) | Out-Null
    $externalPath = [IO.Path]::Combine($externalRoot, $fileName)
    Copy-Item -LiteralPath $backupPath -Destination $externalPath
    if ((Get-FileHash -LiteralPath $externalPath -Algorithm SHA256).Hash -ne $hash) { throw 'La copie externe ne correspond pas au SHA-256 local.' }
    $externalStatus = 'CopiedEncryptedVolume'
}

[pscustomobject]@{
    OperationId = $operationId
    Database = $DatabaseName
    Type = $Type
    File = $backupPath
    Sha256 = $hash
    VerifyOnly = 'PASS'
    ExternalCopy = $externalStatus
    CompletedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
}
