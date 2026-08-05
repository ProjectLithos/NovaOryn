@echo off
setlocal
set "ROOT=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%Build-NovaOrynDocumentation.ps1" -Configuration Release
if errorlevel 1 exit /b %ERRORLEVEL%
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%Build-NovaOryn.ps1" %*
exit /b %ERRORLEVEL%
