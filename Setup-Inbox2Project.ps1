[CmdletBinding()]
param(
    [ValidateSet('Full', 'InstallLocal', 'BuildOnly', 'PublishOnly', 'SkipInstall', 'Release')]
    [string]$Mode = 'InstallLocal'
)

$ErrorActionPreference = 'Stop'

$repoDir = Split-Path -Parent $PSCommandPath
$publishedDir = Join-Path $repoDir 'artifacts\outlook-vsto-addin'
$releasePublishedDir = Join-Path $repoDir 'artifacts\release-package'
$installer = Join-Path $publishedDir 'Install-OutlookVstoAddIn.ps1'
$bridgeCsproj = Join-Path $repoDir 'src\Inbox2Project.OutlookBridge\Inbox2Project.OutlookBridge.csproj'
$outlookAddInKey = 'HKCU:\Software\Microsoft\Office\Outlook\Addins\Inbox2Project.OutlookVstoAddIn'

function Write-Section([string]$message) {
    Write-Host ''
    Write-Host '======================================='
    Write-Host $message
    Write-Host '======================================='
}

function Invoke-Tool([scriptblock]$script, [string]$errorMessage) {
    & $script
    if ($LASTEXITCODE -ne 0) {
        throw $errorMessage
    }
}

function Assert-Command([string]$name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "$name is not installed or not on PATH."
    }
}

function Get-BridgeVersion() {
    [xml]$xml = Get-Content -Path $bridgeCsproj
    $version = $xml.Project.PropertyGroup.Version
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "Version node missing in $bridgeCsproj"
    }

    return $version.Trim()
}

function Bump-BridgeVersion() {
    [xml]$xml = Get-Content -Path $bridgeCsproj
    $propertyGroup = $xml.Project.PropertyGroup
    $versionNode = $propertyGroup.Version
    $fileVersionNode = $propertyGroup.FileVersion
    $assemblyVersionNode = $propertyGroup.AssemblyVersion

    if ([string]::IsNullOrWhiteSpace($versionNode)) {
        throw "Version node missing in $bridgeCsproj"
    }

    $match = [regex]::Match($versionNode.Trim(), '^(\d+)\.(\d+)\.(\d+)$')
    if (-not $match.Success) {
        throw "Unsupported version format: $versionNode"
    }

    $nextVersion = '{0}.{1}.{2}' -f $match.Groups[1].Value, $match.Groups[2].Value, ([int]$match.Groups[3].Value + 1)
    $nextFileVersion = "$nextVersion.0"

    $propertyGroup.Version = $nextVersion
    $propertyGroup.FileVersion = $nextFileVersion
    $propertyGroup.AssemblyVersion = $nextFileVersion
    $xml.Save($bridgeCsproj)

    return $nextVersion
}

function Stop-LockingProcesses([bool]$includeOutlook) {
    $processNames = @('Inbox2Project.OutlookBridge', 'MSBuild', 'dotnet')
    if ($includeOutlook) {
        $processNames = @('OUTLOOK') + $processNames
    }

    foreach ($name in $processNames) {
        Get-Process -Name $name -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    }

    if ($includeOutlook) {
        $deadline = (Get-Date).AddSeconds(20)
        do {
            $outlook = Get-Process -Name OUTLOOK -ErrorAction SilentlyContinue
            if (-not $outlook) {
                break
            }

            Start-Sleep -Milliseconds 500
        } while ((Get-Date) -lt $deadline)

        if (Get-Process -Name OUTLOOK -ErrorAction SilentlyContinue) {
            throw 'Outlook is still running and may keep release files locked.'
        }
    }
}

function Sync-Repository([bool]$releaseMode) {
    Invoke-Tool { git switch main } 'Could not switch to main.'

    $script:beforeCommit = (git rev-parse --short HEAD).Trim()
    if ($releaseMode) {
        Invoke-Tool { git pull --rebase --autostash origin main } 'git pull --rebase --autostash failed.'
    }
    else {
        Invoke-Tool { git pull --ff-only origin main } 'git pull --ff-only failed.'
    }

    $script:afterCommit = (git rev-parse --short HEAD).Trim()
}

function Build-Solution([string]$configuration) {
    Invoke-Tool { dotnet build (Join-Path $repoDir 'Inbox2Project.sln') -c $configuration } 'Build failed.'
}

function Test-Solution([string]$configuration) {
    Invoke-Tool { dotnet test (Join-Path $repoDir 'tests\Inbox2Project.Tests\Inbox2Project.Tests.csproj') -c $configuration } 'Tests failed.'
}

function Publish-Package([string]$outputDirectory) {
    Invoke-Tool {
        powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoDir 'Publish-OutlookVstoAddIn.ps1') -OutputDirectory $outputDirectory
    } 'Publish-OutlookVstoAddIn.ps1 failed.'

    $requiredFiles = @(
        'Inbox2Project.OutlookVstoAddIn.dll',
        'Inbox2Project.OutlookBridge.exe',
        'Install-OutlookVstoAddIn.ps1'
    )

    foreach ($file in $requiredFiles) {
        if (-not (Test-Path (Join-Path $outputDirectory $file))) {
            throw "Published file not found: $file"
        }
    }
}

function Install-AddIn() {
    if (-not (Test-Path $installer)) {
        throw "Installer not found: $installer"
    }

    $command = "& { & '$installer' -AddInDirectory '$publishedDir'; exit `$LASTEXITCODE }"
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    $process = Start-Process -FilePath 'powershell.exe' `
        -Verb RunAs `
        -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', $encoded) `
        -Wait `
        -PassThru

    if ($process.ExitCode -ne 0) {
        throw "Installer failed with exit code $($process.ExitCode)."
    }
}

function Verify-AddInRegistration() {
    if (-not (Test-Path $outlookAddInKey)) {
        throw 'Inbox2Project Outlook add-in registry key was not found after install.'
    }

    $key = Get-ItemProperty $outlookAddInKey
    if ($key.LoadBehavior -ne 3) {
        throw "Inbox2Project LoadBehavior expected 3 but found $($key.LoadBehavior)."
    }

    [pscustomobject]@{
        FriendlyName = $key.FriendlyName
        Description = $key.Description
        LoadBehavior = $key.LoadBehavior
        CommandLineSafe = $key.CommandLineSafe
    }
}

function Start-OutlookIfNeeded([bool]$shouldStart) {
    if (-not $shouldStart) {
        return
    }

    Start-Process 'OUTLOOK.EXE' | Out-Null
}

function Commit-And-PushRelease([string]$version) {
    $releasePaths = @(
        '.github/instructions/inbox2project-release-workflow.instructions.md',
        '.github/workflows/release-guard.yml',
        'AGENTS.md',
        'Create-Inbox2ProjectShortcut.ps1',
        'Dariuse_Build_Release_Instructions.md',
        'Inbox2Project.sln',
        'RELEASE.md',
        'Setup-Inbox2Project.bat',
        'Setup-Inbox2Project.ps1',
        'src/Inbox2Project.OutlookBridge/BridgeProjectSelectorUi.cs',
        'src/Inbox2Project.OutlookBridge/OpenAiApiKeySetupForm.cs',
        'src/Inbox2Project.OutlookBridge/Program.cs',
        'src/Inbox2Project.OutlookBridge/ProjectSelectorForm.cs',
        'src/Inbox2Project.OutlookBridge/Inbox2Project.OutlookBridge.csproj',
        'src/Inbox2Project/Inbox2Project.csproj',
        'src/Inbox2Project/Models/SettingsModel.cs',
        'src/Inbox2Project/Services/FileNameBaseNameNormalizer.cs',
        'src/Inbox2Project/Services/OllamaFolderNameService.cs',
        'src/Inbox2Project/Services/OpenAiFolderNameService.cs',
        'src/Inbox2Project/Services/OpenAiBillingService.cs',
        'src/Inbox2Project/Services/PathSafetyService.cs',
        'src/Inbox2Project/Services/SettingsService.cs',
        'tests/Inbox2Project.Tests/Inbox2Project.Tests.csproj',
        'tests/Inbox2Project.Tests/FileNameBaseNameNormalizerTests.cs'
    )

    $untracked = @(git status --short --untracked-files=all | Where-Object { $_ -match '^\?\? ' } | ForEach-Object { $_.Substring(3) })
    $excludedLocalFiles = @('docs/github-models-api-integration-guide.md')
    $unexpected = @($untracked | Where-Object { $_ -notin $releasePaths -and $_ -notin $excludedLocalFiles })
    foreach ($excludedFile in $untracked | Where-Object { $_ -in $excludedLocalFiles }) {
        Write-Host "Leaving unrelated local file unstaged: $excludedFile"
    }
    if ($unexpected.Count -gt 0) {
        throw "Release stopped because unrelated untracked files were found: $($unexpected -join ', '). Review them, then rerun /release."
    }

    Invoke-Tool { git add -- $releasePaths } 'git add failed.'
    Invoke-Tool { git commit -m "chore(release): v$version" } 'git commit failed.'
    Invoke-Tool { git push origin main } 'git push origin main failed.'
}

$releaseMode = $Mode -eq 'Release'
$shouldInstall = $Mode -in @('InstallLocal', 'Full')
$shouldPublish = $Mode -notin @('BuildOnly')
$shouldRestartOutlook = $Mode -in @('InstallLocal', 'Full')
$shouldStopOutlook = $Mode -in @('InstallLocal', 'Full', 'PublishOnly', 'SkipInstall')
$publishOutputDirectory = if ($releaseMode) { $releasePublishedDir } else { $publishedDir }
$configuration = 'Release'

Write-Section 'Inbox2Project setup and release automation'
Write-Host "Repository: $repoDir"
Write-Host "Mode: $Mode"

Assert-Command 'git'
Assert-Command 'dotnet'
Assert-Command 'powershell'

Write-Host ''
Write-Host 'Step 1 - Syncing repository state...'
Sync-Repository -releaseMode:$releaseMode
Write-Host "Checked out commit: $beforeCommit"
Write-Host "Synced commit:      $afterCommit"

if ($releaseMode) {
    Write-Host ''
    Write-Host 'Step 2 - Bumping bridge version...'
    $currentVersion = Bump-BridgeVersion
    Write-Host "Bumped bridge version to $currentVersion"
}
else {
    $currentVersion = Get-BridgeVersion
    Write-Host ''
    Write-Host "Step 2 - Using bridge version $currentVersion"
}

Write-Host ''
Write-Host 'Step 3 - Building solution...'
Build-Solution -configuration $configuration

Write-Host ''
Write-Host 'Step 4 - Running automated tests...'
Test-Solution -configuration $configuration

if ($shouldPublish) {
    Write-Host ''
    if ($shouldStopOutlook) {
        Write-Host 'Step 5 - Stopping locking processes...'
        Stop-LockingProcesses -includeOutlook:$shouldStopOutlook
    }

    Write-Host ''
    Write-Host 'Step 6 - Publishing Outlook package...'
    Publish-Package -outputDirectory $publishOutputDirectory
}

if ($shouldInstall) {
    Write-Host ''
    Write-Host 'Step 7 - Installing Outlook add-in...'
    Install-AddIn

    Write-Host ''
    Write-Host 'Step 8 - Verifying add-in registration...'
    $registration = Verify-AddInRegistration
    $registration | Format-List | Out-Host

    Write-Host ''
    Write-Host 'Step 9 - Reopening Outlook...'
    Start-OutlookIfNeeded -shouldStart:$shouldRestartOutlook
}

if ($releaseMode) {
    Write-Host ''
    Write-Host 'Step 7 - Committing and pushing release...'
    Commit-And-PushRelease -version $currentVersion
}

Write-Section 'SUCCESS'
Write-Host "Bridge version: $currentVersion"
Write-Host "Published directory: $publishOutputDirectory"
if ($afterCommit) {
    Write-Host "Base synced commit: $afterCommit"
}
if ($releaseMode) {
    $releasedCommit = (git rev-parse --short HEAD).Trim()
    Write-Host "Released commit: $releasedCommit"
}
if ($shouldRestartOutlook) {
    Write-Host 'Classic Outlook was restarted.'
}
