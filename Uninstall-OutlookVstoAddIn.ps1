param(
    [string]$AddInDirectory = (Split-Path -Parent $MyInvocation.MyCommand.Path)
)

$dll = Join-Path $AddInDirectory 'Inbox2Project.OutlookVstoAddIn.dll'
$regAsm = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe'
$addInKey = 'HKCU:\Software\Microsoft\Office\Outlook\Addins\Inbox2Project.OutlookVstoAddIn'

if (Test-Path $dll) {
    & $regAsm $dll /unregister
    if ($LASTEXITCODE -ne 0) { throw "RegAsm unregister failed with exit code $LASTEXITCODE." }
}

Remove-Item $addInKey -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Uninstalled Inbox2Project from Classic Outlook. Restart Outlook if it is running."
