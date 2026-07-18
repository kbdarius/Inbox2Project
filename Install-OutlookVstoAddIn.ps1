param(
    [string]$AddInDirectory = (Split-Path -Parent $MyInvocation.MyCommand.Path)
)

$dll = Join-Path $AddInDirectory 'Inbox2Project.OutlookVstoAddIn.dll'
$regAsm = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe'
$addInKey = 'HKCU:\Software\Microsoft\Office\Outlook\Addins\Inbox2Project.OutlookVstoAddIn'
$resiliency = 'HKCU:\Software\Microsoft\Office\16.0\Outlook\Resiliency'

if (-not (Test-Path $dll)) { throw "Add-in DLL not found: $dll" }
if (-not (Test-Path $regAsm)) { throw "64-bit .NET Framework RegAsm was not found: $regAsm" }

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]$identity
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this installer from an elevated PowerShell window so RegAsm can register the COM class."
}

& $regAsm $dll /codebase
if ($LASTEXITCODE -ne 0) { throw "RegAsm failed with exit code $LASTEXITCODE." }

New-Item -Path $addInKey -Force | Out-Null
New-ItemProperty -Path $addInKey -Name '(Default)' -Value 'Inbox2Project Outlook Add-in' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $addInKey -Name 'Description' -Value 'Adds Save to Inbox2Project to Outlook email context menus.' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $addInKey -Name 'FriendlyName' -Value 'Inbox2Project' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $addInKey -Name 'LoadBehavior' -Value 3 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $addInKey -Name 'CommandLineSafe' -Value 0 -PropertyType DWord -Force | Out-Null

# Remove only resiliency values whose binary payload names Inbox2Project.
foreach ($subKeyName in @('DisabledItems', 'CrashingAddinList')) {
    $subKey = Join-Path $resiliency $subKeyName
    if (-not (Test-Path $subKey)) { continue }
    $properties = (Get-Item $subKey).Property
    foreach ($propertyName in $properties) {
        $value = (Get-ItemProperty $subKey -Name $propertyName).$propertyName
        $text = if ($value -is [byte[]]) { [Text.Encoding]::Unicode.GetString($value) } else { [string]$value }
        if ($text -match 'Inbox2Project|inbox2project') {
            Remove-ItemProperty -Path $subKey -Name $propertyName -ErrorAction SilentlyContinue
        }
    }
}

# The retired .NET 8 in-process add-in must not be loaded.
Remove-Item 'HKCU:\Software\Microsoft\Office\Outlook\Addins\Inbox2Project.OutlookAddIn' -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Installed Inbox2Project for 64-bit Classic Outlook. Restart Outlook before testing."
