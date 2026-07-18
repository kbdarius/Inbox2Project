param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts\outlook-vsto-addin')
)

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null

dotnet publish "$PSScriptRoot\src\Inbox2Project.OutlookVstoAddIn\Inbox2Project.OutlookVstoAddIn.csproj" `
    --configuration Release `
    --output $resolvedOutput
if ($LASTEXITCODE -ne 0) { throw "Outlook .NET Framework add-in publish failed." }

dotnet publish "$PSScriptRoot\src\Inbox2Project.OutlookBridge\Inbox2Project.OutlookBridge.csproj" `
    --configuration Release `
    --output $resolvedOutput
if ($LASTEXITCODE -ne 0) { throw "Outlook bridge publish failed." }

Copy-Item "$PSScriptRoot\Install-OutlookVstoAddIn.ps1" $resolvedOutput -Force
Write-Host "Published Outlook .NET Framework add-in and bridge to $resolvedOutput"
