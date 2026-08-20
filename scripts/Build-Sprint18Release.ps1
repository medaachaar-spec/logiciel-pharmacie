[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$InnoCompiler
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$propsPath = Join-Path $repositoryRoot 'Directory.Build.props'
[xml]$props = Get-Content -LiteralPath $propsPath -Raw
$version = [string]$props.Project.PropertyGroup.PharmacyReleaseVersion
if ($version -notmatch '^\d+\.\d+\.\d+-rc\.\d+$') { throw 'Version RC centrale invalide.' }
if ($Runtime -ne 'win-x64') { throw 'Le Sprint 18 autorise uniquement win-x64.' }

$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts\sprint-18\$version"))
$allowedArtifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\sprint-18'))
if (-not $artifactRoot.StartsWith($allowedArtifactRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Cible de génération hors du répertoire artifacts autorisé.'
}
if (Test-Path -LiteralPath $artifactRoot) { Remove-Item -LiteralPath $artifactRoot -Recurse -Force }

$publishRoot = Join-Path $artifactRoot 'publish'
$desktopRoot = Join-Path $publishRoot 'desktop'
$deploymentRoot = Join-Path $publishRoot 'deployment'
$installerRoot = Join-Path $artifactRoot 'installer'
[IO.Directory]::CreateDirectory($desktopRoot) | Out-Null
[IO.Directory]::CreateDirectory($deploymentRoot) | Out-Null
[IO.Directory]::CreateDirectory($installerRoot) | Out-Null

Push-Location $repositoryRoot
try {
    dotnet publish 'src/Pharmacy.Desktop/Pharmacy.Desktop.csproj' -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $desktopRoot
    if ($LASTEXITCODE -ne 0) { throw 'Publication Desktop en échec.' }
    dotnet publish 'src/Pharmacy.Deployment/Pharmacy.Deployment.csproj' -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $deploymentRoot
    if ($LASTEXITCODE -ne 0) { throw 'Publication Deployment en échec.' }
}
finally { Pop-Location }

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'installer\pilot.template.json') -Destination $publishRoot

if ([string]::IsNullOrWhiteSpace($InnoCompiler)) {
    $command = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($command) { $InnoCompiler = $command.Source }
    else {
        $candidates = @(
            "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
            "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
            "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        )
        $InnoCompiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
}
if ([string]::IsNullOrWhiteSpace($InnoCompiler) -or -not (Test-Path -LiteralPath $InnoCompiler)) {
    throw 'ISCC.exe est requis pour produire le Setup installable.'
}

& $InnoCompiler "/DAppVersion=$version" "/DSourceRoot=$publishRoot" "/DOutputRoot=$installerRoot" (Join-Path $repositoryRoot 'installer\PharmacyPilot.iss')
if ($LASTEXITCODE -ne 0) { throw 'Compilation Inno Setup en échec.' }

$manifest = Get-ChildItem -LiteralPath $artifactRoot -File -Recurse |
    Where-Object { $_.Name -notin @('SHA256SUMS.txt', 'release-manifest.json') } |
    Sort-Object FullName |
    ForEach-Object {
        [pscustomobject]@{
            Path = [IO.Path]::GetRelativePath($artifactRoot, $_.FullName).Replace('\', '/')
            Size = $_.Length
            Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    }
$manifest | ForEach-Object { "$($_.Sha256)  $($_.Path)" } |
    Set-Content -LiteralPath (Join-Path $artifactRoot 'SHA256SUMS.txt') -Encoding ascii
$releaseManifest = [ordered]@{
    Product = 'Logiciel Pharmacie'
    Version = $version
    Channel = 'Release Candidate pilote'
    Runtime = $Runtime
    SelfContained = $true
    Trimmed = $false
    SingleFile = $false
    SourceHead = (git -C $repositoryRoot rev-parse HEAD)
    GeneratedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    Files = $manifest
}
$releaseManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $artifactRoot 'release-manifest.json') -Encoding utf8
Write-Host "Release candidate générée : $artifactRoot"
