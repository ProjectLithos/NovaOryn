[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release",
    [switch]$Strict
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnet = Join-Path $root ".toolchain\DotNet\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) {
    throw "Repository-pinned dotnet.exe was not found: $dotnet. Run Install-NovaOrynToolchain.bat first."
}

$project = Join-Path $root "src\NovaOryn.DocumentationGenerator\NovaOryn.DocumentationGenerator.csproj"
$config = Join-Path $root "docs\NovaOryn.Documentation.json"
Write-Host "[INFO] Building NovaOryn SDK documentation generator."
& $dotnet build $project --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Documentation generator build failed with exit code $LASTEXITCODE." }

$generator = Join-Path $root "src\NovaOryn.DocumentationGenerator\bin\$Configuration\net10.0\NovaOryn.DocumentationGenerator.dll"
if (-not (Test-Path -LiteralPath $generator -PathType Leaf)) {
    throw "Documentation generator was not produced: $generator"
}

$arguments = @("generate", "--root", $root, "--configuration", $config)
if ($Strict) { $arguments += "--validate" }
Write-Host "[INFO] Generating NovaOryn SDK usage site."
& $dotnet $generator @arguments
if ($LASTEXITCODE -ne 0) { throw "Documentation generation failed with exit code $LASTEXITCODE." }

$index = Join-Path $root "docs\site\index.html"
$search = Join-Path $root "docs\site\search-index.json"
if (-not (Test-Path -LiteralPath $index -PathType Leaf) -or -not (Test-Path -LiteralPath $search -PathType Leaf)) {
    throw "Documentation generator did not produce the required site outputs."
}
Write-Host "[ OK ] NovaOryn SDK usage site: $index"
