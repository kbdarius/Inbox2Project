param(
    [string]$AddInDirectory = (Split-Path -Parent $MyInvocation.MyCommand.Path)
)

$clsid = '{0A6FD82C-4D15-49D4-ABDA-4C0E7BEF7E67}'
$key = "HKCU:\Software\Microsoft\Office\Outlook\Addins\Inbox2Project.OutlookAddIn"
$dll = Join-Path $AddInDirectory 'Inbox2Project.OutlookAddIn.dll'
$comhost = Join-Path $AddInDirectory 'Inbox2Project.OutlookAddIn.comhost.dll'

if (-not (Test-Path $dll) -or -not (Test-Path $comhost)) {
    throw "Build the Outlook add-in first. Expected DLL and comhost beside this script."
}

$regsvr = Start-Process -FilePath "$env:WINDIR\System32\regsvr32.exe" -ArgumentList @('/s', $comhost) -Wait -PassThru
if ($regsvr.ExitCode -ne 0) {
    throw "COM registration failed with exit code $($regsvr.ExitCode). Re-run this script from an elevated PowerShell window."
}
New-Item -Path $key -Force | Out-Null
New-ItemProperty -Path $key -Name '(Default)' -Value 'Inbox2Project Outlook Add-in' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $key -Name 'Description' -Value 'Adds Save to Inbox2Project to Outlook context menus.' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $key -Name 'FriendlyName' -Value 'Inbox2Project' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $key -Name 'LoadBehavior' -Value 3 -PropertyType DWord -Force | Out-Null
Write-Host "Installed Inbox2Project Outlook add-in registration for the current user. Restart Outlook."
