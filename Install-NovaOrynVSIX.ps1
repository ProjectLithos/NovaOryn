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
Write-Host "[INFO] Removing an older $extensionId installation, if present."
$uninstall = Start-Process -FilePath $installer -ArgumentList @('/quiet', "/uninstall:$extensionId") -Wait -PassThru
if ($uninstall.ExitCode -notin @(0, 1001, 2003)) {
    Write-Warning "VSIX uninstall returned exit code $($uninstall.ExitCode). Installation will still be attempted."
}

Write-Host "[INFO] Installing $extensionId $version."
$installArguments = @('/quiet', '/shutdownprocesses', '/force', $vsix)
$install = Start-Process -FilePath $installer -ArgumentList $installArguments -Wait -PassThru

if ($install.ExitCode -eq -1073741510) {
    throw "VSIXInstaller.exe was interrupted (Windows status 0xC000013A). Do not press Ctrl+C, ensure Visual Studio is closed, and run Install-NovaOrynVSIX.bat again."
}
if ($install.ExitCode -ne 0) {
    throw "VSIX installation failed with exit code $($install.ExitCode). VSIXInstaller logs are normally written under $env:TEMP."
}

Write-Host "[ OK ] $extensionId $version is installed. Restart Visual Studio before using it."
