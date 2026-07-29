@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "POWERSHELL_SCRIPT=%SCRIPT_DIR%Setup-Inbox2Project.ps1"

if not exist "%POWERSHELL_SCRIPT%" (
    echo ERROR: Could not find %POWERSHELL_SCRIPT%
    pause
    exit /b 1
)

set "MODE=InstallLocal"
if /I "%~1"=="/installLocal" set "MODE=InstallLocal"
if /I "%~1"=="/buildOnly" set "MODE=BuildOnly"
if /I "%~1"=="/publishOnly" set "MODE=PublishOnly"
if /I "%~1"=="/skipInstall" set "MODE=SkipInstall"
if /I "%~1"=="/release" set "MODE=Release"

powershell -NoProfile -ExecutionPolicy Bypass -File "%POWERSHELL_SCRIPT%" -Mode "%MODE%"
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
    echo.
    echo =======================================
    echo FAILED: Setup did not complete.
    echo =======================================
    pause
    exit /b %EXIT_CODE%
)

echo.
echo =======================================
echo SUCCESS: Setup completed.
echo =======================================
pause
exit /b 0
