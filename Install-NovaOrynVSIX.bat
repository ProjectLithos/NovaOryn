@echo off
setlocal
call "%~dp0Build-NovaOrynVSIX.bat" Release
if errorlevel 1 exit /b %ERRORLEVEL%
set "VSIX=%~dp0Artifacts\VisualStudio\NovaOryn.VisualStudio-0.0.31.vsix"
start "" "%VSIX%"
exit /b 0
