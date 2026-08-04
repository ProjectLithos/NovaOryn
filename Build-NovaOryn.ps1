[CmdletBinding()]
param(
    [string]$Project = "examples\MinimalKernel\NovaOrynProject.json",
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release",
    [switch]$DryRun
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnet = Join-Path $root ".toolchain\DotNet\dotnet.exe"
$toolPathsFile = Join-Path $root ".toolchain\NovaOryn.ToolPaths.json"
if (-not (Test-Path $dotnet)) { throw "Pinned dotnet.exe was not found: $dotnet" }
if (-not (Test-Path $toolPathsFile)) { throw "Tool-path manifest was not found: $toolPathsFile" }
$paths = Get-Content $toolPathsFile -Raw | ConvertFrom-Json
$llvmRoot = Join-Path $root ".toolchain\LLVM\bin"
$lldLink = Join-Path $llvmRoot "lld-link.exe"
$llvmNm = Join-Path $llvmRoot "llvm-nm.exe"
$nasm = $paths.nasm
if (-not $nasm) { $nasm = (Get-Command nasm.exe -ErrorAction SilentlyContinue).Source }
foreach ($required in @($lldLink, $llvmNm, $nasm)) { if (-not $required -or -not (Test-Path $required)) { throw "Required build tool is unavailable: $required" } }
$projectManifest = Join-Path $root $Project
$nativeOutput = Join-Path $root "Artifacts\Native\x64"
New-Item -ItemType Directory -Path $nativeOutput -Force | Out-Null
Write-Host "[INFO] Assembling x64 UEFI entry objects."
& $nasm -f win64 (Join-Path $root "native\x64\Entry.asm") -o (Join-Path $nativeOutput "Entry.obj")
if ($LASTEXITCODE -ne 0) { throw "NASM failed for Entry.asm." }
& $nasm -f win64 (Join-Path $root "native\x64\Cpu.asm") -o (Join-Path $nativeOutput "Cpu.obj")
if ($LASTEXITCODE -ne 0) { throw "NASM failed for Cpu.asm." }
Write-Host "[INFO] Building NovaOryn executable tools."
& $dotnet build (Join-Path $root "NovaOryn.sln") --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "NovaOryn solution build failed." }
$compiler = Join-Path $root "src\NovaOryn.ManagedCompiler\bin\$Configuration\net10.0\NovaOryn.ManagedCompiler.dll"
$linker = Join-Path $root "src\NovaOryn.Linker\bin\$Configuration\net10.0\NovaOryn.Linker.dll"
$dry = @(); if ($DryRun) { $dry = @("--dry-run") }
& $dotnet $compiler compile $projectManifest --dotnet $dotnet --configuration $Configuration @dry
if ($LASTEXITCODE -ne 0) { throw "Managed compilation failed." }
& $dotnet $linker link $projectManifest --lld-link $lldLink --llvm-nm $llvmNm --native-root $nativeOutput @dry
if ($LASTEXITCODE -ne 0) { throw "Native link failed." }
Write-Host "[ OK ] NovaOryn x64 NativeAOT build completed."
