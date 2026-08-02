[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$results = New-Object 'System.Collections.Generic.List[object]'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$startingLocation = (Get-Location).Path

function Add-Check {
    param(
        [Parameter(Mandatory = $true)][string]$Component,
        [Parameter(Mandatory = $true)][string]$Requirement,
        [Parameter(Mandatory = $true)][string]$Detected,
        [Parameter(Mandatory = $true)][ValidateSet('PASS', 'WARNING', 'MISSING', 'BLOCKING')][string]$Status,
        [Parameter(Mandatory = $true)][bool]$Blocking,
        [Parameter(Mandatory = $true)][string]$Evidence,
        [Parameter(Mandatory = $true)][string]$Action,
        [ValidateSet('AUTO', 'NONE', 'WARNING', 'BLOCKING')][string]$FoundationImpact = 'AUTO',
        [ValidateSet('AUTO', 'NONE', 'WARNING', 'BLOCKING')][string]$PersistenceImpact = 'AUTO',
        [ValidateSet('AUTO', 'NONE', 'WARNING', 'BLOCKING')][string]$PilotImpact = 'AUTO'
    )

    $defaultImpact = if ($Blocking) {
        'BLOCKING'
    }
    elseif ($Status -eq 'WARNING' -or $Status -eq 'MISSING') {
        'WARNING'
    }
    else {
        'NONE'
    }

    if ($FoundationImpact -eq 'AUTO') { $FoundationImpact = $defaultImpact }
    if ($PersistenceImpact -eq 'AUTO') { $PersistenceImpact = $FoundationImpact }
    if ($PilotImpact -eq 'AUTO') { $PilotImpact = $PersistenceImpact }

    [void]$results.Add([pscustomobject]@{
        Component         = $Component
        Requirement       = $Requirement
        Detected          = $Detected
        Status            = $Status
        FoundationImpact  = $FoundationImpact
        PersistenceImpact = $PersistenceImpact
        PilotImpact       = $PilotImpact
        Evidence          = $Evidence
        Action            = $Action
    })
}

function Invoke-NativeCapture {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$Arguments = @()
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # Windows PowerShell 5.1 can promote redirected native stderr records
        # when the caller uses Stop. Capture them and decide from the exit code.
        $ErrorActionPreference = 'Continue'
        $output = @(& $FilePath @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    return [pscustomobject]@{
        ExitCode = $exitCode
        Output   = $output
    }
}

function Get-OptionalPropertyValue {
    param(
        [AllowNull()][object]$InputObject,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($null -eq $InputObject) {
        return ''
    }

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return ''
    }

    return [string]$property.Value
}

function Join-NonEmptyLines {
    param([object[]]$Lines)

    $safeLines = @($Lines | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ })
    return ($safeLines -join '; ')
}

function Get-ReadinessResult {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('FoundationImpact', 'PersistenceImpact', 'PilotImpact')]
        [string]$ImpactProperty
    )

    $blockers = @($results | Where-Object { $_.$ImpactProperty -eq 'BLOCKING' })
    $warnings = @($results | Where-Object { $_.$ImpactProperty -eq 'WARNING' })
    $verdict = if ($blockers.Count -gt 0) {
        'REMEDIATION REQUIRED'
    }
    elseif ($warnings.Count -gt 0) {
        'READY WITH WARNINGS'
    }
    else {
        'READY'
    }

    return [pscustomobject]@{
        Verdict  = $verdict
        Blockers = @($blockers | ForEach-Object { $_.Component })
        Warnings = @($warnings | ForEach-Object { $_.Component })
    }
}

Push-Location -LiteralPath $repoRoot

try {
    # 1. Windows edition and x64 architecture.
    $os = Get-CimInstance -ClassName Win32_OperatingSystem
    $computer = Get-CimInstance -ClassName Win32_ComputerSystem
    $isWindows11 = $os.Caption -match 'Windows 11'
    $isPro = $os.Caption -match '(?i)( Pro|Professionnel)'
    $isX64 = ($os.OSArchitecture -match '64') -and ($computer.SystemType -match 'x64')
    $osDetected = '{0}; version {1}; build {2}; {3}; {4}' -f $os.Caption, $os.Version, $os.BuildNumber, $os.OSArchitecture, $computer.SystemType
    if ($isWindows11 -and $isX64 -and $isPro) {
        Add-Check 'Windows / architecture' 'Windows 11 x64 for foundation; Pro mandatory for pilot certification' $osDetected 'PASS' $false 'Get-CimInstance Win32_OperatingSystem, Win32_ComputerSystem' 'None.'
    }
    elseif ($isWindows11 -and $isX64) {
        Add-Check 'Windows / architecture' 'Windows 11 x64 for foundation; Pro mandatory for pilot certification' $osDetected 'WARNING' $false 'Get-CimInstance Win32_OperatingSystem, Win32_ComputerSystem' 'Foundation work may continue. Upgrade or use Windows 11 Pro x64 before pilot workstation certification.' -FoundationImpact 'WARNING' -PersistenceImpact 'WARNING' -PilotImpact 'BLOCKING'
    }
    else {
        Add-Check 'Windows / architecture' 'Windows 11 x64 for foundation; Pro mandatory for pilot certification' $osDetected 'BLOCKING' $true 'Get-CimInstance Win32_OperatingSystem, Win32_ComputerSystem' 'Use a supported Windows 11 x64 development machine. Windows 11 Pro x64 remains mandatory for pilot certification.'
    }

    # 2. PowerShell.
    $psVersion = $PSVersionTable.PSVersion
    if ($psVersion -ge [version]'5.1') {
        Add-Check 'PowerShell' 'Mandatory: PowerShell 5.1 or later' ($psVersion.ToString()) 'PASS' $false '$PSVersionTable.PSVersion' 'None.'
    }
    else {
        Add-Check 'PowerShell' 'Mandatory: PowerShell 5.1 or later' ($psVersion.ToString()) 'BLOCKING' $true '$PSVersionTable.PSVersion' 'Install or use a supported PowerShell version.'
    }

    # 3. Free disk space on the repository drive.
    $repoDrive = (Get-Item -LiteralPath $repoRoot).PSDrive
    $freeGb = [math]::Round($repoDrive.Free / 1GB, 2)
    if ($freeGb -ge 25) {
        Add-Check 'Free disk space' 'Recommended: at least 25 GB before SQL, installation, and backup sprints; mandatory floor: 10 GB' ("$freeGb GB on $($repoDrive.Name):") 'PASS' $false "Get-PSDrive $($repoDrive.Name)" 'None.'
    }
    elseif ($freeGb -ge 10) {
        Add-Check 'Free disk space' 'Recommended: at least 25 GB before SQL, installation, and backup sprints; mandatory floor: 10 GB' ("$freeGb GB on $($repoDrive.Name):") 'WARNING' $false "Get-PSDrive $($repoDrive.Name)" 'Free disk space to at least 25 GB before SQL persistence, installation, and backup work.' -FoundationImpact 'WARNING' -PersistenceImpact 'WARNING' -PilotImpact 'WARNING'
    }
    else {
        Add-Check 'Free disk space' 'Recommended: at least 25 GB before SQL, installation, and backup sprints; mandatory floor: 10 GB' ("$freeGb GB on $($repoDrive.Name):") 'BLOCKING' $true "Get-PSDrive $($repoDrive.Name)" 'Free disk space to at least 25 GB before continuing.'
    }

    # 4-6. Git executable, repository, and current branch.
    $gitCommand = Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    $isGitRepository = $false
    if ($null -eq $gitCommand) {
        Add-Check 'Git' 'Mandatory: Git available' 'Not detected' 'BLOCKING' $true 'Get-Command git' 'Install Git for Windows, then rerun this audit.'
        Add-Check 'Git repository' 'Mandatory: local Git repository' 'Cannot check because Git is missing' 'BLOCKING' $true 'git rev-parse --is-inside-work-tree' 'Install Git and initialize the repository locally on main.'
        Add-Check 'Git branch' 'Mandatory: branch known; main for a new repository' 'Cannot check because Git is missing' 'BLOCKING' $true 'git branch --show-current' 'Install Git and initialize the repository locally on main.'
    }
    else {
        $gitVersionResult = Invoke-NativeCapture $gitCommand.Source @('--version')
        $gitVersion = Join-NonEmptyLines $gitVersionResult.Output
        Add-Check 'Git' 'Mandatory: Git available' ("$gitVersion; $($gitCommand.Source)") 'PASS' $false 'git --version; Get-Command git' 'None.'

        $repoResult = Invoke-NativeCapture $gitCommand.Source @('rev-parse', '--is-inside-work-tree')
        $isGitRepository = ($repoResult.ExitCode -eq 0) -and ((Join-NonEmptyLines $repoResult.Output) -eq 'true')
        if ($isGitRepository) {
            Add-Check 'Git repository' 'Mandatory: local Git repository' 'Detected' 'PASS' $false 'git rev-parse --is-inside-work-tree' 'None.'
            $branchResult = Invoke-NativeCapture $gitCommand.Source @('branch', '--show-current')
            $branch = Join-NonEmptyLines $branchResult.Output
            if ($branch) {
                $branchStatus = if ($branch -eq 'main') { 'PASS' } else { 'WARNING' }
                $branchAction = if ($branch -eq 'main') { 'None.' } else { 'Preserve the existing branch unless a separately approved migration to main is required.' }
                Add-Check 'Git branch' 'Mandatory: branch known; main for a new repository' $branch $branchStatus $false 'git branch --show-current' $branchAction
            }
            else {
                Add-Check 'Git branch' 'Mandatory: branch known; main for a new repository' 'Detached or unavailable' 'BLOCKING' $true 'git branch --show-current' 'Restore a named local branch without rewriting history.'
            }
        }
        else {
            Add-Check 'Git repository' 'Mandatory: local Git repository' 'Not detected' 'BLOCKING' $true 'git rev-parse --is-inside-work-tree' 'Initialize Git locally with main; do not add a remote or commit during Sprint 0.'
            Add-Check 'Git branch' 'Mandatory: branch known; main for a new repository' 'Unavailable without a repository' 'BLOCKING' $true 'git branch --show-current' 'Initialize Git locally with main.'
        }
    }

    # 7. Required documentation.
    $requiredDocuments = @(
        'README.md',
        'MANIFEST.txt',
        'docs/governance/PROJECT_CONTEXT.md',
        'docs/governance/DECISIONS_REGISTER.md',
        'docs/governance/SCOPE.md',
        'docs/governance/CURRENT_STATE.md',
        'docs/governance/CODEX_WORKFLOW.md',
        'docs/templates/CODEX_PROMPT_TEMPLATE.md',
        'docs/templates/CODEX_REPORT_TEMPLATE.md',
        'docs/reports/README.md'
    )
    $missingDocuments = @($requiredDocuments | Where-Object { -not (Test-Path -LiteralPath (Join-Path $repoRoot $_) -PathType Leaf) })
    if ($missingDocuments.Count -eq 0) {
        Add-Check 'Project documentation' 'Mandatory: all expected governance documents and templates' ("$($requiredDocuments.Count) of $($requiredDocuments.Count) files present") 'PASS' $false 'Test-Path for the documented source-of-truth list' 'None.'
    }
    else {
        Add-Check 'Project documentation' 'Mandatory: all expected governance documents and templates' ("$($missingDocuments.Count) file(s) missing") 'BLOCKING' $true 'Test-Path for the documented source-of-truth list' 'Restore the missing official documents without renaming or rewriting validated decisions.'
    }

    # 8. Five reference PDFs.
    $expectedPdfs = @(
        '01_Dossier_ADR_Logiciel_Pharmacie_V1.0.pdf',
        '02_Dossier_Phase_2_Modelisation_Fonctionnelle_Complete_V0.1.pdf',
        '03_Dossier_Phase_3_Conception_Modele_Donnees_V0.1.pdf',
        '04_Dossier_Phase_4_Conception_Parcours_UX_UI_V0.1.pdf',
        '05_Dossier_Phase_5_Code_First_Vibe_Coding_V0.1.pdf'
    )
    $missingPdfs = @($expectedPdfs | Where-Object { -not (Test-Path -LiteralPath (Join-Path $repoRoot (Join-Path 'docs/reference' $_)) -PathType Leaf) })
    if ($missingPdfs.Count -eq 0) {
        Add-Check 'Reference PDFs' 'Mandatory: five official PDF references' '5 of 5 files present' 'PASS' $false 'Test-Path docs/reference/*.pdf' 'None.'
    }
    else {
        Add-Check 'Reference PDFs' 'Mandatory: five official PDF references' ("$($missingPdfs.Count) file(s) missing") 'BLOCKING' $true 'Test-Path docs/reference/*.pdf' 'Restore the missing official PDF files; do not rename or alter existing PDFs.'
    }

    # 9-11. .NET SDKs and runtimes.
    $dotnetCommand = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    $sdkVersions = @()
    $stable10Sdks = @()
    $runtimeLines = @()
    if ($null -eq $dotnetCommand) {
        Add-Check '.NET SDKs' 'Mandatory: dotnet CLI and installed SDKs' 'Not detected' 'BLOCKING' $true 'Get-Command dotnet; dotnet --list-sdks' 'Install a stable .NET 10 SDK through the approved local process.'
        Add-Check '.NET 10 stable SDK' 'Mandatory: at least one stable .NET 10 SDK' 'Not detected' 'BLOCKING' $true 'dotnet --list-sdks' 'Install a stable .NET 10 SDK; do not use a preview or release candidate.'
        Add-Check '.NET runtimes' 'Mandatory: .NET, ASP.NET Core, and Windows Desktop 10 runtimes' 'Not detected' 'BLOCKING' $true 'dotnet --list-runtimes' 'Install the approved .NET 10 development tooling.'
    }
    else {
        $sdkResult = Invoke-NativeCapture $dotnetCommand.Source @('--list-sdks')
        $sdkLines = @($sdkResult.Output | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ })
        $sdkVersions = @($sdkLines | ForEach-Object { if ($_ -match '^(\S+)\s+\[') { $matches[1] } })
        if ($sdkResult.ExitCode -eq 0 -and $sdkVersions.Count -gt 0) {
            Add-Check '.NET SDKs' 'Mandatory: dotnet CLI and installed SDKs' (($sdkVersions | Sort-Object) -join ', ') 'PASS' $false 'dotnet --list-sdks' 'None.'
        }
        else {
            Add-Check '.NET SDKs' 'Mandatory: dotnet CLI and installed SDKs' 'dotnet found but no SDK listed' 'BLOCKING' $true 'dotnet --list-sdks' 'Install a stable .NET 10 SDK.'
        }

        $stable10Sdks = @($sdkVersions | Where-Object { $_ -match '^10\.\d+\.\d+$' } | Sort-Object { [version]$_ } -Descending)
        if ($stable10Sdks.Count -gt 0) {
            Add-Check '.NET 10 stable SDK' 'Mandatory: at least one stable .NET 10 SDK' (($stable10Sdks -join ', ')) 'PASS' $false 'dotnet --list-sdks; stable version pattern only' 'None.'
        }
        else {
            Add-Check '.NET 10 stable SDK' 'Mandatory: at least one stable .NET 10 SDK' 'No stable .NET 10 SDK detected' 'BLOCKING' $true 'dotnet --list-sdks; stable version pattern only' 'Install a stable .NET 10 SDK; do not create global.json with an invented version.'
        }

        $runtimeResult = Invoke-NativeCapture $dotnetCommand.Source @('--list-runtimes')
        $runtimeLines = @($runtimeResult.Output | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ })
        $hasCore10 = @($runtimeLines | Where-Object { $_ -match '^Microsoft\.NETCore\.App 10\.' }).Count -gt 0
        $hasAspNet10 = @($runtimeLines | Where-Object { $_ -match '^Microsoft\.AspNetCore\.App 10\.' }).Count -gt 0
        $hasDesktop10 = @($runtimeLines | Where-Object { $_ -match '^Microsoft\.WindowsDesktop\.App 10\.' }).Count -gt 0
        if ($hasCore10 -and $hasAspNet10 -and $hasDesktop10) {
            Add-Check '.NET runtimes' 'Mandatory: .NET, ASP.NET Core, and Windows Desktop 10 runtimes' (Join-NonEmptyLines $runtimeLines) 'PASS' $false 'dotnet --list-runtimes' 'None.'
        }
        else {
            Add-Check '.NET runtimes' 'Mandatory: .NET, ASP.NET Core, and Windows Desktop 10 runtimes' (Join-NonEmptyLines $runtimeLines) 'BLOCKING' $true 'dotnet --list-runtimes' 'Install the missing stable .NET 10 runtime through the approved tooling setup.'
        }
    }

    # 12-14. Visual Studio and required workloads through vswhere.
    $programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
    $programFiles = [Environment]::GetFolderPath('ProgramFiles')
    $vswhereCandidates = @(
        (Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'),
        (Join-Path $programFiles 'Microsoft Visual Studio\Installer\vswhere.exe')
    )
    $vswhere = $vswhereCandidates | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -First 1
    $vsAll = @()
    $vsIde = @()
    if ($vswhere) {
        $vsResult = Invoke-NativeCapture $vswhere @('-all', '-products', '*', '-format', 'json')
        if ($vsResult.ExitCode -eq 0) {
            $vsJson = ($vsResult.Output | ForEach-Object { [string]$_ }) -join [Environment]::NewLine
            if ($vsJson.Trim()) {
                # Windows PowerShell 5.1 returns a top-level JSON array as one
                # pipeline object. Re-emit it once so installations are flat.
                $vsAll = @($vsJson | ConvertFrom-Json | ForEach-Object { $_ })
            }
        }
        $vsIde = @($vsAll | Where-Object { (Get-OptionalPropertyValue $_ 'productId') -match '^Microsoft\.VisualStudio\.Product\.(Community|Professional|Enterprise|BuildTools)$' })
    }

    if ($vsIde.Count -gt 0) {
        $vsDescriptions = @($vsIde | ForEach-Object { '{0} {1}' -f (Get-OptionalPropertyValue $_ 'displayName'), (Get-OptionalPropertyValue $_ 'installationVersion') })
        Add-Check 'Visual Studio' 'Recommended IDE; dotnet CLI and MSBuild are sufficient for foundation readiness' (($vsDescriptions -join ', ').Trim()) 'PASS' $false 'vswhere -all -products * -format json; IDE products filtered' 'None.'
    }
    else {
        $vsEvidence = if ($vswhere) { 'vswhere found; no Visual Studio IDE or Build Tools product returned' } else { 'vswhere not found in standard installer paths' }
        Add-Check 'Visual Studio' 'Recommended IDE; dotnet CLI and MSBuild are sufficient for foundation readiness' 'Not detected (SSMS alone is not Visual Studio)' 'WARNING' $false $vsEvidence 'Continue with dotnet CLI and MSBuild for Sprint 1. Reassess only if create, restore, or the WPF/Blazor Hybrid smoke build fails.' -FoundationImpact 'WARNING' -PersistenceImpact 'WARNING' -PilotImpact 'NONE'
    }

    $desktopWorkloadInstallations = @()
    $webWorkloadInstallations = @()
    if ($vswhere) {
        $desktopResult = Invoke-NativeCapture $vswhere @('-all', '-products', '*', '-requires', 'Microsoft.VisualStudio.Workload.ManagedDesktop', '-format', 'json')
        if ($desktopResult.ExitCode -eq 0) {
            $desktopJson = ($desktopResult.Output | ForEach-Object { [string]$_ }) -join [Environment]::NewLine
            if ($desktopJson.Trim()) {
                $desktopParsed = @($desktopJson | ConvertFrom-Json | ForEach-Object { $_ })
                $desktopWorkloadInstallations = @($desktopParsed | Where-Object { (Get-OptionalPropertyValue $_ 'productId') -match '^Microsoft\.VisualStudio\.Product\.(Community|Professional|Enterprise|BuildTools)$' })
            }
        }

        $webResult = Invoke-NativeCapture $vswhere @('-all', '-products', '*', '-requires', 'Microsoft.VisualStudio.Workload.NetWeb', '-format', 'json')
        if ($webResult.ExitCode -eq 0) {
            $webJson = ($webResult.Output | ForEach-Object { [string]$_ }) -join [Environment]::NewLine
            if ($webJson.Trim()) {
                $webParsed = @($webJson | ConvertFrom-Json | ForEach-Object { $_ })
                $webWorkloadInstallations = @($webParsed | Where-Object { (Get-OptionalPropertyValue $_ 'productId') -match '^Microsoft\.VisualStudio\.Product\.(Community|Professional|Enterprise|BuildTools)$' })
            }
        }
    }

    if ($desktopWorkloadInstallations.Count -gt 0) {
        Add-Check 'Visual Studio .NET Desktop workload' 'Recommended IDE workload; Sprint 1 smoke build is the foundation proof' 'Detected' 'PASS' $false 'vswhere -requires Microsoft.VisualStudio.Workload.ManagedDesktop' 'None.'
    }
    else {
        Add-Check 'Visual Studio .NET Desktop workload' 'Recommended IDE workload; Sprint 1 smoke build is the foundation proof' 'Not detected' 'WARNING' $false 'vswhere -requires Microsoft.VisualStudio.Workload.ManagedDesktop' 'Do not block Sprint 1. Reassess and add the workload only if the WPF/Blazor Hybrid smoke build requires it.' -FoundationImpact 'WARNING' -PersistenceImpact 'WARNING' -PilotImpact 'NONE'
    }

    if ($webWorkloadInstallations.Count -gt 0) {
        Add-Check 'Visual Studio ASP.NET workload' 'Recommended IDE workload; Sprint 1 smoke build is the foundation proof' 'Detected' 'PASS' $false 'vswhere -requires Microsoft.VisualStudio.Workload.NetWeb' 'None.'
    }
    else {
        Add-Check 'Visual Studio ASP.NET workload' 'Recommended IDE workload; Sprint 1 smoke build is the foundation proof' 'Not detected' 'WARNING' $false 'vswhere -requires Microsoft.VisualStudio.Workload.NetWeb' 'Do not block Sprint 1. Reassess and add the workload only if the WPF/Blazor Hybrid smoke build requires it.' -FoundationImpact 'WARNING' -PersistenceImpact 'WARNING' -PilotImpact 'NONE'
    }

    # 15. MSBuild.
    if ($null -ne $dotnetCommand) {
        $msbuildResult = Invoke-NativeCapture $dotnetCommand.Source @('msbuild', '-version', '-nologo')
        $msbuildVersion = Join-NonEmptyLines $msbuildResult.Output
        if ($msbuildResult.ExitCode -eq 0 -and $msbuildVersion) {
            Add-Check 'MSBuild' 'Mandatory: usable MSBuild' ("$msbuildVersion via dotnet") 'PASS' $false 'dotnet msbuild -version -nologo' 'None.'
        }
        else {
            Add-Check 'MSBuild' 'Mandatory: usable MSBuild' 'dotnet found but MSBuild invocation failed' 'BLOCKING' $true 'dotnet msbuild -version -nologo' 'Repair the approved .NET SDK or Visual Studio installation.'
        }
    }
    else {
        Add-Check 'MSBuild' 'Mandatory: usable MSBuild' 'Not detected' 'BLOCKING' $true 'dotnet msbuild -version -nologo' 'Install the approved .NET SDK and Visual Studio workloads.'
    }

    # 16. WebView2 Runtime.
    $webViewExecutables = @()
    $webViewMachinePath = Join-Path $programFilesX86 'Microsoft\EdgeWebView\Application'
    if (Test-Path -LiteralPath $webViewMachinePath -PathType Container) {
        $webViewExecutables = @(Get-ChildItem -LiteralPath $webViewMachinePath -Filter 'msedgewebview2.exe' -File -Recurse -ErrorAction SilentlyContinue)
    }
    if ($webViewExecutables.Count -gt 0) {
        $webViewVersions = @($webViewExecutables | ForEach-Object { $_.VersionInfo.ProductVersion } | Where-Object { $_ } | Sort-Object -Unique)
        Add-Check 'WebView2 Runtime' 'Mandatory: Microsoft Edge WebView2 Runtime' (($webViewVersions -join ', ')) 'PASS' $false 'Standard EdgeWebView runtime executable path and file version' 'None.'
    }
    else {
        Add-Check 'WebView2 Runtime' 'Mandatory: Microsoft Edge WebView2 Runtime' 'Not detected in the standard machine path' 'BLOCKING' $true 'C:\Program Files (x86)\Microsoft\EdgeWebView\Application' 'Install WebView2 Evergreen Runtime through the approved local setup process.'
    }

    # 17-19. SQL Server services, edition, and safe local metadata query.
    $sqlServices = @()
    try {
        $sqlServices = @(Get-CimInstance -ClassName Win32_Service | Where-Object { $_.Name -eq 'MSSQLSERVER' -or $_.Name -like 'MSSQL$*' })
    }
    catch {
        $sqlServices = @()
    }
    if ($sqlServices.Count -gt 0) {
        $sqlServiceDescription = @($sqlServices | ForEach-Object { '{0}={1}/{2}' -f $_.Name, $_.State, $_.StartMode }) -join ', '
        Add-Check 'SQL Server services' 'Deferred for foundation; required for SQL persistence and pilot production' $sqlServiceDescription 'PASS' $false 'Get-CimInstance Win32_Service filtered to SQL Database Engine services' 'None for detection; the audit does not modify service state.'
    }
    else {
        Add-Check 'SQL Server services' 'Deferred for foundation; required for SQL persistence and pilot production' 'No Database Engine service detected' 'WARNING' $false 'Get-CimInstance Win32_Service filtered to SQL Database Engine services' 'Foundation work may continue. Provide SQL Server 2025 Express before the persistence sprint; do not create a project database during this audit.' -FoundationImpact 'NONE' -PersistenceImpact 'BLOCKING' -PilotImpact 'BLOCKING'
    }

    $sqlInstanceRows = @()
    $instanceNamesKey = 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL'
    if (Test-Path -LiteralPath $instanceNamesKey) {
        $instanceProperties = Get-ItemProperty -LiteralPath $instanceNamesKey
        foreach ($property in @($instanceProperties.PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' })) {
            $setupKey = 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\{0}\Setup' -f $property.Value
            $setup = Get-ItemProperty -LiteralPath $setupKey -ErrorAction SilentlyContinue
            $sqlInstanceRows += [pscustomobject]@{
                Instance = $property.Name
                Edition  = Get-OptionalPropertyValue $setup 'Edition'
                Version  = Get-OptionalPropertyValue $setup 'Version'
                Patch    = Get-OptionalPropertyValue $setup 'PatchLevel'
            }
        }
    }

    $sqlDescriptions = @($sqlInstanceRows | ForEach-Object { '{0}: {1}, version {2}, patch {3}' -f $_.Instance, $_.Edition, $_.Version, $_.Patch })
    $express2025Rows = @($sqlInstanceRows | Where-Object { $_.Edition -match '(?i)Express' -and $_.Version -match '^17\.' })
    $runningServiceNames = @($sqlServices | Where-Object { $_.State -eq 'Running' } | ForEach-Object { $_.Name })
    $runningExpress2025Rows = @($express2025Rows | Where-Object {
        $expectedServiceName = if ($_.Instance -eq 'MSSQLSERVER') { 'MSSQLSERVER' } else { 'MSSQL$' + $_.Instance }
        $runningServiceNames -contains $expectedServiceName
    })
    if ($runningExpress2025Rows.Count -gt 0) {
        Add-Check 'SQL Server 2025 Express' 'Deferred for foundation; mandatory before SQL persistence and pilot production' (($sqlDescriptions -join '; ')) 'PASS' $false 'SQL Server instance registry metadata and service state' 'None.'
    }
    else {
        $sqlDetected = if ($sqlDescriptions.Count -gt 0) { $sqlDescriptions -join '; ' } else { 'No registered SQL Database Engine instance' }
        Add-Check 'SQL Server 2025 Express' 'Deferred for foundation; mandatory before SQL persistence and pilot production' $sqlDetected 'WARNING' $false 'SQL Server instance registry metadata and service state' 'Foundation and frontend prototype work may continue. Install SQL Server 2025 Express before the persistence sprint; do not create an instance or database during this audit.' -FoundationImpact 'WARNING' -PersistenceImpact 'BLOCKING' -PilotImpact 'BLOCKING'
    }

    # 20. sqlcmd presence (also used for the version query).
    $sqlcmdCommand = Get-Command sqlcmd -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $sqlcmdCommand) {
        Add-Check 'sqlcmd' 'Recommended for SQL persistence diagnostics' $sqlcmdCommand.Source 'PASS' $false 'Get-Command sqlcmd' 'None.'
    }
    else {
        Add-Check 'sqlcmd' 'Recommended for SQL persistence diagnostics' 'Not detected' 'MISSING' $false 'Get-Command sqlcmd' 'Add sqlcmd through the approved SQL tooling process before persistence diagnostics are required.' -FoundationImpact 'NONE' -PersistenceImpact 'WARNING' -PilotImpact 'WARNING'
    }

    if ($null -ne $sqlcmdCommand -and $sqlInstanceRows.Count -gt 0) {
        $queryRow = if ($express2025Rows.Count -gt 0) { $express2025Rows[0] } else { $sqlInstanceRows[0] }
        $sqlTarget = if ($queryRow.Instance -eq 'MSSQLSERVER') { '.' } else { '.\' + $queryRow.Instance }
        $sqlQuery = "SET NOCOUNT ON; SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion')) + '|' + CONVERT(nvarchar(128),SERVERPROPERTY('Edition')) + '|' + CONVERT(nvarchar(32),SERVERPROPERTY('ProductMajorVersion')) + '|' + CONVERT(nvarchar(32),SERVERPROPERTY('IsIntegratedSecurityOnly'));"
        $normalQueryResult = Invoke-NativeCapture $sqlcmdCommand.Source @('-S', $sqlTarget, '-E', '-l', '5', '-h', '-1', '-W', '-Q', $sqlQuery)
        $normalMetadata = @($normalQueryResult.Output | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ -match '^\d+(\.\d+){2,3}\|' }) | Select-Object -First 1
        if ($normalQueryResult.ExitCode -eq 0 -and $normalMetadata) {
            Add-Check 'SQL Server local version query' 'Deferred SQL/TLS verification through Windows authentication' $normalMetadata 'PASS' $false 'sqlcmd -S <local> -E with SERVERPROPERTY metadata query' 'None.'
        }
        else {
            $trustedQueryResult = Invoke-NativeCapture $sqlcmdCommand.Source @('-S', $sqlTarget, '-E', '-C', '-l', '5', '-h', '-1', '-W', '-Q', $sqlQuery)
            $trustedMetadata = @($trustedQueryResult.Output | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ -match '^\d+(\.\d+){2,3}\|' }) | Select-Object -First 1
            if ($trustedQueryResult.ExitCode -eq 0 -and $trustedMetadata) {
                Add-Check 'SQL Server local version query' 'Deferred SQL/TLS verification through Windows authentication' $trustedMetadata 'WARNING' $false 'Standard TLS validation failed; read-only sqlcmd retry used -C for this process only' 'Treat certificate trust during the persistence sprint. Do not persist TrustServerCertificate and do not disable TLS globally.' -FoundationImpact 'NONE' -PersistenceImpact 'WARNING' -PilotImpact 'WARNING'
            }
            else {
                Add-Check 'SQL Server local version query' 'Deferred SQL/TLS verification through Windows authentication' 'Metadata query unavailable; detailed native errors intentionally suppressed' 'WARNING' $false 'sqlcmd read-only metadata query with a 5-second timeout' 'Verify Windows authentication, instance reachability, and SQL Server TLS trust during the persistence sprint without changing service state.' -FoundationImpact 'NONE' -PersistenceImpact 'WARNING' -PilotImpact 'WARNING'
            }
        }
    }
    else {
        Add-Check 'SQL Server local version query' 'Deferred SQL/TLS verification through Windows authentication' 'Not attempted because sqlcmd or a registered instance is missing' 'MISSING' $false 'sqlcmd and SQL instance detection prerequisites' 'Provide the approved local SQL tooling before the persistence sprint, then rerun the audit.' -FoundationImpact 'NONE' -PersistenceImpact 'WARNING' -PilotImpact 'WARNING'
    }

    # 21. SSMS is informational and non-blocking.
    $ssmsProducts = @($vsAll | Where-Object { (Get-OptionalPropertyValue $_ 'productId') -eq 'Microsoft.VisualStudio.Product.Ssms' })
    $ssmsDescriptions = @($ssmsProducts | ForEach-Object { '{0} {1}' -f (Get-OptionalPropertyValue $_ 'displayName'), (Get-OptionalPropertyValue $_ 'installationVersion') })
    if ($ssmsProducts.Count -gt 0) {
        Add-Check 'SQL Server Management Studio' 'Optional: SSMS for administration' (($ssmsDescriptions -join ', ').Trim()) 'PASS' $false 'vswhere product metadata filtered to SSMS' 'None.'
    }
    else {
        Add-Check 'SQL Server Management Studio' 'Optional: SSMS for administration' 'Not detected' 'MISSING' $false 'vswhere product metadata filtered to SSMS' 'Install SSMS only if approved and needed; it is not required for foundation readiness.' -FoundationImpact 'NONE' -PersistenceImpact 'NONE' -PilotImpact 'NONE'
    }

    # 22. global.json must match a stable, actually installed .NET 10 SDK.
    $globalJsonPath = Join-Path $repoRoot 'global.json'
    if (Test-Path -LiteralPath $globalJsonPath -PathType Leaf) {
        try {
            $globalJson = Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json
            $sdkNodeProperty = $globalJson.PSObject.Properties['sdk']
            $sdkNode = if ($null -ne $sdkNodeProperty) { $sdkNodeProperty.Value } else { $null }
            $pinnedVersion = Get-OptionalPropertyValue $sdkNode 'version'
            $rollForward = Get-OptionalPropertyValue $sdkNode 'rollForward'
            $allowPrerelease = Get-OptionalPropertyValue $sdkNode 'allowPrerelease'
            $globalIsValid = ($pinnedVersion -match '^10\.\d+\.\d+$') -and ($stable10Sdks -contains $pinnedVersion) -and ($rollForward -eq 'latestPatch') -and ($allowPrerelease -eq 'False')
            if ($globalIsValid) {
                Add-Check 'global.json' 'Mandatory when stable .NET 10 is installed: exact installed SDK, latestPatch, no prerelease' ("version=$pinnedVersion; rollForward=$rollForward; allowPrerelease=$allowPrerelease") 'PASS' $false 'Get-Content global.json; dotnet --list-sdks' 'None.'
            }
            else {
                Add-Check 'global.json' 'Mandatory when stable .NET 10 is installed: exact installed SDK, latestPatch, no prerelease' ("version=$pinnedVersion; rollForward=$rollForward; allowPrerelease=$allowPrerelease") 'BLOCKING' $true 'Get-Content global.json; dotnet --list-sdks' 'Pin the newest stable installed .NET 10 SDK and disable prerelease roll-forward.'
            }
        }
        catch {
            Add-Check 'global.json' 'Mandatory when stable .NET 10 is installed: valid reproducible SDK selection' 'File exists but cannot be parsed' 'BLOCKING' $true 'Get-Content global.json | ConvertFrom-Json' 'Correct global.json syntax without inventing an SDK version.'
        }
    }
    elseif ($stable10Sdks.Count -gt 0) {
        Add-Check 'global.json' 'Mandatory when stable .NET 10 is installed: exact installed SDK, latestPatch, no prerelease' 'Missing although a stable .NET 10 SDK is installed' 'BLOCKING' $true 'Test-Path global.json; dotnet --list-sdks' 'Create global.json using the newest stable installed .NET 10 SDK.'
    }
    else {
        Add-Check 'global.json' 'Must be absent when no stable .NET 10 SDK is installed' 'Correctly absent because no stable .NET 10 SDK was detected' 'PASS' $false 'Test-Path global.json; dotnet --list-sdks' 'Install a stable .NET 10 SDK before creating global.json.'
    }

    # 23. Sprint 0 repository protection files.
    $requiredRootFiles = @('.gitignore', '.gitattributes', '.editorconfig', 'scripts/Verify-Environment.ps1')
    $missingRootFiles = @($requiredRootFiles | Where-Object { -not (Test-Path -LiteralPath (Join-Path $repoRoot $_) -PathType Leaf) })
    if ($missingRootFiles.Count -eq 0) {
        Add-Check 'Sprint 0 repository files' 'Mandatory: repository protections and reproducible audit script' ("$($requiredRootFiles.Count) of $($requiredRootFiles.Count) files present") 'PASS' $false 'Test-Path for root protection files and audit script' 'None.'
    }
    else {
        Add-Check 'Sprint 0 repository files' 'Mandatory: repository protections and reproducible audit script' ("$($missingRootFiles.Count) file(s) missing") 'BLOCKING' $true 'Test-Path for root protection files and audit script' 'Restore the missing Sprint 0 repository files.'
    }

    # 24. Sensitive-looking files: inspect names and Git classification only, never contents.
    $sensitivePattern = '(?i)(^|[\\/])\.env($|\.)|(^|[\\/]).*(secret|credential|password|private[-_]?key|token).*|\.(pfx|p12|pem|key|pvk|snk|mdf|ndf|ldf|bak|trn|dif)$'
    $gitDirectoryPrefix = (Join-Path $repoRoot '.git') + [System.IO.Path]::DirectorySeparatorChar
    $sensitiveCandidates = @(Get-ChildItem -LiteralPath $repoRoot -File -Force -Recurse -ErrorAction SilentlyContinue | Where-Object {
        -not $_.FullName.StartsWith($gitDirectoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)
    } | ForEach-Object {
        $relative = $_.FullName.Substring($repoRoot.Length).TrimStart([char[]]'\/')
        if ($relative -match $sensitivePattern) { $relative }
    })

    if ($sensitiveCandidates.Count -eq 0) {
        Add-Check 'Sensitive-looking local files' 'Mandatory: no tracked or exposed secret, certificate, database, or backup file' 'No sensitive-looking filename detected; contents were not read' 'PASS' $false 'Filename-only scan; .git internals excluded' 'None.'
    }
    elseif ($isGitRepository -and $null -ne $gitCommand) {
        $trackedSensitiveCount = 0
        $unignoredSensitiveCount = 0
        $ignoredSensitiveCount = 0
        foreach ($relativePath in $sensitiveCandidates) {
            $trackedResult = Invoke-NativeCapture $gitCommand.Source @('ls-files', '--error-unmatch', '--', $relativePath)
            if ($trackedResult.ExitCode -eq 0) {
                $trackedSensitiveCount++
                continue
            }
            $ignoredResult = Invoke-NativeCapture $gitCommand.Source @('check-ignore', '-q', '--', $relativePath)
            if ($ignoredResult.ExitCode -eq 0) {
                $ignoredSensitiveCount++
            }
            else {
                $unignoredSensitiveCount++
            }
        }

        $sensitiveSummary = "tracked=$trackedSensitiveCount; untracked-and-unignored=$unignoredSensitiveCount; ignored-local=$ignoredSensitiveCount; file contents not read"
        if ($trackedSensitiveCount -gt 0 -or $unignoredSensitiveCount -gt 0) {
            Add-Check 'Sensitive-looking local files' 'Mandatory: no tracked or exposed secret, certificate, database, or backup file' $sensitiveSummary 'BLOCKING' $true 'Filename-only scan plus git ls-files and git check-ignore' 'Remove sensitive material from the repository scope using an approved recoverable process; do not display its contents.'
        }
        else {
            Add-Check 'Sensitive-looking local files' 'Mandatory: no tracked or exposed secret, certificate, database, or backup file' $sensitiveSummary 'WARNING' $false 'Filename-only scan plus git ls-files and git check-ignore' 'Keep ignored local sensitive files outside the project whenever possible and never stage them.'
        }
    }
    else {
        Add-Check 'Sensitive-looking local files' 'Mandatory: no tracked or exposed secret, certificate, database, or backup file' ("$($sensitiveCandidates.Count) sensitive-looking filename(s); contents not read; Git classification unavailable") 'BLOCKING' $true 'Filename-only scan; Git unavailable' 'Initialize Git protections before handling any local sensitive material.'
    }

    # 25. Final Git state without exposing remote URLs.
    if ($isGitRepository -and $null -ne $gitCommand) {
        $finalBranchResult = Invoke-NativeCapture $gitCommand.Source @('branch', '--show-current')
        $finalBranch = Join-NonEmptyLines $finalBranchResult.Output
        $statusResult = Invoke-NativeCapture $gitCommand.Source @('status', '--short')
        $statusCount = @($statusResult.Output | Where-Object { ([string]$_).Trim() }).Count
        $remoteResult = Invoke-NativeCapture $gitCommand.Source @('remote')
        $remoteCount = @($remoteResult.Output | Where-Object { ([string]$_).Trim() }).Count
        $commitResult = Invoke-NativeCapture $gitCommand.Source @('rev-list', '--count', '--all')
        $commitCount = Join-NonEmptyLines $commitResult.Output
        if (-not $commitCount) { $commitCount = '0' }
        $gitSummary = "branch=$finalBranch; working-tree-entries=$statusCount; commits=$commitCount; remotes=$remoteCount"
        $gitFinalStatus = if ($remoteCount -eq 0) { 'PASS' } else { 'WARNING' }
        $gitFinalAction = if ($remoteCount -eq 0) { 'Review the visible Sprint 0 working tree; do not commit until explicitly approved.' } else { 'Confirm all remotes predated Sprint 0; this script intentionally does not print remote URLs.' }
        Add-Check 'Final Git state' 'Mandatory: known branch, visible review changes, no Sprint 0 commit/push/remote creation' $gitSummary $gitFinalStatus $false 'git branch --show-current; git status --short; git rev-list --count --all; git remote (names counted only)' $gitFinalAction
    }
    else {
        Add-Check 'Final Git state' 'Mandatory: known branch and reviewable working tree' 'Unavailable' 'BLOCKING' $true 'Read-only Git commands' 'Restore local Git availability.'
    }

    # Additional Sprint 0 guard: no solution or application source artifacts.
    $applicationExtensions = @('.sln', '.slnx', '.csproj', '.cs', '.razor', '.xaml', '.sql')
    $applicationArtifacts = @(Get-ChildItem -LiteralPath $repoRoot -File -Recurse -ErrorAction SilentlyContinue | Where-Object {
        (-not $_.FullName.StartsWith($gitDirectoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) -and
        ($applicationExtensions -contains $_.Extension.ToLowerInvariant())
    })
    if ($applicationArtifacts.Count -eq 0) {
        Add-Check 'Sprint 0 application artifacts' 'Mandatory: no solution, project, or application code during Sprint 0' 'No .sln, .slnx, .csproj, .cs, .razor, .xaml, or .sql file detected' 'PASS' $false 'Recursive extension-only inventory; .git internals excluded' 'None.'
    }
    else {
        Add-Check 'Sprint 0 application artifacts' 'Mandatory: no solution, project, or application code during Sprint 0' ("$($applicationArtifacts.Count) forbidden artifact(s) detected") 'BLOCKING' $true 'Recursive extension-only inventory; .git internals excluded' 'Review and remove only unauthorized Sprint 0 artifacts through an approved recoverable process.'
    }
}
catch {
    Add-Check 'Audit script execution' 'Mandatory: script completes all safe checks' ('Unexpected error: ' + $_.Exception.Message) 'BLOCKING' $true 'PowerShell exception handling' 'Correct the audit script or the failing read-only check, then rerun it.'
}
finally {
    Set-Location -LiteralPath $startingLocation
}

Write-Output ''
Write-Output 'Logiciel Pharmacie - Environment Audit'
Write-Output ('Repository: {0}' -f $repoRoot)
Write-Output ('Timestamp:  {0}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss K'))
Write-Output ''

foreach ($result in $results) {
    Write-Output ('[{0}] {1}' -f $result.Status, $result.Component)
    Write-Output ('  Requirement: {0}' -f $result.Requirement)
    Write-Output ('  Detected:    {0}' -f $result.Detected)
    Write-Output ('  Foundation:  {0}' -f $result.FoundationImpact)
    Write-Output ('  Persistence: {0}' -f $result.PersistenceImpact)
    Write-Output ('  Pilot:       {0}' -f $result.PilotImpact)
    Write-Output ('  Evidence:    {0}' -f $result.Evidence)
    Write-Output ('  Action:      {0}' -f $result.Action)
    Write-Output ''
}

$passCount = @($results | Where-Object { $_.Status -eq 'PASS' }).Count
$warningCount = @($results | Where-Object { $_.Status -eq 'WARNING' }).Count
$missingCount = @($results | Where-Object { $_.Status -eq 'MISSING' }).Count
$blockingCount = @($results | Where-Object { $_.Status -eq 'BLOCKING' }).Count

Write-Output ('Summary: PASS={0}; WARNING={1}; MISSING={2}; BLOCKING={3}' -f $passCount, $warningCount, $missingCount, $blockingCount)

$foundationReadiness = Get-ReadinessResult 'FoundationImpact'
$persistenceReadiness = Get-ReadinessResult 'PersistenceImpact'
$pilotReadiness = Get-ReadinessResult 'PilotImpact'

Write-Output ''
Write-Output ('FoundationReadiness = {0}' -f $foundationReadiness.Verdict)
Write-Output ('PersistenceReadiness = {0}' -f $persistenceReadiness.Verdict)
Write-Output ('PilotProductionReadiness = {0}' -f $pilotReadiness.Verdict)

if ($foundationReadiness.Blockers.Count -gt 0) {
    Write-Output ('FoundationBlockers = {0}' -f ($foundationReadiness.Blockers -join ', '))
}
if ($foundationReadiness.Warnings.Count -gt 0) {
    Write-Output ('FoundationWarnings = {0}' -f ($foundationReadiness.Warnings -join ', '))
}
if ($persistenceReadiness.Blockers.Count -gt 0) {
    Write-Output ('PersistenceBlockers = {0}' -f ($persistenceReadiness.Blockers -join ', '))
}
if ($persistenceReadiness.Warnings.Count -gt 0) {
    Write-Output ('PersistenceWarnings = {0}' -f ($persistenceReadiness.Warnings -join ', '))
}
if ($pilotReadiness.Blockers.Count -gt 0) {
    Write-Output ('PilotProductionBlockers = {0}' -f ($pilotReadiness.Blockers -join ', '))
}
if ($pilotReadiness.Warnings.Count -gt 0) {
    Write-Output ('PilotProductionWarnings = {0}' -f ($pilotReadiness.Warnings -join ', '))
}

if ($foundationReadiness.Verdict -eq 'REMEDIATION REQUIRED') {
    exit 2
}
elseif ($foundationReadiness.Verdict -eq 'READY WITH WARNINGS') {
    exit 1
}
else {
    exit 0
}
