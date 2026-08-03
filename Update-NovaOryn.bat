@echo off
setlocal EnableExtensions
set "SCRIPT=%~dp0Update-NovaOryn.ps1"
if not exist "%SCRIPT%" (
  echo [FAIL] Missing Update-NovaOryn.ps1 beside this batch file.
  exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" %*
set "EXITCODE=%ERRORLEVEL%"
endlocal & exit /b %EXITCODE%
