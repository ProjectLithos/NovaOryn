[CmdletBinding()]
param([ValidateSet("Debug", "Release")][string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnet = Join-Path $root ".toolchain\DotNet\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { throw "Pinned dotnet.exe was not found. Run Install-NovaOrynToolchain.bat." }
$project = Join-Path $root "src\NovaOryn.VisualStudio\NovaOryn.VisualStudio.csproj"
& $dotnet build $project --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "NovaOryn VSIX build failed with exit code $LASTEXITCODE." }
$vsix = Join-Path $root "src\NovaOryn.VisualStudio\bin\$Configuration\NovaOryn.VisualStudio.vsix"
if (-not (Test-Path -LiteralPath $vsix -PathType Leaf)) { throw "NovaOryn VSIX was not produced: $vsix" }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($vsix)
try {
    $requiredTemplateEntries = @(
        "ProjectTemplates/CSharp/1033/NovaOrynKernel/NovaOrynKernel.vstemplate",
        "ProjectTemplates/CSharp/1033/NovaOrynKernel/NovaOrynKernel.csproj",
        "ProjectTemplates/CSharp/1033/NovaOrynKernel/Kernel/Kernel.cs",
        "ProjectTemplates/CSharp/1033/NovaOrynKernel/Runtime/CoreLib.cs",
        "ProjectTemplates/CSharp/1033/NovaOrynKernel/Boot/BootContext.cs",
        "ProjectTemplates/CSharp/1033/NovaOrynKernel/Console/FramebufferConsole.cs",
        "ProjectTemplates/CSharp/1033/NovaOrynKernel/Console/BitmapFont.cs",
        "ProjectTemplates/CSharp/1033/NovaOrynKernel/NovaOrynProject.json"
    )
    $entryNames = @($archive.Entries | ForEach-Object { $_.FullName.Replace("\", "/") })
    foreach ($requiredEntry in $requiredTemplateEntries) {
        if ($entryNames -notcontains $requiredEntry) { throw "NovaOryn VSIX is missing project-template content: $requiredEntry" }
    }
} finally {
    $archive.Dispose()
}
$artifact = Join-Path $root "Artifacts\VisualStudio\NovaOryn.VisualStudio-0.0.41.vsix"
New-Item -ItemType Directory -Path (Split-Path -Parent $artifact) -Force | Out-Null
Copy-Item -LiteralPath $vsix -Destination $artifact -Force
Write-Host "[ OK ] NovaOryn VSIX: $artifact"
