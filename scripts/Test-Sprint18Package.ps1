[CmdletBinding()]
param([string]$ArtifactRoot)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) { throw 'ArtifactRoot est requis.' }
$root = [IO.Path]::GetFullPath($ArtifactRoot)
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$props = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props') -Raw
$expectedVersion = [string]$props.Project.PropertyGroup.PharmacyReleaseVersion
$manifestPath = Join-Path $root 'release-manifest.json'
$hashesPath = Join-Path $root 'SHA256SUMS.txt'
if (-not (Test-Path -LiteralPath $manifestPath) -or -not (Test-Path -LiteralPath $hashesPath)) { throw 'Manifestes absents.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.Version -ne $expectedVersion -or $manifest.Runtime -ne 'win-x64' -or -not $manifest.SelfContained) { throw 'Métadonnées RC invalides.' }
foreach ($file in $manifest.Files) {
    $path = Join-Path $root $file.Path
    if (-not (Test-Path -LiteralPath $path)) { throw "Fichier manquant : $($file.Path)" }
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($actual -ne $file.Sha256) { throw "Hash invalide : $($file.Path)" }
}
$setup = Get-ChildItem -LiteralPath (Join-Path $root 'installer') -Filter '*Setup.exe' -File
if ($setup.Count -ne 1) { throw 'Un et un seul Setup.exe est attendu.' }
if (-not (Test-Path -LiteralPath (Join-Path $root 'publish\desktop\Pharmacy.Desktop.exe'))) { throw 'Desktop publié absent.' }
if (-not (Test-Path -LiteralPath (Join-Path $root 'publish\deployment\Pharmacy.Deployment.exe'))) { throw 'Outil de déploiement publié absent.' }
$forbidden = Get-ChildItem -LiteralPath (Join-Path $root 'publish') -File -Recurse |
    Where-Object { $_.Extension -in @('.pdb', '.log', '.bak', '.mdf', '.ldf', '.pfx', '.p12', '.pem', '.key') }
if ($forbidden) { throw "Fichiers interdits dans le package : $($forbidden.Name -join ', ')" }
Write-Host "PASS package $($manifest.Version) : $($manifest.Files.Count) fichiers vérifiés."
