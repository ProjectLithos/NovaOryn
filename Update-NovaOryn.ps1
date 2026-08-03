param(
    [Parameter(Position = 0)]
    [string]$ArchiveFolder
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step([string]$Message) { Write-Host "[INFO] $Message" }
function Write-Ok([string]$Message) { Write-Host "[ OK ] $Message" }
function Fail([string]$Message) { throw "[FAIL] $Message" }

function Get-VersionFromName([string]$Name, [string]$Kind) {
    $pattern = '^NovaOryn-' + [regex]::Escape($Kind) + '-(?<version>\d+\.\d+\.\d+)\.zip$'
    $match = [regex]::Match($Name, $pattern, [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success) { return $null }
    return [version]$match.Groups['version'].Value
}

function Find-LatestArchive([string[]]$Folders, [string]$Kind) {
    $matches = foreach ($folder in ($Folders | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $folder -PathType Container)) { continue }
        Get-ChildItem -LiteralPath $folder -File -Filter "NovaOryn-$Kind-*.zip" | ForEach-Object {
            $version = Get-VersionFromName $_.Name $Kind
            if ($null -ne $version) { [pscustomobject]@{ File = $_; Version = $version } }
        }
    }
    return $matches | Sort-Object Version -Descending | Select-Object -First 1
}

function Test-RepositoryHasCommit([string]$RepositoryRoot) {
    if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot '.git') -PathType Container)) { return $false }

    $standardOutput = Join-Path ([IO.Path]::GetTempPath()) ('NovaOrynGitOut-' + [guid]::NewGuid().ToString('N') + '.txt')
    $standardError = Join-Path ([IO.Path]::GetTempPath()) ('NovaOrynGitErr-' + [guid]::NewGuid().ToString('N') + '.txt')

    try {
        $process = Start-Process `
            -FilePath 'git.exe' `
            -ArgumentList @('-C', $RepositoryRoot, 'rev-parse', '--verify', '--quiet', 'HEAD') `
            -Wait `
            -PassThru `
            -NoNewWindow `
            -RedirectStandardOutput $standardOutput `
            -RedirectStandardError $standardError

        return $process.ExitCode -eq 0
    } finally {
        Remove-Item -LiteralPath $standardOutput -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $standardError -Force -ErrorAction SilentlyContinue
    }
}

function Ensure-Repository([string]$RepositoryRoot, [string]$RemoteUrl) {
    New-Item -ItemType Directory -Path $RepositoryRoot -Force | Out-Null
    if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot '.git') -PathType Container)) {
        Write-Step 'Initialising the Git repository.'
        & git.exe -C $RepositoryRoot init -b main
        if ($LASTEXITCODE -ne 0) { Fail 'git init failed.' }
    }
    $remoteNames = @(& git.exe -C $RepositoryRoot remote)
    if ($LASTEXITCODE -ne 0) { Fail 'Could not list Git remotes.' }

    if ($remoteNames -notcontains 'origin') {
        & git.exe -C $RepositoryRoot remote add origin $RemoteUrl
        if ($LASTEXITCODE -ne 0) { Fail 'Could not add the origin remote.' }
        Write-Ok "Added origin remote: $RemoteUrl"
        return
    }

    $origin = (& git.exe -C $RepositoryRoot remote get-url origin | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { Fail 'Could not read the origin remote.' }
    if ($origin -ne $RemoteUrl) {
        Fail "The origin remote is '$origin', not '$RemoteUrl'."
    }
}

function Expand-SourceArchive([string]$ArchivePath, [string]$RepositoryRoot) {
    $temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('NovaOrynUpdate-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    try {
        Write-Step "Extracting $([IO.Path]::GetFileName($ArchivePath))."
        Expand-Archive -LiteralPath $ArchivePath -DestinationPath $temporaryRoot -Force
        if (Test-Path -LiteralPath (Join-Path $temporaryRoot '.git')) { Fail 'The archive must not contain a .git directory.' }
        $rootItems = @(Get-ChildItem -LiteralPath $temporaryRoot -Force)
        if ($rootItems.Count -eq 1 -and $rootItems[0].PSIsContainer) { Fail 'The archive has an enclosing top-level directory.' }
        Get-ChildItem -LiteralPath $temporaryRoot -Force | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $RepositoryRoot -Recurse -Force
        }
    } finally {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Assert-CleanRepository([string]$RepositoryRoot) {
    $status = & git.exe -C $RepositoryRoot status --porcelain
    if ($LASTEXITCODE -ne 0) { Fail 'Could not inspect repository status.' }
    if (-not [string]::IsNullOrWhiteSpace(($status -join "`n"))) { Fail 'C:\NovaOryn has uncommitted changes.' }
}

function Clear-UncommittedInitialTree([string]$RepositoryRoot) {
    Get-ChildItem -LiteralPath $RepositoryRoot -Force | Where-Object Name -ne '.git' | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Recurse -Force
    }
}

function Apply-ChangeManifest([string]$RepositoryRoot) {
    $manifestPath = Join-Path $RepositoryRoot 'NovaOryn-Changes.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { return }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    foreach ($relativePath in @($manifest.deletedFiles)) {
        if ([string]::IsNullOrWhiteSpace([string]$relativePath)) { continue }
        $target = Join-Path $RepositoryRoot ([string]$relativePath)
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force; Write-Step "Deleted: $relativePath" }
    }
    foreach ($rename in @($manifest.renamedFiles)) {
        if ($null -eq $rename) { continue }
        $oldPath = Join-Path $RepositoryRoot ([string]$rename.from)
        $newPath = Join-Path $RepositoryRoot ([string]$rename.to)
        if (Test-Path -LiteralPath $oldPath) {
            if (Test-Path -LiteralPath $newPath) { Remove-Item -LiteralPath $oldPath -Recurse -Force }
            else {
                $newParent = Split-Path -Parent $newPath
                if (-not (Test-Path -LiteralPath $newParent)) { New-Item -ItemType Directory -Path $newParent -Force | Out-Null }
                Move-Item -LiteralPath $oldPath -Destination $newPath -Force
            }
            Write-Step "Renamed: $($rename.from) -> $($rename.to)"
        }
    }
}

try {
    $null = Get-Command git.exe -ErrorAction Stop
    $repositoryRoot = 'C:\NovaOryn'
    $remoteUrl = 'https://github.com/ProjectLithos/NovaOryn.git'
    $scriptFolder = Split-Path -Parent $MyInvocation.MyCommand.Path
    $downloadsFolder = Join-Path $env:USERPROFILE 'Downloads'
    $archiveFolders = @($scriptFolder, $downloadsFolder)
    if (-not [string]::IsNullOrWhiteSpace($ArchiveFolder)) {
        if (-not (Test-Path -LiteralPath $ArchiveFolder -PathType Container)) { Fail "Archive folder does not exist: $ArchiveFolder" }
        $archiveFolders = @($ArchiveFolder)
    }

    $hasCommit = Test-RepositoryHasCommit $repositoryRoot
    $archiveKind = if ($hasCommit) { 'ChangedFiles' } else { 'FullSource' }
    $latest = Find-LatestArchive $archiveFolders $archiveKind
    if ($null -eq $latest) { Fail "No valid NovaOryn-$archiveKind-x.y.z.zip archive was found. Checked: $($archiveFolders -join ', ')" }

    Write-Ok "Selected $($latest.File.Name)."
    Ensure-Repository $repositoryRoot $remoteUrl
    if ($hasCommit) { Assert-CleanRepository $repositoryRoot } else { Clear-UncommittedInitialTree $repositoryRoot }
    Expand-SourceArchive $latest.File.FullName $repositoryRoot
    if ($hasCommit) { Apply-ChangeManifest $repositoryRoot }

    & git.exe -C $repositoryRoot add -A
    if ($LASTEXITCODE -ne 0) { Fail 'git add failed.' }
    & git.exe -C $repositoryRoot diff --cached --quiet
    if ($LASTEXITCODE -eq 0) { Write-Ok 'The archive produced no source changes. No commit was created.'; exit 0 }
    if ($LASTEXITCODE -ne 1) { Fail 'Could not inspect the staged changes.' }

    $commitKind = if ($hasCommit) { 'Update' } else { 'Initial source' }
    $commitMessage = "$commitKind NovaOryn to $($latest.Version)"
    Write-Step "Creating commit: $commitMessage"
    & git.exe -C $repositoryRoot commit -m $commitMessage
    if ($LASTEXITCODE -ne 0) { Fail 'git commit failed. Configure Git user.name and user.email, then run the batch again.' }

    Write-Ok "Committed $($latest.File.Name) to $repositoryRoot."
    Write-Host '[INFO] No toolchain was downloaded and no source was pushed automatically.'
    Write-Host '[INFO] Review the commit, push it, and only then run the separate toolchain installer.'
    exit 0
} catch {
    Write-Host $_.Exception.Message
    exit 1
}
