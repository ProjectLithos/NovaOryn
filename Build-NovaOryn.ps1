[CmdletBinding()]
param(
    [string]$Project = "",
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release",
    [ValidateRange(5, 300)][int]$BootTimeoutSeconds = 30,
    [switch]$Run,
    [switch]$NoRun,
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

function Find-Firmware {
    param(
        [Parameter(Mandatory = $true)][string]$DisplayName,
        [AllowNull()][string]$RecordedPath,
        [Parameter(Mandatory = $true)][string]$QemuPath,
        [Parameter(Mandatory = $true)][string[]]$FileNames
    )

    if (-not [string]::IsNullOrWhiteSpace($RecordedPath)) {
        $expanded = [Environment]::ExpandEnvironmentVariables($RecordedPath)
        if (Test-Path -LiteralPath $expanded -PathType Leaf) {
            return (Resolve-Path -LiteralPath $expanded).Path
        }
    }

    $qemuDirectory = Split-Path -Parent $QemuPath
    $roots = @(
        $qemuDirectory,
        (Join-Path $qemuDirectory "share"),
        (Join-Path $qemuDirectory "share\qemu"),
        ([IO.Path]::GetFullPath((Join-Path $qemuDirectory "..\share"))),
        ([IO.Path]::GetFullPath((Join-Path $qemuDirectory "..\share\qemu"))),
        ([Environment]::ExpandEnvironmentVariables("%ProgramFiles%\qemu")),
        ([Environment]::ExpandEnvironmentVariables("%ProgramFiles(x86)%\qemu")),
        ([Environment]::ExpandEnvironmentVariables("%LOCALAPPDATA%\Programs\qemu"))
    ) | Select-Object -Unique

    foreach ($searchRoot in $roots) {
        if (-not (Test-Path -LiteralPath $searchRoot -PathType Container)) { continue }
        foreach ($fileName in $FileNames) {
            $direct = Join-Path $searchRoot $fileName
            if (Test-Path -LiteralPath $direct -PathType Leaf) {
                return (Resolve-Path -LiteralPath $direct).Path
            }
            $recursive = Get-ChildItem -LiteralPath $searchRoot -Filter $fileName -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($null -ne $recursive) {
                return $recursive.FullName
            }
        }
    }

    throw "$DisplayName was not found beside the QEMU installation. Run Install-NovaOrynToolchain.bat."
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

$nativeOutput = Join-Path $root "Artifacts\Native\x64"
New-Item -ItemType Directory -Path $nativeOutput -Force | Out-Null

Write-Host "[INFO] Assembling x64 UEFI entry objects."
& $nasm -f win64 (Join-Path $root "native\x64\Entry.asm") -o (Join-Path $nativeOutput "Entry.obj")
if ($LASTEXITCODE -ne 0) { throw "NASM failed for Entry.asm with exit code $LASTEXITCODE." }
& $nasm -f win64 (Join-Path $root "native\x64\Cpu.asm") -o (Join-Path $nativeOutput "Cpu.obj")
if ($LASTEXITCODE -ne 0) { throw "NASM failed for Cpu.asm with exit code $LASTEXITCODE." }
& $nasm -f win64 (Join-Path $root "native\x64\Runtime.asm") -o (Join-Path $nativeOutput "Runtime.obj")
if ($LASTEXITCODE -ne 0) { throw "NASM failed for Runtime.asm with exit code $LASTEXITCODE." }
& $nasm -f win64 (Join-Path $root "native\x64\Descriptors.asm") -o (Join-Path $nativeOutput "Descriptors.obj")
if ($LASTEXITCODE -ne 0) { throw "NASM failed for Descriptors.asm with exit code $LASTEXITCODE." }
& $nasm -f win64 (Join-Path $root "native\x64\Interrupts.asm") -o (Join-Path $nativeOutput "Interrupts.obj")
if ($LASTEXITCODE -ne 0) { throw "NASM failed for Interrupts.asm with exit code $LASTEXITCODE." }

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
$imageBuilder = Join-Path $root "src\NovaOryn.ImageBuilder\bin\$Configuration\net10.0\NovaOryn.ImageBuilder.dll"
$qemuLauncher = Join-Path $root "src\NovaOryn.QemuLauncher\bin\$Configuration\net10.0\NovaOryn.QemuLauncher.dll"
foreach ($tool in @(
    @{Name='NovaOryn.ManagedCompiler';Path=$compiler},
    @{Name='NovaOryn.Linker';Path=$linker},
    @{Name='NovaOryn.ImageBuilder';Path=$imageBuilder},
    @{Name='NovaOryn.QemuLauncher';Path=$qemuLauncher}
)) {
    if (-not (Test-Path -LiteralPath $tool.Path -PathType Leaf)) {
        throw "$($tool.Name) was not produced: $($tool.Path)"
    }
}

$projectCreator = Join-Path $root "src\NovaOryn.ProjectCreator\bin\$Configuration\net10.0\NovaOryn.ProjectCreator.dll"
if (-not (Test-Path -LiteralPath $projectCreator -PathType Leaf)) {
    throw "NovaOryn.ProjectCreator was not produced: $projectCreator"
}

$defaultKernelDirectory = Join-Path $env:USERPROFILE "Source\Repos\NovaOrynKernel"
$projectManifest = if ([string]::IsNullOrWhiteSpace($Project)) {
    Join-Path $defaultKernelDirectory "NovaOrynProject.json"
} elseif ([IO.Path]::IsPathRooted($Project)) {
    [IO.Path]::GetFullPath($Project)
} else {
    [IO.Path]::GetFullPath((Join-Path $root $Project))
}

if (-not (Test-Path -LiteralPath $projectManifest -PathType Leaf)) {
    Write-Host "[INFO] Creating the C# kernel project at $defaultKernelDirectory"
    & $dotnet $projectCreator create --output $defaultKernelDirectory --sdk-root $root
    if ($LASTEXITCODE -ne 0) { throw "C# kernel project creation failed with exit code $LASTEXITCODE." }
}
if (-not (Test-Path -LiteralPath $projectManifest -PathType Leaf)) {
    throw "NovaOryn project manifest was not found: $projectManifest"
}
Write-Host "[ OK ] C# kernel project manifest: $projectManifest"

$projectData = Get-Content -LiteralPath $projectManifest -Raw | ConvertFrom-Json
$projectDirectory = Split-Path -Parent $projectManifest
$outputDirectory = if ([IO.Path]::IsPathRooted([string]$projectData.OutputDirectory)) {
    [IO.Path]::GetFullPath([string]$projectData.OutputDirectory)
} else {
    [IO.Path]::GetFullPath((Join-Path $projectDirectory ([string]$projectData.OutputDirectory)))
}
$imagePath = Join-Path $outputDirectory (([string]$projectData.Name) + ".img")

$dry = @()
if ($DryRun) { $dry = @("--dry-run") }

& $dotnet $compiler compile $projectManifest --dotnet $dotnet --ilc $ilc --configuration $Configuration --sdk-root $root @dry
if ($LASTEXITCODE -ne 0) { throw "Managed compilation failed with exit code $LASTEXITCODE." }

& $dotnet $linker link $projectManifest --lld-link $lldLink --llvm-nm $llvmNm --nasm $nasm --native-root $nativeOutput @dry
if ($LASTEXITCODE -ne 0) { throw "Native link failed with exit code $LASTEXITCODE." }

& $dotnet $imageBuilder create $projectManifest --output $imagePath @dry
if ($LASTEXITCODE -ne 0) { throw "Bootable EFI image creation failed with exit code $LASTEXITCODE." }

if ($NoRun -or -not $Run) {
    Write-Host "[ OK ] NovaOryn x64 NativeAOT build and FAT32 image creation completed."
    if ($NoRun) {
        Write-Host "[INFO] QEMU launch was skipped because -NoRun was supplied."
    } else {
        Write-Host "[INFO] QEMU launch is disabled for a normal build. Supply -Run or use the Visual Studio Run command to launch it."
    }
    exit 0
}

$qemu = Find-Executable -DisplayName "QEMU x64 system emulator" -Candidates @(
    (Get-RecordedPath @("qemuSystemX64", "qemu-system-x86_64.exe")),
    "%ProgramFiles%\qemu\qemu-system-x86_64.exe",
    "%ProgramFiles(x86)%\qemu\qemu-system-x86_64.exe",
    "%LOCALAPPDATA%\Programs\qemu\qemu-system-x86_64.exe",
    "%LOCALAPPDATA%\Microsoft\WinGet\Links\qemu-system-x86_64.exe",
    ((Get-Command qemu-system-x86_64.exe -ErrorAction SilentlyContinue).Source)
)
$ovmfCode = Find-Firmware -DisplayName "x64 OVMF code firmware" -RecordedPath (Get-RecordedPath @("ovmfCodeX64", "ovmfCode")) -QemuPath $qemu -FileNames @("edk2-x86_64-code.fd", "OVMF_CODE.fd")
$ovmfVars = Find-Firmware -DisplayName "x64 OVMF variable-store template" -RecordedPath (Get-RecordedPath @("ovmfVarsX64", "ovmfVars")) -QemuPath $qemu -FileNames @("edk2-i386-vars.fd", "edk2-x86_64-vars.fd", "OVMF_VARS.fd")
Write-Host "[ OK ] qemu    : $qemu"
Write-Host "[ OK ] OVMF code: $ovmfCode"
Write-Host "[ OK ] OVMF vars: $ovmfVars"

& $dotnet $qemuLauncher run $projectManifest --qemu $qemu --image $imagePath --ovmf-code $ovmfCode --ovmf-vars $ovmfVars --timeout-seconds $BootTimeoutSeconds @dry
if ($LASTEXITCODE -ne 0) { throw "QEMU runtime acceptance failed with exit code $LASTEXITCODE." }

Write-Host "[ OK ] NovaOryn x64 NativeAOT boot-and-run acceptance completed."
