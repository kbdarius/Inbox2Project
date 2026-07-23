$ErrorActionPreference = "Stop"

$repoDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$batPath = Join-Path $repoDir "Setup-Inbox2Project.bat"
$desktop = [Environment]::GetFolderPath("Desktop")
$shell = New-Object -ComObject WScript.Shell

if (-not (Test-Path $batPath)) {
    throw "Could not find setup file: $batPath"
}

$cmdPath = Join-Path $env:WINDIR "System32\cmd.exe"

function Create-Shortcut {
    param(
        [string]$Name,
        [string]$Args,
        [string]$Description
    )

    $shortcutPath = Join-Path $desktop $Name
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $cmdPath
    $shortcut.Arguments = "/C `"$batPath$Args`""
    $shortcut.WorkingDirectory = $repoDir
    $shortcut.IconLocation = "$env:SystemRoot\System32\shell32.dll,4"
    $shortcut.WindowStyle = 1
    $shortcut.Description = $Description
    $shortcut.Save()
    Write-Output "Created shortcut: $shortcutPath"
}

Create-Shortcut -Name "Inbox2Project Setup.lnk" -Args "" -Description "Run Inbox2Project setup (update, build, publish, install)"
Create-Shortcut -Name "Inbox2Project Build Only.lnk" -Args " /buildOnly" -Description "Run Inbox2Project setup (update + build only)"
Create-Shortcut -Name "Inbox2Project Publish Only.lnk" -Args " /publishOnly" -Description "Run Inbox2Project setup (update, build, publish)"
