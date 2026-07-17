@echo off
setlocal
set EXE=%~dp0src\Inbox2Project.OutlookBridge\bin\Release\net8.0-windows\win-x64\publish\Inbox2Project.OutlookBridge.exe
if not exist "%EXE%" (
  echo Outlook bridge executable not found.
  echo Build it first with:
  echo dotnet publish src/Inbox2Project.OutlookBridge/Inbox2Project.OutlookBridge.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false
  exit /b 1
)
start "Inbox2Project Outlook Bridge" "%EXE%" %*
