[CmdletBinding()]
param([ValidateSet("Debug", "Release")][string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnet = Join-Path $root ".toolchain\DotNet\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { throw "Pinned dotnet.exe was not found. Run Install-NovaOrynToolchain.bat." }
$projectDirectory = Join-Path $root "src\NovaOryn.VisualStudio"
$project = Join-Path $projectDirectory "NovaOryn.VisualStudio.csproj"
$sourceManifest = Join-Path $projectDirectory "source.extension.vsixmanifest"
if (-not (Test-Path -LiteralPath $sourceManifest -PathType Leaf)) { throw "VSIX source manifest was not found: $sourceManifest" }
[xml]$sourceManifestXml = Get-Content -LiteralPath $sourceManifest -Raw
$expectedVersion = [string]$sourceManifestXml.PackageManifest.Metadata.Identity.Version
if ([string]::IsNullOrWhiteSpace($expectedVersion)) { throw "VSIX source manifest does not define Metadata/Identity/@Version." }
$templateSource = Join-Path $projectDirectory "ProjectTemplates\CSharp\1033\NovaOrynKernel\NovaOrynKernel.vstemplate"
if (-not (Test-Path -LiteralPath $templateSource -PathType Leaf)) { throw "NovaOryn project template was not found: $templateSource" }
[xml]$templateSourceXml = Get-Content -LiteralPath $templateSource -Raw
$templateNamespace = New-Object System.Xml.XmlNamespaceManager($templateSourceXml.NameTable)
$templateNamespace.AddNamespace("vst", "http://schemas.microsoft.com/developer/vstemplate/2005")
$templateName = [string]$templateSourceXml.SelectSingleNode("/vst:VSTemplate/vst:TemplateData/vst:Name", $templateNamespace).InnerText
$expectedTemplateName = "NovaOryn Kernel $expectedVersion"
if ($templateName -ne $expectedTemplateName) { throw "Project template name is '$templateName', expected '$expectedTemplateName'." }
foreach ($stale in @((Join-Path $projectDirectory "bin"), (Join-Path $projectDirectory "obj"))) {
    if (Test-Path -LiteralPath $stale) { Remove-Item -LiteralPath $stale -Recurse -Force }
}
& $dotnet build $project --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "NovaOryn VSIX build failed with exit code $LASTEXITCODE." }
$vsix = Join-Path $projectDirectory "bin\$Configuration\NovaOryn.VisualStudio.vsix"
if (-not (Test-Path -LiteralPath $vsix -PathType Leaf)) { throw "NovaOryn VSIX was not produced: $vsix" }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($vsix)
try {
    $entryNames = @($archive.Entries | ForEach-Object { $_.FullName.Replace("\", "/") })
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
    foreach ($requiredEntry in $requiredTemplateEntries) {
        if ($entryNames -notcontains $requiredEntry) { throw "NovaOryn VSIX is missing project-template content: $requiredEntry" }
    }
    $templateEntry = $archive.Entries | Where-Object { $_.FullName.Replace("\", "/") -eq "ProjectTemplates/CSharp/1033/NovaOrynKernel/NovaOrynKernel.vstemplate" } | Select-Object -First 1
    if ($null -eq $templateEntry) { throw "NovaOryn VSIX is missing NovaOrynKernel.vstemplate." }
    $templateReader = [IO.StreamReader]::new($templateEntry.Open())
    try { [xml]$builtTemplate = $templateReader.ReadToEnd() } finally { $templateReader.Dispose() }
    $builtTemplateNamespace = New-Object System.Xml.XmlNamespaceManager($builtTemplate.NameTable)
    $builtTemplateNamespace.AddNamespace("vst", "http://schemas.microsoft.com/developer/vstemplate/2005")
    $builtTemplateName = [string]$builtTemplate.SelectSingleNode("/vst:VSTemplate/vst:TemplateData/vst:Name", $builtTemplateNamespace).InnerText
    if ($builtTemplateName -ne $expectedTemplateName) { throw "Built VSIX template name is '$builtTemplateName', expected '$expectedTemplateName'." }
    $manifestEntry = $archive.Entries | Where-Object { $_.FullName -eq "extension.vsixmanifest" } | Select-Object -First 1
    if ($null -eq $manifestEntry) { throw "NovaOryn VSIX is missing extension.vsixmanifest." }
    $reader = [IO.StreamReader]::new($manifestEntry.Open())
    try { [xml]$builtManifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $builtIdentity = $builtManifest.PackageManifest.Metadata.Identity
    if ([string]$builtIdentity.Id -ne "NovaOryn.VisualStudio" -or [string]$builtIdentity.Version -ne $expectedVersion) {
        throw "Built VSIX identity/version is '$($builtIdentity.Id)' '$($builtIdentity.Version)', expected NovaOryn.VisualStudio $expectedVersion."
    }
} finally {
    $archive.Dispose()
}
$artifact = Join-Path $root "Artifacts\VisualStudio\NovaOryn.VisualStudio-$expectedVersion.vsix"
New-Item -ItemType Directory -Path (Split-Path -Parent $artifact) -Force | Out-Null
Copy-Item -LiteralPath $vsix -Destination $artifact -Force
Write-Host "[ OK ] NovaOryn VSIX: $artifact"
