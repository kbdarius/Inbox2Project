@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ============================================================================
rem Inbox2Project setup helper
rem - Updates repository from origin/main
rem - Builds solution in Release
rem - Publishes Outlook VSTO add-in + bridge
rem - Optionally bumps bridge version, commits, and pushes on release
rem - Runs installer with elevation
rem - Validates key outputs and prints next steps
rem ============================================================================
rem Supported optional modes:
rem   /skipInstall   - do not run COM/add-in installation
rem   /buildOnly     - skip publish and install (build only)
rem   /publishOnly   - update, build and publish, skip install
rem   /release       - bump version, publish, commit and push (no install)

set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set "REPO_DIR=%SCRIPT_DIR%"
set "PUBLISHED_DIR=%REPO_DIR%\artifacts\outlook-vsto-addin"
set "INSTALLER=%PUBLISHED_DIR%\Install-OutlookVstoAddIn.ps1"
set "BRIDGE_CSPROJ=%REPO_DIR%\src\Inbox2Project.OutlookBridge\Inbox2Project.OutlookBridge.csproj"

set "MODE=FULL"
if /I "%~1"=="/skipInstall" set "MODE=SKIP_INSTALL"
if /I "%~1"=="/buildOnly" set "MODE=BUILD_ONLY"
if /I "%~1"=="/publishOnly" set "MODE=PUBLISH_ONLY"
if /I "%~1"=="/release" set "MODE=RELEASE"

cd /d "%REPO_DIR%"

echo =======================================
echo Inbox2Project update and setup
echo =======================================
echo Repository: %REPO_DIR%
echo Mode: %MODE%

where git >nul 2>nul
if errorlevel 1 (
    echo ERROR: git is not installed or not on PATH.
    goto :fail
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: dotnet SDK/runtime is not installed or not on PATH.
    goto :fail
)

echo.

tasklist /FI "IMAGENAME eq Inbox2Project.OutlookBridge.exe" | findstr /I "Inbox2Project.OutlookBridge.exe" >nul 2>nul
if not errorlevel 1 (
    echo INFO: Stopping running Inbox2Project helper process to avoid locked output files.
    taskkill /F /IM Inbox2Project.OutlookBridge.exe >nul 2>nul
)

if /I "%MODE%"=="BUILD_ONLY" goto :buildMode
tasklist /FI "IMAGENAME eq OUTLOOK.EXE" | findstr /I "OUTLOOK.EXE" >nul 2>nul
if not errorlevel 1 (
    echo WARN: Outlook is currently running. Close Outlook before install or publish to avoid locked outputs.
)

:buildMode
echo Step 1/5 - Updating repository from origin/main...
git switch main
if errorlevel 1 (
    echo ERROR: Could not switch to main.
    goto :fail
)

for /f "delims=" %%H in ('git rev-parse --short HEAD') do set "BEFORE_COMMIT=%%H"
echo Checked Out Commit: %BEFORE_COMMIT%

git pull --ff-only origin main
if errorlevel 1 (
    echo ERROR: git pull failed. Check network and remote branch access.
    goto :fail
)

for /f "delims=" %%H in ('git rev-parse --short HEAD') do set "AFTER_COMMIT=%%H"
echo Pulled Commit: %AFTER_COMMIT%

if /I "%MODE%"=="RELEASE" (
    echo Step 2/6 - Bumping version before build...
) else (
    echo Step 2/5 - Building Inbox2Project.sln...
)
if /I "%MODE%"=="RELEASE" (
    for /f "delims=" %%V in ('powershell -NoProfile -ExecutionPolicy Bypass -Command "$path = '%BRIDGE_CSPROJ%'; $xml = New-Object xml; $xml.Load($path); $version = $xml.SelectSingleNode('//Project/PropertyGroup/Version'); $fileVersion = $xml.SelectSingleNode('//Project/PropertyGroup/FileVersion'); $assemblyVersion = $xml.SelectSingleNode('//Project/PropertyGroup/AssemblyVersion'); if (-not $version -or [string]::IsNullOrWhiteSpace($version.InnerText)) { throw 'Version node missing in ' + $path }; $m = [regex]::Match($version.InnerText.Trim(), '^(\\d+)\\.(\\d+)\\.(\\d+)$'); if (-not $m.Success) { throw 'Unsupported version format: ' + $version.InnerText.Trim() }; $nextVersion = ($m.Groups[1].Value + '.' + $m.Groups[2].Value + '.' + (([int]$m.Groups[3].Value) + 1)); $nextFileVersion = $nextVersion + '.0'; $version.InnerText = $nextVersion; $fileVersion.InnerText = $nextFileVersion; $assemblyVersion.InnerText = $nextFileVersion; $xml.Save($path); Write-Output $nextVersion;"') do set "CURRENT_VERSION=%%V"
    if errorlevel 1 (
        echo ERROR: Version bump failed.
        goto :fail
    )
) else (
    for /f "delims=" %%V in ('powershell -NoProfile -ExecutionPolicy Bypass -Command "$path = '%BRIDGE_CSPROJ%'; $xml = New-Object xml; $xml.Load($path); $version = $xml.SelectSingleNode('//Project/PropertyGroup/Version'); if (-not $version) { exit 1 }; Write-Output $version.InnerText.Trim()"') do set "CURRENT_VERSION=%%V"
)

if /I "%MODE%"=="RELEASE" (
    echo Bumped Bridge Version: %CURRENT_VERSION%
) else (
    echo Current Bridge Version: %CURRENT_VERSION%
)

if /I "%MODE%"=="RELEASE" (
    if "%CURRENT_VERSION%"=="" (
        echo ERROR: Could not read bumped version.
        goto :fail
    )
)

if /I "%MODE%"=="RELEASE" (
    echo Step 3/6 - Building Inbox2Project.sln...
) else (
    echo Step 2/5 - Building Inbox2Project.sln...
)

dotnet build Inbox2Project.sln -c Release
if errorlevel 1 (
    echo ERROR: Build failed. Fix build issues before continuing.
    goto :fail
)

if "%MODE%"=="BUILD_ONLY" (
    echo Mode: BUILD_ONLY. Skipping publish and install.
    goto :success
)

if /I "%MODE%"=="RELEASE" (
    echo Step 4/6 - Publishing Outlook add-in package...
) else (
    echo Step 3/5 - Publishing Outlook add-in package...
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%REPO_DIR%\Publish-OutlookVstoAddIn.ps1"
if errorlevel 1 (
    echo ERROR: Publish-OutlookVstoAddIn.ps1 failed.
    goto :fail
)

if not exist "%PUBLISHED_DIR%\Inbox2Project.OutlookVstoAddIn.dll" (
    echo ERROR: Published add-in DLL not found: %PUBLISHED_DIR%\Inbox2Project.OutlookVstoAddIn.dll
    goto :fail
)
if not exist "%PUBLISHED_DIR%\Inbox2Project.OutlookBridge.exe" (
    echo ERROR: Published bridge executable not found: %PUBLISHED_DIR%\Inbox2Project.OutlookBridge.exe
    goto :fail
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "Write-Output ('Published Bridge Version: ' + (Get-Item '%PUBLISHED_DIR%\\Inbox2Project.OutlookBridge.exe').VersionInfo.FileVersion)"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Write-Output ('Published Addin Version:  ' + (Get-Item '%PUBLISHED_DIR%\\Inbox2Project.OutlookVstoAddIn.dll').VersionInfo.FileVersion)"

if "%MODE%"=="PUBLISH_ONLY" (
    echo Mode: PUBLISH_ONLY. Skipping add-in install.
    goto :success
)
if "%MODE%"=="SKIP_INSTALL" (
    echo Mode: SKIP_INSTALL. Skipping add-in install.
    goto :success
)
if "%MODE%"=="RELEASE" (
    echo Step 6/6 - Committing release and pushing to GitHub...
    git add "src\Inbox2Project.OutlookBridge\ProjectSelectorForm.cs" "src\Inbox2Project.OutlookBridge\Inbox2Project.OutlookBridge.csproj" "src\Inbox2Project\Services\PathSafetyService.cs" "src\Inbox2Project\Services\OllamaFolderNameService.cs" "Setup-Inbox2Project.bat" "Create-Inbox2ProjectShortcut.ps1"
    if errorlevel 1 (
        echo ERROR: Could not stage release changes.
        goto :fail
    )

    git commit -m "chore(release): bump version to %CURRENT_VERSION%"
    if errorlevel 1 (
        echo ERROR: git commit failed.
        goto :fail
    )

    git push origin main
    if errorlevel 1 (
        echo ERROR: git push failed.
        goto :fail
    )

    echo Mode: RELEASE. Version bumped, published, committed, and pushed.
    goto :success
)

echo Step 4/5 - Installing Outlook add-in (UAC required)...
echo Outlook should be closed before install.
set "PS_ARGS=-NoProfile -ExecutionPolicy Bypass -File \"%INSTALLER%\" -AddInDirectory \"%PUBLISHED_DIR%\""
Start-Process powershell.exe -Verb RunAs -ArgumentList "%PS_ARGS%" -Wait
if errorlevel 1 (
    echo ERROR: Installer reported a problem. Check the elevated PowerShell output.
    goto :fail
)

echo Step 5/5 - Post-install checks...
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { $k = Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Office\\Outlook\\Addins\\Inbox2Project.OutlookVstoAddIn' -ErrorAction Stop; Write-Output ('FriendlyName=' + $k.FriendlyName); Write-Output ('LoadBehavior=' + $k.LoadBehavior); Write-Output 'SUCCESS: Add-in registry entry is present.' } catch { Write-Output 'WARN: Add-in registry key not found under HKCU. Install may have failed or not completed.' }"

:success
echo.
if "%MODE%"=="BUILD_ONLY" goto :successBuild
if "%MODE%"=="PUBLISH_ONLY" goto :successPublish
if "%MODE%"=="SKIP_INSTALL" goto :successSkipInstall
if "%MODE%"=="RELEASE" goto :successRelease
goto :successFull

:successBuild
echo =======================================
echo SUCCESS: Build-only flow completed.
echo Next:
echo 1) Run Setup-Inbox2Project.bat /publishOnly to publish add-in and bridge
echo =======================================
goto :reportVersion

:successPublish
echo =======================================
echo SUCCESS: Publish-only flow completed.
echo Next:
echo 1) Run Setup-Inbox2Project.bat /skipInstall to install add-in only
echo 2) Or run Setup-Inbox2Project.bat (no args) for full install
echo =======================================
goto :reportVersion

:successSkipInstall
echo =======================================
echo SUCCESS: Update/build/publish completed, install skipped.
echo Next:
echo 1) Run Setup-Inbox2Project.bat to perform install, or restart Outlook to test current install
echo =======================================
goto :reportVersion

:successRelease
echo =======================================
echo SUCCESS: Release flow completed.
echo 1) Version bumped and committed: v%CURRENT_VERSION%
echo 2) Git push to origin/main completed
echo =======================================
goto :reportVersion

:successFull
echo =======================================
echo SUCCESS: Update and setup completed.
echo Next:
echo 1) Open Classic Outlook (Try the new Outlook OFF)
echo 2) Right-click one email and confirm "Save to Inbox2Project" appears
echo 3) Test with a harmless message first
echo =======================================

:reportVersion
echo.
if "%CURRENT_VERSION%"=="" (
    for /f "delims=" %%V in ('powershell -NoProfile -ExecutionPolicy Bypass -Command "$path = '%BRIDGE_CSPROJ%'; $xml = New-Object xml; $xml.Load($path); $version = $xml.SelectSingleNode('//Project/PropertyGroup/Version'); if ($version -ne $null) { Write-Output $version.InnerText.Trim() }"') do set "CURRENT_VERSION=%%V"
)
echo Bridge Version: %CURRENT_VERSION%
if "%AFTER_COMMIT%"=="" (
    echo Version check: repository commit unavailable
) else (
    echo Version check: commit %AFTER_COMMIT%
)
powershell -NoProfile -ExecutionPolicy Bypass -Command "Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.MessageBox]::Show('Inbox2Project version %CURRENT_VERSION% is installed and ready.', 'Inbox2Project setup complete', [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null"
pause
exit /b 0

:fail
echo.
echo =======================================
echo FAILED: Setup did not complete.
echo Fix the error above and re-run this script.
echo =======================================
pause
exit /b 1
