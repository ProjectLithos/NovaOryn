[CmdletBinding()]
param([ValidateSet("Debug", "Release")][string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root "Build-NovaOrynVSIX.ps1") -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw "NovaOryn VSIX build failed with exit code $LASTEXITCODE." }
$vsix = Join-Path $root "Artifacts\VisualStudio\NovaOryn.VisualStudio-0.0.44.vsix"
if (-not (Test-Path -LiteralPath $vsix -PathType Leaf)) { throw "NovaOryn VSIX was not produced: $vsix" }
$installerCandidates = @(
    "$env:ProgramFiles\Microsoft Visual Studio\2022\Community\Common7\IDE\VSIXInstaller.exe",
    "$env:ProgramFiles\Microsoft Visual Studio\2022\Professional\Common7\IDE\VSIXInstaller.exe",
    "$env:ProgramFiles\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\VSIXInstaller.exe",
    "$env:ProgramFiles(x86)\Microsoft Visual Studio\Installer\resources\app\ServiceHub\Services\Microsoft.VisualStudio.Setup.Service\VSIXInstaller.exe"
)
$installer = $installerCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($installer)) {
    $installer = Get-ChildItem -Path "$env:ProgramFiles\Microsoft Visual Studio", "$env:ProgramFiles(x86)\Microsoft Visual Studio" -Filter VSIXInstaller.exe -File -Recurse -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName -First 1
}
if ([string]::IsNullOrWhiteSpace($installer)) { throw "VSIXInstaller.exe was not found in the installed Visual Studio products." }
Write-Host "[INFO] Removing an older NovaOryn.VisualStudio installation, if present."
$uninstall = Start-Process -FilePath $installer -ArgumentList @('/quiet', '/uninstall:NovaOryn.VisualStudio') -Wait -PassThru
if ($uninstall.ExitCode -notin @(0, 1001, 2003)) {
    Write-Warning "VSIX uninstall returned exit code $($uninstall.ExitCode). Installation will still be attempted."
}
Write-Host "[INFO] Installing NovaOryn.VisualStudio 0.0.44."
$install = Start-Process -FilePath $installer -ArgumentList @('/quiet', $vsix) -Wait -PassThru
if ($install.ExitCode -ne 0) { throw "VSIX installation failed with exit code $($install.ExitCode)." }
Write-Host "[ OK ] NovaOryn.VisualStudio 0.0.44 is installed. Restart Visual Studio before using it."
