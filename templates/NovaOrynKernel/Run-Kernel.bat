@echo off
setlocal
set "SDK_ROOT=%NOVAORYN_SDK_ROOT%"
if not defined SDK_ROOT set "SDK_ROOT=C:\NovaOryn"
if not exist "%SDK_ROOT%\Build-NovaOryn.bat" (
    echo [FAIL] NovaOryn SDK was not found at "%SDK_ROOT%".
    echo [INFO] Set NOVAORYN_SDK_ROOT to the SDK directory and run this file again.
    exit /b 1
)
call "%SDK_ROOT%\Build-NovaOryn.bat" -Project "%~dp0NovaOrynProject.json" -Run
exit /b %ERRORLEVEL%
