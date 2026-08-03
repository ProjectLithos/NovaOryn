$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step([string]$Message) { Write-Host "[INFO] $Message" }
function Write-Ok([string]$Message) { Write-Host "[ OK ] $Message" }
function Fail([string]$Message) { throw "[FAIL] $Message" }

function Invoke-Checked([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) { Fail "$FilePath failed with exit code $LASTEXITCODE." }
}

function Get-CommandPath([string]$Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) { return $null }
    return $command.Source
}

function Test-VersionOutput([string]$Executable, [string]$ExpectedText) {
    if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { return $false }
    try {
        $output = (& $Executable --version 2>&1 | Out-String)
        return $output -match [regex]::Escape($ExpectedText)
    } catch { return $false }
}

function Install-DotNet([string]$RepositoryRoot, [pscustomobject]$Manifest) {
    $installRoot = Join-Path $RepositoryRoot $Manifest.dotNetSdk.installDirectory
    $dotnet = Join-Path $installRoot 'dotnet.exe'
    if (Test-VersionOutput $dotnet $Manifest.dotNetSdk.version) {
        Write-Ok ".NET SDK $($Manifest.dotNetSdk.version) is already valid."
        return
    }

    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    $installer = Join-Path $env:TEMP 'NovaOryn-dotnet-install.ps1'
    Write-Step "Downloading the official .NET installer."
    Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
    Write-Step "Installing .NET SDK $($Manifest.dotNetSdk.version)."
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Version $Manifest.dotNetSdk.version -InstallDir $installRoot -NoPath 2>&1 | ForEach-Object { Write-Host $_ }
    $installExitCode = $LASTEXITCODE
    if ($installExitCode -ne 0 -or -not (Test-VersionOutput $dotnet $Manifest.dotNetSdk.version)) {
        Fail 'The pinned .NET SDK could not be installed or validated.'
    }
    Write-Ok ".NET SDK $($Manifest.dotNetSdk.version) installed."
}


function Install-NativeAot([string]$RepositoryRoot, [string]$DotNet, [pscustomobject]$Manifest) {
    $project = Join-Path $RepositoryRoot 'toolchain\NovaOryn.NativeAot.Bootstrap.csproj'
    $packages = Join-Path $RepositoryRoot $Manifest.nativeAot.packageDirectory
    $ilcPackage = Join-Path $packages ("microsoft.dotnet.ilcompiler\" + $Manifest.nativeAot.packageVersion)
    $runtimePackage = Join-Path $packages ("microsoft.netcore.app.runtime.nativeaot.win-x64\" + $Manifest.nativeAot.packageVersion)
    if ((Test-Path $ilcPackage) -and (Test-Path $runtimePackage)) {
        Write-Ok "NativeAOT/ILC packages $($Manifest.nativeAot.packageVersion) are already present."
        return
    }
    New-Item -ItemType Directory -Path $packages -Force | Out-Null
    Write-Step "Restoring NativeAOT/ILC runtime packs $($Manifest.nativeAot.packageVersion) through the .NET SDK."
    Invoke-Checked $DotNet @(
        'restore',
        $project,
        '--runtime',
        'win-x64',
        '--packages',
        $packages,
        '--nologo',
        '/p:PublishAot=true',
        '/p:SelfContained=true',
        "/p:RuntimeFrameworkVersion=$($Manifest.nativeAot.packageVersion)",
        '/p:TargetLatestRuntimePatch=false'
    )
    if (-not (Test-Path $ilcPackage) -or -not (Test-Path $runtimePackage)) {
        Fail 'NativeAOT/ILC packages were not restored to the pinned package directory.'
    }
    Write-Ok 'NativeAOT/ILC runtime packs installed.'
}

function Install-LlvmTools([string]$RepositoryRoot, [pscustomobject]$Manifest) {
    $installRoot = Join-Path $RepositoryRoot $Manifest.llvm.installDirectory
    $binRoot = Join-Path $installRoot 'bin'
    $allPresent = $true
    foreach ($tool in $Manifest.llvm.requiredTools) {
        if (-not (Test-Path -LiteralPath (Join-Path $binRoot $tool) -PathType Leaf)) { $allPresent = $false; break }
    }
    if ($allPresent -and (Test-VersionOutput (Join-Path $binRoot 'ld.lld.exe') $Manifest.llvm.version)) {
        Write-Ok "LLD and required LLVM utilities $($Manifest.llvm.version) are already valid."
        return
    }

    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    $installer = Join-Path $env:TEMP ("LLVM-" + $Manifest.llvm.version + '-win64.exe')
    $url = "https://github.com/llvm/llvm-project/releases/download/llvmorg-$($Manifest.llvm.version)/LLVM-$($Manifest.llvm.version)-win64.exe"
    Write-Step "Downloading the official LLVM Windows distribution $($Manifest.llvm.version)."
    Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $installer
    Write-Step 'Installing LLD and LLVM utilities into the repository-local toolchain.'
    $process = Start-Process -FilePath $installer -ArgumentList @('/S', "/D=$installRoot") -Wait -PassThru
    if ($process.ExitCode -ne 0) { Fail "LLVM installer failed with exit code $($process.ExitCode)." }
    foreach ($tool in $Manifest.llvm.requiredTools) {
        if (-not (Test-Path -LiteralPath (Join-Path $binRoot $tool) -PathType Leaf)) { Fail "Required LLVM tool is missing: $tool" }
    }
    if ($null -ne $Manifest.llvm.optionalTools) {
        foreach ($tool in $Manifest.llvm.optionalTools) {
            if (Test-Path -LiteralPath (Join-Path $binRoot $tool) -PathType Leaf) {
                Write-Ok "Optional LLVM tool is available: $tool"
            } else {
                Write-Step "Optional LLVM tool is unavailable and is not required: $tool"
            }
        }
    }
    if (-not (Test-VersionOutput (Join-Path $binRoot 'ld.lld.exe') $Manifest.llvm.version)) { Fail 'LLD version validation failed.' }
    Write-Ok 'LLD and required LLVM utilities installed.'
}

function Ensure-WingetTool([string]$DisplayName, [string]$CommandName, [string]$WingetId) {
    $existing = Get-CommandPath $CommandName
    if ($null -ne $existing) { Write-Ok "$DisplayName already exists: $existing"; return }
    $winget = Get-CommandPath 'winget.exe'
    if ($null -eq $winget) { Fail "$DisplayName is missing and winget.exe is unavailable." }
    Write-Step "Installing $DisplayName with winget."
    Invoke-Checked $winget @('install', '--id', $WingetId, '--exact', '--accept-package-agreements', '--accept-source-agreements', '--silent')
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$machinePath;$userPath"
    if ($null -eq (Get-CommandPath $CommandName)) { Fail "$DisplayName was installed but $CommandName is not discoverable." }
    Write-Ok "$DisplayName installed."
}

try {
    $repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $manifestPath = Join-Path $repositoryRoot 'toolchain\NovaOryn.Toolchain.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { Fail "Missing toolchain manifest: $manifestPath" }
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot '.git') -PathType Container)) { Fail 'The source must be committed in a Git repository before installing the toolchain.' }
    & git.exe -C $repositoryRoot diff --quiet
    if ($LASTEXITCODE -ne 0) { Fail 'The repository has uncommitted source changes.' }
    & git.exe -C $repositoryRoot diff --cached --quiet
    if ($LASTEXITCODE -ne 0) { Fail 'The repository has staged but uncommitted source changes.' }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    New-Item -ItemType Directory -Path (Join-Path $repositoryRoot '.toolchain') -Force | Out-Null
    Install-DotNet $repositoryRoot $manifest
    $dotnet = Join-Path (Join-Path $repositoryRoot $manifest.dotNetSdk.installDirectory) 'dotnet.exe'
    Install-NativeAot $repositoryRoot $dotnet $manifest
    Install-LlvmTools $repositoryRoot $manifest
    Ensure-WingetTool 'QEMU' 'qemu-system-x86_64.exe' $manifest.qemu.wingetId
    Ensure-WingetTool 'NASM' 'nasm.exe' $manifest.nasm.wingetId
    Write-Ok 'NovaOryn toolchain validation completed.'
    exit 0
} catch {
    Write-Host $_.Exception.Message
    exit 1
}
