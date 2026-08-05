[CmdletBinding()]
param([ValidateSet("Debug", "Release")][string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceManifest = Join-Path $root "src\NovaOryn.VisualStudio\source.extension.vsixmanifest"

if (-not (Test-Path -LiteralPath $sourceManifest -PathType Leaf)) {
    throw "VSIX source manifest was not found: $sourceManifest"
}

[xml]$sourceManifestXml = Get-Content -LiteralPath $sourceManifest -Raw
$identity = $sourceManifestXml.PackageManifest.Metadata.Identity
$extensionId = [string]$identity.Id
$version = [string]$identity.Version

if ([string]::IsNullOrWhiteSpace($extensionId)) {
    throw "VSIX source manifest does not define Metadata/Identity/@Id."
}
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "VSIX source manifest does not define Metadata/Identity/@Version."
}

& (Join-Path $root "Build-NovaOrynVSIX.ps1") -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "NovaOryn VSIX build failed with exit code $LASTEXITCODE."
}

$vsix = Join-Path $root "Artifacts\VisualStudio\NovaOryn.VisualStudio-$version.vsix"
if (-not (Test-Path -LiteralPath $vsix -PathType Leaf)) {
    throw "NovaOryn VSIX was not produced: $vsix"
}

$runningVisualStudio = @(Get-Process -Name devenv -ErrorAction SilentlyContinue)
if ($runningVisualStudio.Count -gt 0) {
    throw "Visual Studio is running. Save your work, close every Visual Studio window, and run Install-NovaOrynVSIX.bat again."
}

$installerCandidates = @(
    "$env:ProgramFiles\Microsoft Visual Studio\2022\Community\Common7\IDE\VSIXInstaller.exe",
    "$env:ProgramFiles\Microsoft Visual Studio\2022\Professional\Common7\IDE\VSIXInstaller.exe",
    "$env:ProgramFiles\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\VSIXInstaller.exe",
    "$env:ProgramFiles(x86)\Microsoft Visual Studio\Installer\resources\app\ServiceHub\Services\Microsoft.VisualStudio.Setup.Service\VSIXInstaller.exe"
)
$installer = $installerCandidates |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($installer)) {
    $searchRoots = @(
        "$env:ProgramFiles\Microsoft Visual Studio",
        "$env:ProgramFiles(x86)\Microsoft Visual Studio"
    ) | Where-Object { Test-Path -LiteralPath $_ -PathType Container }

    $installer = Get-ChildItem -Path $searchRoots -Filter VSIXInstaller.exe -File -Recurse -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty FullName -First 1
}

if ([string]::IsNullOrWhiteSpace($installer)) {
    throw "VSIXInstaller.exe was not found in the installed Visual Studio products."
}

Write-Host "[INFO] VSIX installer: $installer"
Write-Host "[INFO] Installing or upgrading $extensionId $version."
# Visual Studio has already been confirmed closed. Do not use /shutdownprocesses:
# on Visual Studio 2026 it can terminate the VSIX installer host with 0xC000013A.
# VSIXInstaller performs an in-place upgrade when the extension ID is unchanged.
$installArguments = @('/quiet', '/force', $vsix)
$install = Start-Process -FilePath $installer -ArgumentList $installArguments -Wait -PassThru

if ($install.ExitCode -eq -1073741510) {
    Write-Warning "Quiet VSIX installation was interrupted (Windows status 0xC000013A). Retrying with the installer UI so Visual Studio can display the actual result."
    $install = Start-Process -FilePath $installer -ArgumentList @('/force', $vsix) -Wait -PassThru
}
if ($install.ExitCode -ne 0) {
    throw "VSIX installation failed with exit code $($install.ExitCode). VSIXInstaller logs are normally written under $env:TEMP."
}

Write-Host "[ OK ] $extensionId $version is installed. Restart Visual Studio before using it."
