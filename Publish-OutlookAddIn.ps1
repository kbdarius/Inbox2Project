param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts\outlook-addin')
)

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null

dotnet publish "$PSScriptRoot\src\Inbox2Project.OutlookAddIn\Inbox2Project.OutlookAddIn.csproj" `
    --configuration Release `
    --output $resolvedOutput
if ($LASTEXITCODE -ne 0) { throw "Outlook add-in publish failed." }

dotnet publish "$PSScriptRoot\src\Inbox2Project.OutlookBridge\Inbox2Project.OutlookBridge.csproj" `
    --configuration Release `
    --output $resolvedOutput
if ($LASTEXITCODE -ne 0) { throw "Outlook bridge publish failed." }

Copy-Item "$PSScriptRoot\src\Inbox2Project.OutlookAddIn\install-addin.ps1" $resolvedOutput -Force
Write-Host "Published Outlook add-in and bridge to $resolvedOutput"
