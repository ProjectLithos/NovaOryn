[CmdletBinding()]
param(
    [string]$Project = "examples\MinimalKernel\NovaOrynProject.json",
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Executable {
    param(
        [Parameter(Mandatory = $true)][string]$DisplayName,
        [AllowNull()][AllowEmptyCollection()][string[]]$Candidates
    )

    $usableCandidates = @($Candidates | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
    foreach ($candidate in $usableCandidates) {
        $expanded = [Environment]::ExpandEnvironmentVariables($candidate)
        if (Test-Path -LiteralPath $expanded -PathType Leaf) {
            return (Resolve-Path -LiteralPath $expanded).Path
        }
    }

    if ($usableCandidates.Count -eq 0) {
        throw "Required build tool is unavailable: $DisplayName. No usable candidate paths were supplied."
    }
    throw "Required build tool is unavailable: $DisplayName. Checked: $($usableCandidates -join ', ')"
}

$dotnet = Find-Executable -DisplayName ".NET SDK dotnet.exe" -Candidates @(
    (Join-Path $root ".toolchain\DotNet\dotnet.exe")
)

$toolPathsFile = Join-Path $root ".toolchain\NovaOryn.ToolPaths.json"
$paths = $null
if (Test-Path -LiteralPath $toolPathsFile -PathType Leaf) {
    try {
        $paths = Get-Content -LiteralPath $toolPathsFile -Raw | ConvertFrom-Json
    } catch {
        throw "Tool-path manifest is invalid: $toolPathsFile. $($_.Exception.Message)"
    }
}

function Get-RecordedPath {
    param([string[]]$Names)
    if ($null -eq $paths) { return $null }
    foreach ($name in $Names) {
        $property = $paths.PSObject.Properties[$name]
        if ($null -ne $property -and -not [string]::IsNullOrWhiteSpace([string]$property.Value)) {
            return [string]$property.Value
        }
    }
    return $null
}

$llvmRoot = Join-Path $root ".toolchain\LLVM\bin"
$lldLink = Find-Executable -DisplayName "LLD linker (lld-link.exe)" -Candidates @(
    (Join-Path $llvmRoot "lld-link.exe"),
    (Get-RecordedPath @("lld-link.exe", "lldLink"))
)
$llvmNm = Find-Executable -DisplayName "LLVM symbol tool (llvm-nm.exe)" -Candidates @(
    (Join-Path $llvmRoot "llvm-nm.exe"),
    (Get-RecordedPath @("llvm-nm.exe", "llvmNm"))
)
$nasm = Find-Executable -DisplayName "NASM assembler (nasm.exe)" -Candidates @(
    (Get-RecordedPath @("nasm.exe", "nasm", "nasmPath")),
    "%LOCALAPPDATA%\bin\NASM\nasm.exe",
    "%ProgramFiles%\NASM\nasm.exe",
    "%ProgramFiles(x86)%\NASM\nasm.exe",
    "%LOCALAPPDATA%\Microsoft\WinGet\Links\nasm.exe",
    ((Get-Command nasm.exe -ErrorAction SilentlyContinue).Source)
)
$toolchainManifestPath = Join-Path $root "toolchain\NovaOryn.Toolchain.json"
$toolchainManifest = Get-Content -LiteralPath $toolchainManifestPath -Raw | ConvertFrom-Json
$ilcVersion = [string]$toolchainManifest.nativeAot.packageVersion
$ilc = Find-Executable -DisplayName "NativeAOT compiler (ilc.exe)" -Candidates @(
    (Get-RecordedPath @("ilc", "ilc.exe", "ilcPath")),
    (Join-Path $root ".toolchain\NuGetPackages\runtime.win-x64.microsoft.dotnet.ilcompiler\$ilcVersion\tools\ilc.exe"),
    (Join-Path $env:USERPROFILE ".nuget\packages\runtime.win-x64.microsoft.dotnet.ilcompiler\$ilcVersion\tools\ilc.exe")
)


Write-Host "[ OK ] dotnet : $dotnet"
Write-Host "[ OK ] lld-link: $lldLink"
Write-Host "[ OK ] llvm-nm: $llvmNm"
Write-Host "[ OK ] nasm    : $nasm"
Write-Host "[ OK ] ilc     : $ilc"

$projectManifest = Join-Path $root $Project
if (-not (Test-Path -LiteralPath $projectManifest -PathType Leaf)) {
    throw "NovaOryn project manifest was not found: $projectManifest"
}

$nativeOutput = Join-Path $root "Artifacts\Native\x64"
New-Item -ItemType Directory -Path $nativeOutput -Force | Out-Null

Write-Host "[INFO] Assembling x64 UEFI entry objects."
& $nasm -f win64 (Join-Path $root "native\x64\Entry.asm") -o (Join-Path $nativeOutput "Entry.obj")
if ($LASTEXITCODE -ne 0) { throw "NASM failed for Entry.asm with exit code $LASTEXITCODE." }
& $nasm -f win64 (Join-Path $root "native\x64\Cpu.asm") -o (Join-Path $nativeOutput "Cpu.obj")
if ($LASTEXITCODE -ne 0) { throw "NASM failed for Cpu.asm with exit code $LASTEXITCODE." }
& $nasm -f win64 (Join-Path $root "native\x64\Runtime.asm") -o (Join-Path $nativeOutput "Runtime.obj")
if ($LASTEXITCODE -ne 0) { throw "NASM failed for Runtime.asm with exit code $LASTEXITCODE." }

Write-Host "[INFO] Building NovaOryn executable tools."
& $dotnet build (Join-Path $root "NovaOryn.sln") --configuration $Configuration --property:Platform="Any CPU" --nologo
if ($LASTEXITCODE -ne 0) { throw "NovaOryn solution build failed with exit code $LASTEXITCODE." }

Write-Host "[INFO] Running NovaOryn source-policy tests."
$sourcePolicyTests = Join-Path $root "tests\NovaOryn.SourcePolicy.Tests\bin\$Configuration\net10.0\NovaOryn.SourcePolicy.Tests.dll"
if (-not (Test-Path -LiteralPath $sourcePolicyTests -PathType Leaf)) { throw "NovaOryn source-policy test executable was not produced: $sourcePolicyTests" }
& $dotnet $sourcePolicyTests
if ($LASTEXITCODE -ne 0) { throw "NovaOryn source-policy tests failed with exit code $LASTEXITCODE." }

$compiler = Join-Path $root "src\NovaOryn.ManagedCompiler\bin\$Configuration\net10.0\NovaOryn.ManagedCompiler.dll"
$linker = Join-Path $root "src\NovaOryn.Linker\bin\$Configuration\net10.0\NovaOryn.Linker.dll"
foreach ($tool in @(@{Name='NovaOryn.ManagedCompiler';Path=$compiler}, @{Name='NovaOryn.Linker';Path=$linker})) {
    if (-not (Test-Path -LiteralPath $tool.Path -PathType Leaf)) {
        throw "$($tool.Name) was not produced: $($tool.Path)"
    }
}

$dry = @()
if ($DryRun) { $dry = @("--dry-run") }

& $dotnet $compiler compile $projectManifest --dotnet $dotnet --ilc $ilc --configuration $Configuration @dry
if ($LASTEXITCODE -ne 0) { throw "Managed compilation failed with exit code $LASTEXITCODE." }

& $dotnet $linker link $projectManifest --lld-link $lldLink --llvm-nm $llvmNm --nasm $nasm --native-root $nativeOutput @dry
if ($LASTEXITCODE -ne 0) { throw "Native link failed with exit code $LASTEXITCODE." }

Write-Host "[ OK ] NovaOryn x64 NativeAOT build completed."
